using System.Collections.Generic;
using System.Linq;
using Mirror;
using UnityEngine;

public class PlayerQuest : NetworkBehaviour
{
    [Header("Quest Configuration")]
    [SerializeField] private int maxActiveQuests = 5;
    
    private List<Quest> _activeQuests = new();
    private Dictionary<string, QuestDestination> _questDestinations = new();
    
    public System.Action<Quest> OnQuestAdded;
    public System.Action<Quest> OnQuestCompleted;
    public System.Action<List<Quest>> OnQuestListUpdated;
    
    public override void OnStartClient()
    {
        base.OnStartClient();
        
        if (isLocalPlayer)
        {
            // Подписываемся на события QuestManager
            if (QuestManager.Instance != null)
            {
                QuestManager.Instance.OnQuestAccepted += HandleQuestAccepted;
                QuestManager.Instance.OnQuestCompleted += HandleQuestCompleted;
            }
            
            // Кэшируем все quest destinations для быстрого доступа
            CacheQuestDestinations();
        }
    }
    
    public override void OnStopClient()
    {
        if (isLocalPlayer && QuestManager.Instance != null)
        {
            QuestManager.Instance.OnQuestAccepted -= HandleQuestAccepted;
            QuestManager.Instance.OnQuestCompleted -= HandleQuestCompleted;
        }
        
        base.OnStopClient();
    }
    
    private void CacheQuestDestinations()
    {
        var destinations = FindObjectsByType<QuestDestination>(FindObjectsSortMode.None);
        _questDestinations.Clear();
        
        foreach (var destination in destinations)
        {
            _questDestinations[destination.destinationName] = destination;
        }
    }
    
    [Command]
    public void CmdInteractWithQuestZone(uint questZoneNetId)
    {
        if (NetworkServer.spawned.TryGetValue(questZoneNetId, out var questZoneObject))
        {
            var questZone = questZoneObject.GetComponent<QuestZone>();
            if (questZone != null)
            {
                // Проверяем, можем ли мы принять новый квест
                if (_activeQuests.Count >= maxActiveQuests)
                {
                    TargetShowMessage(connectionToClient, 
                        $"У вас уже максимальное количество активных квестов ({maxActiveQuests})");
                    return;
                }
                
                questZone.TryGiveQuest(connectionToClient.identity);
            }
        }
    }
    
    [Command]
    public void CmdAbandonQuest(string questId)
    {
        if (QuestManager.Instance != null)
        {
            var quest = _activeQuests.FirstOrDefault(q => q.questId == questId);
            if (quest != null)
            {
                _activeQuests.Remove(quest);
                
                // Убираем квест из QuestManager
                var playerQuests = QuestManager.Instance.GetActiveQuests(netId);
                var questToRemove = playerQuests.FirstOrDefault(q => q.questId == questId);
                if (questToRemove != null)
                {
                    playerQuests.Remove(questToRemove);
                }
                
                TargetQuestAbandoned(connectionToClient, quest.questName);
                UpdateQuestDestinationMarkers();
            }
        }
    }
    
    [Command]
    public void CmdRequestQuestStatus(string questId)
    {
        var quest = _activeQuests.FirstOrDefault(q => q.questId == questId);
        if (quest != null)
        {
            // Можно добавить логику для проверки прогресса квеста
            float distanceToDestination = 0f;
            if (_questDestinations.TryGetValue(quest.destinationName, out var destination))
            {
                distanceToDestination = Vector3.Distance(transform.position, destination.transform.position);
            }
            
            TargetQuestStatusUpdate(connectionToClient, questId, distanceToDestination);
        }
    }
    
    private void HandleQuestAccepted(Quest quest)
    {
        if (!isLocalPlayer) return;
        
        if (_activeQuests.All(q => q.questId != quest.questId))
        {
            _activeQuests.Add(quest);
            OnQuestAdded?.Invoke(quest);
            OnQuestListUpdated?.Invoke(_activeQuests);
            
            UpdateQuestDestinationMarkers();
            
            Debug.Log($"Quest accepted: {quest.questName}");
        }
    }
    
    private void HandleQuestCompleted(Quest quest)
    {
        if (!isLocalPlayer) return;
        
        var completedQuest = _activeQuests.FirstOrDefault(q => q.questId == quest.questId);
        if (completedQuest != null)
        {
            _activeQuests.Remove(completedQuest);
            OnQuestCompleted?.Invoke(completedQuest);
            OnQuestListUpdated?.Invoke(_activeQuests);
            
            UpdateQuestDestinationMarkers();
            
            Debug.Log($"Quest completed: {quest.questName} - Reward: {quest.reward}");
        }
    }
    
    private void UpdateQuestDestinationMarkers()
    {
        // Обновляем видимость маркеров для всех destinations
        foreach (var kvp in _questDestinations)
        {
            string destinationName = kvp.Key;
            QuestDestination destination = kvp.Value;
            
            bool hasActiveQuest = _activeQuests.Any(q => q.destinationName == destinationName && !q.isCompleted);
            destination.UpdateMarkerVisibility(hasActiveQuest);
        }
    }
    
    [TargetRpc]
    private void TargetShowMessage(NetworkConnection target, string message)
    {
        UIManager.Instance?.ShowNotification(message);
    }
    
    [TargetRpc]
    private void TargetQuestAbandoned(NetworkConnection target, string questName)
    {
        UIManager.Instance?.ShowNotification($"Квест отменен: {questName}");
    }
    
    [TargetRpc]
    private void TargetQuestStatusUpdate(NetworkConnection target, string questId, float distanceToDestination)
    {
        // Можно использовать для обновления UI с информацией о расстоянии до цели
        Debug.Log($"Quest {questId} - Distance to destination: {distanceToDestination:F1}m");
    }
    
    // Публичные методы для UI и других систем
    public List<Quest> GetActiveQuests() => new List<Quest>(_activeQuests);
    
    public Quest GetQuestById(string questId) => _activeQuests.FirstOrDefault(q => q.questId == questId);
    
    public bool HasActiveQuest(string questId) => _activeQuests.Any(q => q.questId == questId);
    
    public int GetActiveQuestCount() => _activeQuests.Count;
    
    public bool CanAcceptNewQuest() => _activeQuests.Count < maxActiveQuests;
    
    // Метод для получения расстояния до цели квеста
    public float GetDistanceToQuestDestination(string questId)
    {
        var quest = GetQuestById(questId);
        if (quest != null && _questDestinations.TryGetValue(quest.destinationName, out var destination))
        {
            return Vector3.Distance(transform.position, destination.transform.position);
        }

        return -1f;
    }
    
    // Метод для получения направления к цели квеста
    public Vector3 GetDirectionToQuestDestination(string questId)
    {
        var quest = GetQuestById(questId);
        if (quest != null && _questDestinations.TryGetValue(quest.destinationName, out var destination))
        {
            return (destination.transform.position - transform.position).normalized;
        }

        return Vector3.zero;
    }
    
    private void OnDrawGizmosSelected()
    {
        if (!Application.isPlaying || !isLocalPlayer) return;
        
        // Рисуем линии к активным quest destinations в Scene view
        Gizmos.color = Color.yellow;
        foreach (var quest in _activeQuests)
        {
            if (_questDestinations.TryGetValue(quest.destinationName, out var destination))
            {
                Gizmos.DrawLine(transform.position, destination.transform.position);
                Gizmos.DrawWireSphere(destination.transform.position, 2f);
            }
        }
    }
}
using System.Collections.Generic;
using System.Linq;
using Mirror;
using UnityEngine;

public class QuestManager : NetworkBehaviour
{
    public static QuestManager Instance;
    
    [SyncVar(hook = nameof(OnActiveQuestsChanged))]
    private string _activeQuestsData = "";
    
    private Dictionary<uint, List<Quest>> playerQuests = new Dictionary<uint, List<Quest>>();
    private Dictionary<uint, HashSet<string>> completedQuests = new Dictionary<uint, HashSet<string>>();
    private Dictionary<uint, Dictionary<string, float>> questCooldowns = new Dictionary<uint, Dictionary<string, float>>();
    private List<QuestDestination> questDestinations = new List<QuestDestination>();
    
    public event System.Action<Quest> OnQuestCompleted;
    public event System.Action<Quest> OnQuestAccepted;
    
    private void Awake()
    {
        if (Instance && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }
    
    public override void OnStartServer()
    {
        // Находим все точки назначения квестов на сцене
        questDestinations.AddRange(FindObjectsByType<QuestDestination>(FindObjectsSortMode.None));
        Debug.Log($"Found {questDestinations.Count} quest destinations");
    }
    
    [Server]
    public void CheckQuestCompletion(NetworkIdentity playerIdentity, string destinationName)
    {
        if (playerIdentity == null) return;
        
        uint playerId = playerIdentity.netId;
        if (!playerQuests.ContainsKey(playerId)) return;
        
        var quests = playerQuests[playerId];
        for (int i = quests.Count - 1; i >= 0; i--)
        {
            var quest = quests[i];
            if (!quest.isCompleted && quest.destinationName == destinationName)
            {
                CompleteQuest(playerIdentity, quest);
            }
        }
    }
    
    [Server]
    private void CompleteQuest(NetworkIdentity playerIdentity, Quest quest)
    {
        if (playerIdentity == null || playerIdentity.connectionToClient == null) return;
        
        quest.isCompleted = true;
        uint playerId = playerIdentity.netId;
        
        // Даем награду игроку
        var playerInventory = playerIdentity.GetComponent<PlayerInventory>();
        if (playerInventory)
        {
            playerInventory.AddMoney(quest.reward);
        }
        
        // Добавляем квест в список выполненных
        if (!completedQuests.ContainsKey(playerId))
            completedQuests[playerId] = new HashSet<string>();
        completedQuests[playerId].Add(quest.questId);
        
        // Если квест повторяющийся, устанавливаем кулдаун
        if (quest.isRepeatable)
        {
            if (!questCooldowns.ContainsKey(playerId))
                questCooldowns[playerId] = new Dictionary<string, float>();
            questCooldowns[playerId][quest.questId] = Time.time + quest.repeatCooldown;
        }
        
        // Удаляем выполненный квест из активных
        playerQuests[playerId].Remove(quest);
        SyncQuestData(playerIdentity);
        
        // Уведомляем клиента о завершении квеста
        TargetQuestCompleted(playerIdentity.connectionToClient, quest);
        
        Debug.Log($"Quest '{quest.questName}' completed by player {playerId}. Reward: {quest.reward}");
    }
    
    [Server]
    private void SyncQuestData(NetworkIdentity playerIdentity)
    {
        if (playerIdentity == null || playerIdentity.connectionToClient == null) return;
        
        uint playerId = playerIdentity.netId;
        if (!playerQuests.ContainsKey(playerId)) return;
        
        string questData = "";
        foreach (var quest in playerQuests[playerId])
        {
            questData += $"{quest.questId}|{quest.questName}|{quest.description}|{quest.reward}|" +
                         $"{quest.destinationName}|{quest.isRepeatable};";
        }
        
        TargetSyncQuests(playerIdentity.connectionToClient, questData);
    }
    
    [TargetRpc]
    private void TargetSyncQuests(NetworkConnection target, string questData)
    {
        _activeQuestsData = questData;
    }
    
    [TargetRpc]
    private void TargetQuestAccepted(NetworkConnection target, Quest quest)
    {
        OnQuestAccepted?.Invoke(quest);
        UIManager.Instance?.ShowNotification($"Новый квест: {quest.questName}");
    }
    
    [TargetRpc]
    private void TargetQuestCompleted(NetworkConnection target, Quest quest)
    {
        OnQuestCompleted?.Invoke(quest);
        string message = $"Квест выполнен: {quest.questName} (+{quest.reward}$)";
        if (quest.isRepeatable)
        {
            message += " (Повторяющийся)";
        }

        UIManager.Instance?.ShowNotification(message);
    }
    
    [TargetRpc]
    private void TargetQuestOnCooldown(NetworkConnection target, string questName, float remainingTime)
    {
        UIManager.Instance?.ShowNotification($"Квест '{questName}' будет доступен через {remainingTime:F0} секунд");
    }
    
    private void OnActiveQuestsChanged(string oldData, string newData)
    {
        if (isLocalPlayer)
        {
            ParseQuestData(newData);
        }
    }
    
    private void ParseQuestData(string questData)
    {
        // Парсим данные квестов для локального игрока
        // Здесь можно обновить UI квестов
    }
    
    public List<Quest> GetActiveQuests(uint playerId)
    {
        return playerQuests.TryGetValue(playerId, out var quest) ? quest : new List<Quest>();
    }
    
    [Server]
    public bool CanAcceptQuest(uint playerId, string questId, out float cooldownRemaining)
    {
        cooldownRemaining = 0f;
        
        // Проверяем, есть ли уже активный квест
        if (playerQuests.ContainsKey(playerId) && 
            playerQuests[playerId].Exists(q => q.questId == questId))
        {
            return false;
        }
        
        // Проверяем кулдаун
        if (questCooldowns.ContainsKey(playerId) && 
            questCooldowns[playerId].ContainsKey(questId))
        {
            float cooldownEnd = questCooldowns[playerId][questId];
            if (Time.time < cooldownEnd)
            {
                cooldownRemaining = cooldownEnd - Time.time;
                return false;
            }
        }
        
        return true;
    }
    
    // Метод для получения информации о выполненных квестах
    [Server]
    public HashSet<string> GetCompletedQuests(uint playerId)
    {
        return completedQuests.TryGetValue(playerId, out var quest) ? new HashSet<string>(quest) : new HashSet<string>();
    }
    
    // Add these methods to your existing QuestManager class:

    [Server]
    public void RemoveQuestFromPlayer(NetworkIdentity playerIdentity, string questId)
    {
        if (playerIdentity == null) return;
        
        uint playerId = playerIdentity.netId;
        if (!playerQuests.ContainsKey(playerId)) return;
        
        var quest = playerQuests[playerId].FirstOrDefault(q => q.questId == questId);
        if (quest != null)
        {
            playerQuests[playerId].Remove(quest);
            SyncQuestData(playerIdentity);
            
            TargetQuestRemoved(playerIdentity.connectionToClient, quest.questName);
            Debug.Log($"Quest '{quest.questName}' removed from player {playerId}");
        }
    }

    [TargetRpc]
    private void TargetQuestRemoved(NetworkConnection target, string questName)
    {
        UIManager.Instance?.ShowNotification($"Квест отменен: {questName}");
    }

    // Update the TryGiveQuest method to work better with PlayerQuest
    [Server]
    public bool TryGiveQuest(NetworkIdentity playerIdentity, Quest quest)
    {
        if (playerIdentity == null || playerIdentity.connectionToClient == null)
        {
            Debug.LogWarning("Player identity or connection is null");
            return false;
        }
        
        uint playerId = playerIdentity.netId;
        
        // Get PlayerQuest component to check quest limits
        var playerQuestComponent = playerIdentity.GetComponent<PlayerQuest>();
        if (playerQuestComponent != null && !playerQuestComponent.CanAcceptNewQuest())
        {
            TargetQuestLimitReached(playerIdentity.connectionToClient);
            return false;
        }
        
        // Initialize data structures for player
        if (!playerQuests.ContainsKey(playerId))
            playerQuests[playerId] = new List<Quest>();
        if (!completedQuests.ContainsKey(playerId))
            completedQuests[playerId] = new HashSet<string>();
        if (!questCooldowns.ContainsKey(playerId))
            questCooldowns[playerId] = new Dictionary<string, float>();
        
        // Check if player already has this quest
        if (playerQuests[playerId].Exists(q => q.questId == quest.questId))
        {
            Debug.Log($"Player already has quest: {quest.questName}");
            TargetQuestAlreadyActive(playerIdentity.connectionToClient, quest.questName);
            return false;
        }
        
        // Check if quest was already completed
        if (completedQuests[playerId].Contains(quest.questId))
        {
            if (!quest.isRepeatable)
            {
                Debug.Log($"Quest {quest.questName} already completed and not repeatable");
                TargetQuestAlreadyCompleted(playerIdentity.connectionToClient, quest.questName);
                return false;
            }
            
            // Check cooldown for repeatable quest
            if (questCooldowns[playerId].ContainsKey(quest.questId))
            {
                float cooldownEnd = questCooldowns[playerId][quest.questId];
                if (Time.time < cooldownEnd)
                {
                    float remainingTime = cooldownEnd - Time.time;
                    Debug.Log($"Quest {quest.questName} on cooldown for {remainingTime:F1} seconds");
                    TargetQuestOnCooldown(playerIdentity.connectionToClient, quest.questName, remainingTime);
                    return false;
                }
            }
        }
        
        // Create quest copy for player
        Quest playerQuest = new Quest(
            quest.questId,
            quest.questName,
            quest.description,
            quest.reward,
            Vector3.zero,
            quest.destinationName,
            quest.isRepeatable,
            quest.repeatCooldown
        );
        
        playerQuests[playerId].Add(playerQuest);
        SyncQuestData(playerIdentity);
        
        // Notify client about new quest
        TargetQuestAccepted(playerIdentity.connectionToClient, playerQuest);
        
        Debug.Log($"Quest '{playerQuest.questName}' given to player {playerId}");
        return true;
    }

    [TargetRpc]
    private void TargetQuestLimitReached(NetworkConnection target)
    {
        UIManager.Instance?.ShowNotification("Вы уже взяли максимальное количество квестов!");
    }

    [TargetRpc]
    private void TargetQuestAlreadyActive(NetworkConnection target, string questName)
    {
        UIManager.Instance?.ShowNotification($"У вас уже есть активный квест: {questName}");
    }

    [TargetRpc]
    private void TargetQuestAlreadyCompleted(NetworkConnection target, string questName)
    {
        UIManager.Instance?.ShowNotification($"Квест '{questName}' уже выполнен и не может быть повторен");
    }
}
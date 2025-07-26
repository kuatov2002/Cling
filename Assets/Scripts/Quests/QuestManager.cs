using System.Collections.Generic;
using System.Linq;
using Mirror;
using UnityEngine;

namespace Quests
{
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
        public event System.Action<Quest, QuestObjective> OnObjectiveCompleted;
    
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
            // Используем цикл for, т.к. коллекция может измениться внутри цикла
            for (int i = quests.Count - 1; i >= 0; i--)
            {
                var quest = quests[i];
                if (quest.isCompleted) continue;
        
                var currentObjective = quest.GetCurrentObjective();
                if (currentObjective is { isCompleted: false } && currentObjective.destinationName == destinationName)
                {
                    // Сначала продвигаем квест (это обновит isCompleted для текущей цели)
                    bool questIsNowComplete = quest.AdvanceObjective(); 
            
                    // Теперь отправляем уведомление с обновленным состоянием
                    TargetObjectiveCompleted(playerIdentity.connectionToClient, quest, currentObjective); 

                    if (questIsNowComplete)
                    {
                        CompleteQuest(playerIdentity, quest);
                    }
                    else
                    {
                        // Цель выполнена, но квест продолжается. Просто синхронизируем данные.
                        // (Логика синхронизации должна быть добавлена или улучшена)
                        Debug.Log($"Objective for '{quest.questName}' completed. Next objective is now active.");
                        // Возможно, стоит синхронизировать данные здесь, если UI должен отображать промежуточное состояние
                        // SyncQuestData(playerIdentity); 
                    }
            
                    break; 
                }
            }
        }
    
        [Server]
        private void CompleteQuest(NetworkIdentity playerIdentity, Quest quest)
        {
            if (playerIdentity == null || playerIdentity.connectionToClient == null) return;
        
            uint playerId = playerIdentity.netId;
        
            var playerInventory = playerIdentity.GetComponent<PlayerInventory>();
            if (playerInventory)
            {
                playerInventory.AddMoney(quest.reward);
            }
        
            if (!completedQuests.ContainsKey(playerId))
                completedQuests[playerId] = new HashSet<string>();
            completedQuests[playerId].Add(quest.questId);
        
            if (quest.isRepeatable)
            {
                if (!questCooldowns.ContainsKey(playerId))
                    questCooldowns[playerId] = new Dictionary<string, float>();
                questCooldowns[playerId][quest.questId] = Time.time + quest.repeatCooldown;
            }
        
            playerQuests[playerId].Remove(quest);
            SyncQuestData(playerIdentity);
        
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
                // Serialize quest with objectives
                string objectivesData = "";
                foreach (var objective in quest.objectives)
                {
                    objectivesData += $"{objective.objectiveId}#{objective.destinationName}#{objective.description}#{objective.isCompleted}^";
                }
            
                questData += $"{quest.questId}|{quest.questName}|{quest.description}|{quest.reward}|" +
                             $"{quest.isRepeatable}|{objectivesData};";
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
        private void TargetObjectiveCompleted(NetworkConnection target, Quest quest, QuestObjective objective)
        {
            OnObjectiveCompleted?.Invoke(quest, objective);
            string message = $"Цель выполнена: {objective.description} ({quest.CompletedObjectives}/{quest.objectives.Count})";
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
            // Parse quest data with objectives for local player
            // Update quest UI here if needed
        }
    
        public List<Quest> GetActiveQuests(uint playerId)
        {
            return playerQuests.TryGetValue(playerId, out var quest) ? quest : new List<Quest>();
        }
    
        [Server]
        public bool CanAcceptQuest(uint playerId, string questId, out float cooldownRemaining)
        {
            cooldownRemaining = 0f;
        
            if (playerQuests.ContainsKey(playerId) && 
                playerQuests[playerId].Exists(q => q.questId == questId))
            {
                return false;
            }
        
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
    
        [Server]
        public HashSet<string> GetCompletedQuests(uint playerId)
        {
            return completedQuests.TryGetValue(playerId, out var quest) ? new HashSet<string>(quest) : new HashSet<string>();
        }
    
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

        [Server]
        public bool TryGiveQuest(NetworkIdentity playerIdentity, Quest quest)
        {
            if (playerIdentity == null || playerIdentity.connectionToClient == null)
            {
                Debug.LogWarning("Player identity or connection is null");
                return false;
            }
        
            uint playerId = playerIdentity.netId;
        
            var playerQuestComponent = playerIdentity.GetComponent<PlayerQuest>();
            if (playerQuestComponent != null && !playerQuestComponent.CanAcceptNewQuest())
            {
                TargetQuestLimitReached(playerIdentity.connectionToClient);
                return false;
            }
        
            if (!playerQuests.ContainsKey(playerId))
                playerQuests[playerId] = new List<Quest>();
            if (!completedQuests.ContainsKey(playerId))
                completedQuests[playerId] = new HashSet<string>();
            if (!questCooldowns.ContainsKey(playerId))
                questCooldowns[playerId] = new Dictionary<string, float>();
        
            if (playerQuests[playerId].Exists(q => q.questId == quest.questId))
            {
                Debug.Log($"Player already has quest: {quest.questName}");
                TargetQuestAlreadyActive(playerIdentity.connectionToClient, quest.questName);
                return false;
            }
        
            if (completedQuests[playerId].Contains(quest.questId))
            {
                if (!quest.isRepeatable)
                {
                    Debug.Log($"Quest {quest.questName} already completed and not repeatable");
                    TargetQuestAlreadyCompleted(playerIdentity.connectionToClient, quest.questName);
                    return false;
                }
            
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
        
            // Create quest copy with reset objectives
            Quest playerQuest = new Quest(
                quest.questId,
                quest.questName,
                quest.description,
                quest.reward,
                quest.objectives.Select(obj => new QuestObjective(obj.objectiveId, obj.destinationName, obj.description)).ToList(),
                quest.isRepeatable,
                quest.repeatCooldown
            );
        
            playerQuests[playerId].Add(playerQuest);
            SyncQuestData(playerIdentity);
        
            TargetQuestAccepted(playerIdentity.connectionToClient, playerQuest);
        
            Debug.Log($"Quest '{playerQuest.questName}' with {playerQuest.objectives.Count} objectives given to player {playerId}");
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
}
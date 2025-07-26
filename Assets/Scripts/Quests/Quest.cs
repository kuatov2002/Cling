using System;
using System.Collections.Generic;
using System.Linq;
using TriInspector;
using UnityEngine;

namespace Quests
{
    [Serializable]
    public class QuestObjective
    {
        public string objectiveId;
        public string destinationName;
        public string description;
        public bool isCompleted;

        public QuestObjective()
        {
            objectiveId = "";
            destinationName = "";
            description = "";
            isCompleted = false;
        }
    
        public QuestObjective(string id, string destName, string desc, bool optional = false)
        {
            objectiveId = id;
            destinationName = destName;
            description = desc;
            isCompleted = false;
        }
    }

    [Serializable]
    public class Quest
    {
        public string questId;
        public string questName;
        [TextArea(3, 5)] public string description;
        public int reward;
        public bool isCompleted;
        public bool isRepeatable;
    
    
        [ShowIf(nameof(isRepeatable))] public float repeatCooldown;
    
        // Quest objectives (sub-quests)
        public List<QuestObjective> objectives;

        // Computed properties
        public int CompletedObjectives => objectives.Count(o => o.isCompleted);
        public bool IsReadyForCompletion => isCompleted;
        public int currentObjectiveIndex = 0;
        public float CompletionPercentage => objectives.Count > 0 ? (float)CompletedObjectives / objectives.Count : 0f;

        public Quest() 
        {
            questId = "";
            questName = "";
            description = "";
            reward = 0;
            isCompleted = false;
            isRepeatable = false;
            objectives = new List<QuestObjective>();
        }
    
        public Quest(string id, string name, string desc, int rewardAmount, List<QuestObjective> questObjectives, bool isRepeat, float questCooldown)
        {
            questId = id;
            questName = name;
            description = desc;
            reward = rewardAmount;
            isCompleted = false;
            isRepeatable = isRepeat;
            repeatCooldown = questCooldown;
            objectives = questObjectives ?? new List<QuestObjective>();
        }
    
        // Helper methods
        public QuestObjective GetObjectiveByDestination(string destName)
        {
            return objectives.FirstOrDefault(obj => obj.destinationName == destName);
        }
    
        public void ResetQuest()
        {
            isCompleted = false;
            currentObjectiveIndex = 0; // Сбрасываем индекс
            foreach (var objective in objectives)
            {
                objective.isCompleted = false;
            }
        }
    
        public QuestObjective GetCurrentObjective()
        {
            if (objectives == null || currentObjectiveIndex < 0 || currentObjectiveIndex >= objectives.Count)
            {
                return null;
            }
        
            return objectives[currentObjectiveIndex];
        }
    
        public List<string> GetActiveDestinations()
        {
            var activeDestinations = new List<string>();
            var currentObjective = GetCurrentObjective();
            if (currentObjective is { isCompleted: false })
            {
                activeDestinations.Add(currentObjective.destinationName);
            }
        
            return activeDestinations;
        }
    
        public bool AdvanceObjective()
        {
            var currentObjective = GetCurrentObjective();
            if (currentObjective is { isCompleted: false })
            {
                currentObjective.isCompleted = true;
                currentObjectiveIndex++;

                // Если это была последняя цель, квест завершен
                if (currentObjectiveIndex >= objectives.Count)
                {
                    isCompleted = true;
                    return true; // Квест полностью выполнен
                }
            }
        
            return false; // Квест еще не закончен
        }

        public List<string> GetAllDestinations()
        {
            return objectives.Select(obj => obj.destinationName).ToList();
        }
    }
}
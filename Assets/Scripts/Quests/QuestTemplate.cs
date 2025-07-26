using System;
using System.Collections.Generic;
using TriInspector;
using UnityEngine;

namespace Quests
{
    [Serializable]
    public class QuestObjectiveTemplate
    {
        public string objectiveId;
        public string destinationName;
        [TextArea(2, 3)] public string description;
    
        public QuestObjectiveTemplate()
        {
            objectiveId = "";
            destinationName = "";
            description = "";
        }
    }

    [Serializable]
    public class QuestTemplate
    {
        public string questId;
        public string questName;
        [TextArea(3, 5)] public string description;
        public int reward;
        public bool isRepeatable;
        [ShowIf(nameof(isRepeatable))] public float repeatCooldown;
    
        [Header("Quest Objectives")]
        public List<QuestObjectiveTemplate> objectives = new List<QuestObjectiveTemplate>();
        
    
        public Quest CreateQuest()
        {
            var questObjectives = new List<QuestObjective>();
        
            if (objectives is { Count: > 0 })
            {
                // Use new objective system
                foreach (var objTemplate in objectives)
                {
                    questObjectives.Add(new QuestObjective(
                        objTemplate.objectiveId,
                        objTemplate.destinationName,
                        objTemplate.description
                    ));
                }
            }
        
            return new Quest(
                questId,
                questName,
                description,
                reward,
                questObjectives,
                isRepeatable,
                repeatCooldown
            );
        }
    }
}
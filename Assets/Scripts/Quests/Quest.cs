using System; 
using TriInspector;
using UnityEngine;

[Serializable]
public class Quest
{
    public string questId;
    public string questName;
    public string description;
    public int reward;
    public bool isCompleted;
    public string destinationName;
    public bool isRepeatable;

    // Показываем кулдаун только если isRepeatable == true
    [ShowIf(nameof(isRepeatable))] public float repeatCooldown;

    // Конструктор по умолчанию для Mirror
    public Quest() 
    {
        questId = "";
        questName = "";
        description = "";
        reward = 0;
        isCompleted = false;
        destinationName = "";
        isRepeatable = false;
    }
    
    public Quest(string id, string name, string desc, int rewardAmount, Vector3 destination, string destName, bool isRepeat, float questCooldown)
    {
        questId = id;
        questName = name;
        description = desc;
        reward = rewardAmount;
        isCompleted = false;
        destinationName = destName;
        isRepeatable = isRepeat;
        repeatCooldown = questCooldown;
    }
}
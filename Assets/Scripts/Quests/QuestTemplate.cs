using System;
using TriInspector;
using UnityEngine;

[Serializable]
public class QuestTemplate
{
    public string questId;
    public string questName;
    [TextArea(3, 5)] public string description;
    public int reward;
    public string destinationName;
    public bool isRepeatable;
    [ShowIf(nameof(isRepeatable))] public float repeatCooldown;
}
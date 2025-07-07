using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class AbilitySlot : MonoBehaviour
{
    public string abilityName;
    public Image iconImage;
    public Image cooldownOverlay;
    public TextMeshProUGUI abilityNameText;
    
    [HideInInspector] public float cooldownDuration;
    [HideInInspector] public float currentCooldown;
    [HideInInspector] public bool isOnCooldown;

    public void RegisterSlot(string name, float cooldown, Sprite icon)
    {
        abilityName = name;
        cooldownDuration = cooldown;
        iconImage.sprite = icon;
        abilityNameText.text = name;
        
        currentCooldown = 0f;
        isOnCooldown = false;
    }
}

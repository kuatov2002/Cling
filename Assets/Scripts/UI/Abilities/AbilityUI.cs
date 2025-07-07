using System.Collections.Generic;
using UnityEngine;

public class AbilityUI : MonoBehaviour
{
    private List<AbilitySlot> _abilitySlots = new();
    private readonly Dictionary<string, AbilitySlot> _abilityMap = new();

    [SerializeField] private AbilitySlot slotPrefab;
    
    private void Update()
    {
        UpdateCooldowns();
    }
    
    public void RegisterAbility(string abilityName, float cooldown, Sprite icon)
    {
        var sloUI = Instantiate(slotPrefab, transform);
        sloUI.RegisterSlot(abilityName, cooldown, icon);
        _abilitySlots.Add(sloUI);
        
        _abilityMap[sloUI.abilityName] = sloUI;
        SetSlotVisibility(sloUI, true);
    }
    
    public void UnregisterAbility(string abilityName)
    {
        if (_abilityMap.TryGetValue(abilityName, out var slot))
        {
            slot.abilityName = "";
            slot.currentCooldown = 0f;
            slot.isOnCooldown = false;
            
            if (slot.iconImage)
            {
                slot.iconImage = null;
            }
            
            if (slot.cooldownOverlay)
            {
                slot.cooldownOverlay.fillAmount = 0f;
            }
            
            SetSlotVisibility(slot, false);
            _abilityMap.Remove(abilityName);
        }
    }
    
    public void StartCooldown(string abilityName)
    {
        if (_abilityMap.TryGetValue(abilityName, out var slot))
        {
            slot.currentCooldown = slot.cooldownDuration;
            slot.isOnCooldown = true;
        }
    }
    
    public void StartCooldownWithTime(string abilityName, float remainingTime)
    {
        if (_abilityMap.TryGetValue(abilityName, out var slot))
        {
            slot.currentCooldown = remainingTime;
            slot.isOnCooldown = remainingTime > 0f;
        }
    }
    
    public void ForceAbilityReady(string abilityName)
    {
        if (_abilityMap.TryGetValue(abilityName, out var slot))
        {
            slot.currentCooldown = 0f;
            slot.isOnCooldown = false;
            
            if (slot.cooldownOverlay)
            {
                slot.cooldownOverlay.fillAmount = 0f;
            }
        }
    }
    
    public bool IsAbilityReady(string abilityName)
    {
        if (_abilityMap.TryGetValue(abilityName, out var slot))
        {
            return !slot.isOnCooldown;
        }

        return false;
    }
    
    public float GetCooldownProgress(string abilityName)
    {
        if (_abilityMap.TryGetValue(abilityName, out var slot))
        {
            return slot.isOnCooldown ? 1f - (slot.currentCooldown / slot.cooldownDuration) : 1f;
        }

        return 1f;
    }
    
    private void UpdateCooldowns()
    {
        foreach (var slot in _abilitySlots)
        {
            if (!slot.isOnCooldown) continue;
            
            slot.currentCooldown -= Time.deltaTime;
            
            if (slot.currentCooldown <= 0f)
            {
                slot.currentCooldown = 0f;
                slot.isOnCooldown = false;
                
                if (slot.cooldownOverlay)
                {
                    slot.cooldownOverlay.fillAmount = 0f;
                }
            }
            else
            {
                UpdateSlotCooldownUI(slot);
            }
        }
    }
    
    private void UpdateSlotCooldownUI(AbilitySlot slot)
    {
        float progress = slot.currentCooldown / slot.cooldownDuration;
        
        if (slot.cooldownOverlay)
        {
            slot.cooldownOverlay.fillAmount = progress;
        }
    }
    
    private void SetSlotVisibility(AbilitySlot slot, bool visible)
    {
        if (slot.cooldownOverlay) slot.cooldownOverlay.gameObject.SetActive(visible);
    }
}
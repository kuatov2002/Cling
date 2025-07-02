using System;
using UnityEngine;
using Mirror;
using Unity.VisualScripting;

[Serializable]
public abstract class BaseItem : NetworkBehaviour
{
    public string itemName;
    public string itemDescription;

    public string itemTooltip;
    public Sprite itemIcon;
    

    public virtual void Use(PlayerInventory playerInventory = null)
    {
        // Реализация использования предмета
    }

    public virtual bool CanUse()
    {
        return true;
    }
    
    public virtual void TakeItem(PlayerInventory inventory)
    {
        // Логика добавления в инвентарь
    }
}
using UnityEngine;
using Mirror;

public abstract class BaseItem : NetworkBehaviour
{
    [SerializeField] protected InventoryItemData data;
    
    public InventoryItemData Data => data;
    
    protected virtual void Start()
    {
        if (data == null)
        {
            Debug.LogError($"Item data not assigned for {gameObject.name}");
        }
    }
    
    public virtual void Use(PlayerInventory playerInventory = null)
    {
        // Default implementation - override in derived classes
    }
    
    public virtual bool CanUse()
    {
        return data != null;
    }
    
    public virtual void Initialize(InventoryItemData itemData)
    {
        data = itemData;
    }

    public virtual void TakeItem(PlayerInventory inventory)
    {
        //прописать логику добавления в инвентарь
    }
}
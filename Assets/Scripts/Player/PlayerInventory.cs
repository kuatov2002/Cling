using Mirror;
using UnityEngine;

public class PlayerInventory : NetworkBehaviour
{
    [Header("Inventory Configuration")]
    [SerializeField] private BaseItem[] inventorySlots;

    [Header("Active Item Display")]
    [SerializeField] private int activeItemIndex = 0;

    [Header("Input")]
    [SerializeField] private KeyCode[] hotkeys = {
        KeyCode.Alpha1, KeyCode.Alpha2, KeyCode.Alpha3,
        KeyCode.Alpha4, KeyCode.Alpha5
    };
    [SerializeField] private KeyCode useItemKey = KeyCode.E;

    public BaseItem CurrentActiveSlot => 
        inventorySlots != null && activeItemIndex < inventorySlots.Length 
            ? inventorySlots[activeItemIndex] 
            : null;

    public BaseItem CurrentActiveItem => CurrentActiveSlot;
    public int ActiveItemIndex => activeItemIndex;

    private void Start()
    {
        if (!isLocalPlayer) return;
        UpdateActiveItem();
    }

    private void Update()
    {
        if (!isLocalPlayer) return;
        HandleHotkeyInput();
        HandleUseItemInput();
    }

    private void HandleHotkeyInput()
    {
        for (int i = 0; i < hotkeys.Length && i < inventorySlots.Length; i++)
        {
            if (Input.GetKeyDown(hotkeys[i]))
            {
                SetActiveItem(i);
                break;
            }
        }
    }

    private void HandleUseItemInput()
    {
        if (Input.GetKeyDown(useItemKey))
        {
            UseActiveItem();
        }
    }

    private void UseActiveItem()
    {
        BaseItem activeItem = CurrentActiveItem;
        if (activeItem && activeItem.CanUse())
        {
            CmdUseItem(activeItemIndex);
        }
    }

    private void SetActiveItem(int index)
    {
        if (index < 0 || index >= inventorySlots.Length)
        {
            Debug.LogWarning($"Invalid inventory index: {index}");
            return;
        }

        activeItemIndex = index;
        UpdateActiveItem();
    }

    private void UpdateActiveItem()
    {
        OnActiveItemChanged();
        // Обновляем UI только для локального игрока
        if (isLocalPlayer)
        {
            UIManager.Instance?.UpdateInventoryUI(inventorySlots);
        }
    }

    public bool AddItem(BaseItem item)
    {
        if (!item) return false;

        for (int i = 0; i < inventorySlots.Length; i++)
        {
            if (!inventorySlots[i])
            {
                inventorySlots[i] = item;
                if (i == activeItemIndex)
                {
                    UpdateActiveItem();
                }

                if (isLocalPlayer)
                {
                    UIManager.Instance?.UpdateInventoryUI(inventorySlots);
                }
                return true;
            }
        }

        return false;
    }

    public bool RemoveItem(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= inventorySlots.Length || 
            inventorySlots[slotIndex] == null)
        {
            return false;
        }

        inventorySlots[slotIndex] = null;
        
        if (slotIndex == activeItemIndex)
        {
            UpdateActiveItem();
        }
        
        if (isLocalPlayer)
        {
            UIManager.Instance?.UpdateInventoryUI(inventorySlots);
        }
        return true;
    }

    protected virtual void OnActiveItemChanged()
    {
        string itemName = CurrentActiveItem?.Data?.itemName ?? "None";
        Debug.Log($"Active item changed to: {itemName} (Index: {activeItemIndex})");
    }

    public void OnInventorySlotChanged(int slotIndex)
    {
        if (slotIndex == activeItemIndex)
        {
            UpdateActiveItem();
        }
        else if (isLocalPlayer)
        {
            UIManager.Instance?.UpdateInventoryUI(inventorySlots);
        }
    }

    // ИСПРАВЛЕННЫЕ методы сетевой синхронизации
    [TargetRpc]
    private void TargetSyncInventory(NetworkConnection target, BaseItem[] items)
    {
        if (inventorySlots == null) return;

        for (int i = 0; i < Mathf.Min(items.Length, inventorySlots.Length); i++)
        {
            inventorySlots[i] = items[i];
        }
        
        UpdateActiveItem();
    }

    [Command]
    public void CmdAddItem(BaseItem item)
    {
        if (AddItem(item))
        {
            var items = new BaseItem[inventorySlots.Length];
            for (int i = 0; i < inventorySlots.Length; i++)
            {
                items[i] = inventorySlots[i];
            }

            // Отправляем только владельцу объекта
            TargetSyncInventory(connectionToClient, items);
        }
    }
    
    [Command]
    private void CmdUseItem(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= inventorySlots.Length || 
            !inventorySlots[slotIndex])
            return;

        BaseItem item = inventorySlots[slotIndex];
        if (item.CanUse())
        {
            item.Use();
            inventorySlots[slotIndex] = null;
            
            // Отправляем обновленный инвентарь только владельцу
            TargetSyncInventory(connectionToClient, inventorySlots);
        }
    }
}
using Mirror;
using UnityEngine;

public class PlayerInventory : NetworkBehaviour
{
    [Header("Inventory Configuration")]
    [SerializeField] private BaseItem[] inventorySlots;

    [Header("Active Item Display")]
    [SerializeField] private int activeItemIndex = 0;

    [Header("Input")]
    [SerializeField] private KeyCode[] hotkeys =
    {
        KeyCode.Alpha1, KeyCode.Alpha2, KeyCode.Alpha3,
        KeyCode.Alpha4, KeyCode.Alpha5
    };

    [SerializeField] private KeyCode useItemKey = KeyCode.E;

    protected int money = 0;

    public BaseItem CurrentActiveSlot => 
        inventorySlots != null && activeItemIndex < inventorySlots.Length 
            ? inventorySlots[activeItemIndex] 
            : null;

    public BaseItem CurrentActiveItem => CurrentActiveSlot;
    public int ActiveItemIndex => activeItemIndex;
    public int Money => money;

    private void Start()
    {
        if (!isLocalPlayer) return;
        UpdateActiveItem();
        UpdateMoneyDisplay();
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
        if (isLocalPlayer)
        {
            UIManager.Instance?.UpdateInventoryUI(inventorySlots, activeItemIndex);
        }
    }

    private void UpdateMoneyDisplay()
    {
        if (isLocalPlayer)
        {
            UIManager.Instance?.UpdateMoney(money);
        }
    }

    public void SetMoney(int amount)
    {
        money = amount;
        UpdateMoneyDisplay();
    }

    public void AddMoney(int amount)
    {
        money += amount;
        UpdateMoneyDisplay();
    }

    public bool SpendMoney(int amount)
    {
        if (money >= amount)
        {
            money -= amount;
            UpdateMoneyDisplay();
            return true;
        }

        return false;
    }

    private bool AddItem(BaseItem item)
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
                    UIManager.Instance?.UpdateInventoryUI(inventorySlots, activeItemIndex);
                }

                return true;
            }
        }

        return false;
    }

    public bool RemoveItem(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= inventorySlots.Length || 
            !inventorySlots[slotIndex])
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
            UIManager.Instance?.UpdateInventoryUI(inventorySlots, activeItemIndex);
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
            UIManager.Instance?.UpdateInventoryUI(inventorySlots, activeItemIndex);
        }
    }

    [TargetRpc]
    private void TargetSyncInventory(NetworkConnection target)
    {
        UpdateActiveItem();
        UpdateMoneyDisplay();
    }

    [Command]
    public void CmdAddItem(BaseItem item)
    {
        if (AddItem(item))
        {
            TargetSyncInventory(connectionToClient);
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
            item.Use(this);
            inventorySlots[slotIndex] = null;
            
            TargetSyncInventory(connectionToClient);
        }
    }
}
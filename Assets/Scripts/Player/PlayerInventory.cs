using System.Collections;
using Mirror;
using UnityEngine;

public class PlayerInventory : NetworkBehaviour
{
    [Header("Inventory Configuration")]
    [SerializeField] private BaseItem[] initialInventorySlots = new BaseItem[5];
    private SyncList<BaseItem> inventorySlots = new();

    [Header("Active Item Display")]
    [SerializeField] private int activeItemIndex = 0;

    [Header("Input")]
    [SerializeField] private KeyCode[] hotkeys =
    {
        KeyCode.Alpha1, KeyCode.Alpha2, KeyCode.Alpha3,
        KeyCode.Alpha4, KeyCode.Alpha5
    };

    [SerializeField] private KeyCode useItemKey = KeyCode.E;

    [SyncVar(hook = nameof(OnMoneyChanged))]
    private int money = 2;

    public BaseItem CurrentActiveSlot => 
        inventorySlots != null && activeItemIndex < inventorySlots.Count 
            ? inventorySlots[activeItemIndex] 
            : null;

    public BaseItem CurrentActiveItem => CurrentActiveSlot;
    public int ActiveItemIndex => activeItemIndex;
    public int Money => money;

    [SyncVar(hook = nameof(OnLastMoneyTakeTimeChanged))]
    private float LastMoneyTakeTime = -Mathf.Infinity;
    
    [SerializeField] private float moneyTakeInterval = 10f;

    private void Start()
    {
        if (!isLocalPlayer) return;
        
        // Initialize inventory from inspector values
        if (inventorySlots.Count == 0)
        {
            for (int i = 0; i < initialInventorySlots.Length; i++)
            {
                inventorySlots.Add(initialInventorySlots[i]);
            }
        }
        
        // Subscribe to inventory changes
        inventorySlots.OnChange += OnInventoryChanged;
        
        UpdateActiveItem();
        UpdateMoneyDisplay();
    }

    public override void OnStartClient()
    {
        LastMoneyTakeTime = (float)NetworkTime.time;
        StartCoroutine(MoneyTakeRoutine());
        
        // Subscribe to inventory changes for all clients
        inventorySlots.OnChange += OnInventoryChanged;
    }

    private void OnInventoryChanged(SyncList<BaseItem>.Operation op, int index, BaseItem newItem)
    {
        if (!isLocalPlayer) return;
        
        if (index == activeItemIndex)
        {
            UpdateActiveItem();
        }
        
        UIManager.Instance?.UpdateInventoryUI(inventorySlots, activeItemIndex);
    }

    private IEnumerator MoneyTakeRoutine()
    {
        while (true)
        {
            if ((float)NetworkTime.time - LastMoneyTakeTime >= moneyTakeInterval)
            {
                money++;
                LastMoneyTakeTime = (float)NetworkTime.time;
            }

            yield return new WaitForSeconds(0.17f);
        }
    }

    private void OnMoneyChanged(int oldAmount, int newAmount)
    {
        if (isLocalPlayer)
        {
            UpdateMoneyDisplay();
        }
    }

    private void OnLastMoneyTakeTimeChanged(float oldAmount, float newAmount)
    {
        if (isLocalPlayer)
        {
            UpdateMoneyDisplay();
        }
    }

    private void Update()
    {
        if (!isLocalPlayer) return;
        HandleHotkeyInput();
        HandleUseItemInput();
    }

    private void HandleHotkeyInput()
    {
        for (int i = 0; i < hotkeys.Length && i < inventorySlots.Count; i++)
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
        if (index < 0 || index >= inventorySlots.Count)
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
    

    [Server]
    public bool SpendMoney(int amount)
    {
        if (money >= amount)
        {
            money -= amount;
            return true;
        }

        return false;
    }

    [Server]
    public bool AddItem(BaseItem item)
    {
        if (!item) return false;

        for (int i = 0; i < inventorySlots.Count; i++)
        {
            if (!inventorySlots[i])
            {
                inventorySlots[i] = item;
                return true;
            }
        }

        return false;
    }

    protected virtual void OnActiveItemChanged()
    {
        string itemName = CurrentActiveItem?.Data?.itemName ?? "None";
        Debug.Log($"Active item changed to: {itemName} (Index: {activeItemIndex})");
    }

    [Command]
    private void CmdUseItem(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= inventorySlots.Count || 
            !inventorySlots[slotIndex])
            return;

        BaseItem item = inventorySlots[slotIndex];
        if (item.CanUse())
        {
            item.Use(this);
            inventorySlots[slotIndex] = null;
        }
    }
}
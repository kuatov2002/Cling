using System.Collections;
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

    [SyncVar(hook = nameof(OnMoneyChanged))]
    protected int money = 2;

    // Синхронизация слотов инвентаря
    [SyncVar(hook = nameof(OnInventoryChanged))]
    private string inventoryData = "";

    public BaseItem CurrentActiveSlot => 
        inventorySlots != null && activeItemIndex < inventorySlots.Length 
            ? inventorySlots[activeItemIndex] 
            : null;

    public BaseItem CurrentActiveItem => CurrentActiveSlot;
    public int ActiveItemIndex => activeItemIndex;
    public int Money => money;

    [SyncVar(hook = nameof(OnLastMoneyTakeTimeChanged))]
    private float _lastMoneyTakeTime = -Mathf.Infinity;
    
    [SerializeField] private float moneyTakeInterval = 10f;

    public override void OnStartServer()
    {
        _lastMoneyTakeTime = (float)NetworkTime.time;
        StartCoroutine(MoneyTakeRoutine());
        SyncInventoryToClients();
    }

    public override void OnStartClient()
    {
        if (isLocalPlayer)
        {
            DeserializeInventory();
            UpdateActiveItem();
            UpdateMoneyDisplay();
        }
    }

    private IEnumerator MoneyTakeRoutine()
    {
        while (true)
        {
            if ((float)NetworkTime.time - _lastMoneyTakeTime >= moneyTakeInterval)
            {
                money++;
                _lastMoneyTakeTime = (float)NetworkTime.time;
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

    private void OnInventoryChanged(string oldData, string newData)
    {
        if (!isServer)
        {
            DeserializeInventory();
            if (isLocalPlayer)
            {
                UpdateActiveItem();
            }
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
        if (isLocalPlayer)
        {
            UIManager.Instance?.UpdateInventoryUI(inventorySlots, activeItemIndex);
            Debug.Log("Инвентарь обновлен");
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
    public void SetMoney(int amount)
    {
        money = amount;
    }

    [Server]
    public void AddMoney(int amount)
    {
        money += amount;
    }

    [Server]
    public virtual bool SpendMoney(int amount)
    {
        if (money >= amount)
        {
            money -= amount;
            return true;
        }

        return false;
    }

    public bool AddItem(BaseItem item)
    {
        if (!item) return false;

        for (int i = 0; i < inventorySlots.Length; i++)
        {
            if (!inventorySlots[i])
            {
                inventorySlots[i] = item;
                
                if (isServer)
                {
                    SyncInventoryToClients();
                }
                
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
        
        if (isServer)
        {
            SyncInventoryToClients();
        }
        
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

    [Command]
    public void CmdAddItem(BaseItem item)
    {
        if (AddItem(item))
        {
            SyncInventoryToClients();
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
            
            SyncInventoryToClients();
        }
    }

    [Server]
    private void SyncInventoryToClients()
    {
        inventoryData = SerializeInventory();
    }

    private string SerializeInventory()
    {
        string result = "";
        for (int i = 0; i < inventorySlots.Length; i++)
        {
            if (inventorySlots[i])
            {
                result += $"{i}:{inventorySlots[i].itemName};";
            }
        }

        return result;
    }

    private void DeserializeInventory()
    {
        // Очищаем инвентарь
        for (int i = 0; i < inventorySlots.Length; i++)
        {
            inventorySlots[i] = null;
        }

        if (string.IsNullOrEmpty(inventoryData)) return;

        string[] slots = inventoryData.Split(';');
        foreach (string slot in slots)
        {
            if (string.IsNullOrEmpty(slot)) continue;

            string[] parts = slot.Split(':');
            if (parts.Length == 2)
            {
                int index = int.Parse(parts[0]);
                string itemName = parts[1];
                
                // Находим предмет по имени (можно заменить на более сложную логику)
                BaseItem item = FindItemByName(itemName);
                if (item && index < inventorySlots.Length)
                {
                    inventorySlots[index] = item;
                }
            }
        }
    }

    private BaseItem FindItemByName(string itemName)
    {
        Debug.Log($"пытаюсь найти Items/{itemName}");
        // Простой поиск по имени в Resources или используйте ItemDatabase
        GameObject itemPrefab = Resources.Load<GameObject>($"Items/{itemName}");
        if (itemPrefab)
        {
            Debug.Log($"Нашел Items/{itemName}");
            return itemPrefab.GetComponent<BaseItem>();
        }

        Debug.Log($"Не наход Items/{itemName}");
        return null;
    }
}
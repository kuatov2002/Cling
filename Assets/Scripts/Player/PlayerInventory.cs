using Mirror;
using UnityEngine;
using System.Collections.Generic; // Необходимо для SyncList

// Mirror требует специальный тип для SyncList, если вы хотите хранить сложные объекты.
// Однако BaseItem - это ScriptableObject/NetworkBehaviour, который не может быть напрямую сериализован Mirror для SyncList.
// Поэтому мы будем хранить имя предмета (string) в SyncList.
// Это требует методов для преобразования между BaseItem и string.
[System.Serializable]
public class SyncListItemNames : SyncList<string> { }

public class PlayerInventory : NetworkBehaviour
{
    [Header("Inventory Configuration")]
    // Используем массив для определения размера и типа слотов (сервер/клиент)
    // Этот массив не будет синхронизироваться напрямую
    [SerializeField] private BaseItem[] inventorySlotsTemplate; 

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

    // Используем SyncList для синхронизации имен предметов в слотах
    public SyncListItemNames syncInventorySlotNames = new SyncListItemNames();

    // Локальный кэш для преобразования имен в объекты BaseItem
    // Этот массив заполняется на клиенте после десериализации имен из syncInventorySlotNames
    private BaseItem[] inventorySlots;

    public BaseItem CurrentActiveSlot => 
        inventorySlots != null && activeItemIndex < inventorySlots.Length 
            ? inventorySlots[activeItemIndex] 
            : null;
    public BaseItem CurrentActiveItem => CurrentActiveSlot;
    public int ActiveItemIndex => activeItemIndex;
    public int Money => money;

    public override void OnStartServer()
    {
        // Инициализируем SyncList на сервере
        // Размер должен соответствовать шаблону
        if (inventorySlotsTemplate != null)
        {
            for (int i = 0; i < inventorySlotsTemplate.Length; i++)
            {
                 // Добавляем пустые строки для каждого слота
                 syncInventorySlotNames.Add("");
            }
        }
        // Начальная синхронизация не требуется, так как SyncList синхронизируется автоматически
    }

    public override void OnStartClient()
    {
        // Инициализируем локальный кэш на клиенте
        if (inventorySlotsTemplate != null)
        {
             inventorySlots = new BaseItem[inventorySlotsTemplate.Length];
        }
        else
        {
             inventorySlots = new BaseItem[0]; // Или определите размер по умолчанию
        }
        
        // Подписываемся на изменения SyncList
        syncInventorySlotNames.Callback += OnSyncListChanged;

        // Первоначальное заполнение локального кэша
        DeserializeInventoryFromSyncList();
        
        if (isLocalPlayer)
        {
            UpdateActiveItem();
            UpdateMoneyDisplay();
        }
    }

    // Обработчик изменений в SyncList
    private void OnSyncListChanged(SyncList<string>.Operation op, int index, string oldItem, string newItem)
    {
        // Этот метод вызывается на клиенте при изменении syncInventorySlotNames
        // OP_SET возникает при прямом присваивании по индексу: syncInventorySlotNames[i] = "itemName";
        if (op == SyncList<string>.Operation.OP_SET) 
        {
            // Обновляем локальный кэш для измененного слота
            inventorySlots[index] = string.IsNullOrEmpty(newItem) ? null : FindItemByName(newItem);
            // Если это локальный игрок, обновляем UI
            if (isLocalPlayer)
            {
                // Обновляем конкретный слот или весь UI
                UIManager.Instance?.UpdateInventoryUI(inventorySlots, activeItemIndex);
            }
        }
        // OP_DIRTY обычно не используется напрямую в callback, 
        // изменения через OP_SET уже помечают элемент как "грязный" и синхронизируют его.
    }


    private void OnMoneyChanged(int oldAmount, int newAmount)
    {
        if (isLocalPlayer)
        {
            UpdateMoneyDisplay();
        }
    }

    // Эта часть, вероятно, больше не нужна, если мы не используем LastMoneyTakeTime для UI
    // private void OnLastMoneyTakeTimeChanged(float oldAmount, float newAmount)
    // {
    //     if (isLocalPlayer)
    //     {
    //         UpdateMoneyDisplay();
    //     }
    // }

    // Хук изменения инвентаря больше не нужен, так как мы используем SyncList
    // private void OnInventoryChanged(string oldData, string newData)
    // {
    //     if (!isServer)
    //     {
    //         DeserializeInventory();
    //         if (isLocalPlayer)
    //         {
    //             UpdateActiveItem();
    //         }
    //     }
    // }

    private void Update()
    {
        if (!isLocalPlayer) return;
        HandleHotkeyInput();
        HandleUseItemInput();
    }

    private void HandleHotkeyInput()
    {
        for (int i = 0; i < hotkeys.Length && i < (inventorySlots?.Length ?? 0); i++)
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
        if (index < 0 || index >= (inventorySlots?.Length ?? 0))
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

    // Добавляет предмет в первый свободный слот
    [Server] 
    public bool AddItem(BaseItem item)
    {
        if (!item) return false;
        
        // Найдем первый пустой слот в syncInventorySlotNames
        for (int i = 0; i < syncInventorySlotNames.Count; i++) 
        {
            if (string.IsNullOrEmpty(syncInventorySlotNames[i])) 
            {
                // Устанавливаем имя предмета в SyncList
                syncInventorySlotNames[i] = item.itemName;
                // SyncList автоматически синхронизирует это изменение с клиентами
                // Обработчик OnSyncListChanged на клиентах обновит их inventorySlots
                return true;
            }
        }
        return false; // Инвентарь полон
    }

    // Удаляет предмет из указанного слота
    [Server]
    public bool RemoveItem(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= syncInventorySlotNames.Count)
        {
            return false;
        }
        if (string.IsNullOrEmpty(syncInventorySlotNames[slotIndex]))
        {
             return false; // Слот уже пуст
        }
        // Очищаем слот в SyncList
        syncInventorySlotNames[slotIndex] = "";
        // SyncList автоматически синхронизирует это изменение с клиентами
        // Обработчик OnSyncListChanged на клиентах обновит их inventorySlots
        return true;
    }

    // Вызывается, когда слот изменяется (например, извне)
    // Может быть полезно, если UI напрямую изменяет слоты
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

    // Команда для добавления предмета
    [Command]
    public void CmdAddItem(BaseItem item)
    {
        // Логика добавления на сервере
        AddItem(item);
        // SyncList автоматически синхронизирует изменения с клиентами
    }

    // Команда для использования предмета
    [Command]
    private void CmdUseItem(int slotIndex)
    {
        // Проверяем корректность индекса и наличия предмета в SyncList
        if (slotIndex < 0 || slotIndex >= syncInventorySlotNames.Count || 
            string.IsNullOrEmpty(syncInventorySlotNames[slotIndex]))
            return;

        string itemName = syncInventorySlotNames[slotIndex];
        BaseItem item = FindItemByName(itemName); // Получаем объект BaseItem по имени
        
        if (item != null && item.CanUse())
        {
            item.Use(this); // Используем предмет
            
            // После использования удаляем его из инвентаря
            syncInventorySlotNames[slotIndex] = ""; 
            // SyncList автоматически синхронизирует это изменение с клиентами
            // Обработчик OnSyncListChanged на клиентах обновит их inventorySlots
        }
    }

    // Метод для десериализации имен из SyncList в локальный кэш inventorySlots
    // Вызывается при подключении клиента и при изменениях SyncList
    private void DeserializeInventoryFromSyncList()
    {
        for (int i = 0; i < syncInventorySlotNames.Count && i < inventorySlots.Length; i++)
        {
             string itemName = syncInventorySlotNames[i];
             inventorySlots[i] = string.IsNullOrEmpty(itemName) ? null : FindItemByName(itemName);
        }
    }

    // Метод для поиска предмета по имени (как и было)
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
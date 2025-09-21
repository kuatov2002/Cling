using System;
using System.Collections;
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
    
    [SerializeField] private float moneyTakeInterval = 10f;
    
    [SyncVar] private float _lastMoneyTakeTime = -Mathf.Infinity;
    public override void OnStartServer()
    {
        // Инициализируем SyncList на сервере
        // Размер должен соответствовать шаблону
        if (inventorySlotsTemplate != null)
        {
            foreach (var item in inventorySlotsTemplate)
            {
                // Добавляем пустые строки для каждого слота
                syncInventorySlotNames.Add(item ? item.itemName : "");
            }
        }
        
        StartCoroutine(MoneyTakeRoutine());
        // Начальная синхронизация не требуется, так как SyncList синхронизируется автоматически
    }

    public override void OnStartClient()
    {
        // Инициализируем локальный кэш на клиенте
        inventorySlots = inventorySlotsTemplate != null ? new BaseItem[inventorySlotsTemplate.Length] : Array.Empty<BaseItem>(); // Или определите размер по умолчанию

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

    private void OnSyncListChanged(SyncList<string>.Operation op, int index, string oldItem, string newItem)
    {
        switch (op)
        {
            case SyncList<string>.Operation.OP_SET:
                UpdateSlot(index, newItem);
                break;
            case SyncList<string>.Operation.OP_ADD:
            case SyncList<string>.Operation.OP_INSERT:
                // Пересоздать весь кэш, так как размер изменился
                DeserializeInventoryFromSyncList();
                break;
            case SyncList<string>.Operation.OP_REMOVEAT:
            case SyncList<string>.Operation.OP_CLEAR:
                DeserializeInventoryFromSyncList();
                break;
        }

        if (isLocalPlayer)
        {
            UIManager.Instance?.UpdateInventoryUI(inventorySlots, activeItemIndex);
        }
    }

    private void UpdateSlot(int index, string itemName)
    {
        if (index < inventorySlots.Length)
        {
            inventorySlots[index] = string.IsNullOrEmpty(itemName) ? null : FindItemByName(itemName);
        }
    }
    
    private void OnMoneyChanged(int oldAmount, int newAmount)
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

    private void UseActiveItem()
    {
        // Только отправляем команду, если слот валиден
        if (activeItemIndex >= 0 && activeItemIndex < syncInventorySlotNames.Count)
        {
            CmdUseItem(activeItemIndex);
        }
    }

    [Command]
    private void CmdUseItem(int slotIndex)
    {
        if (!IsSlotIndexValid(slotIndex)) return;

        string itemName = syncInventorySlotNames[slotIndex];
        if (string.IsNullOrEmpty(itemName)) return;

        BaseItem item = FindItemByName(itemName);
        if (item == null) 
        {
            Debug.LogWarning($"Player {connectionToClient} tried to use non-existent item: {itemName}");
            return; 
        }

        if (item.CanUse())
        {
            item.Use(this);
            RemoveItem(slotIndex);
        }
    }

    private bool IsSlotIndexValid(int index) => 
        index >= 0 && index < syncInventorySlotNames.Count;

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
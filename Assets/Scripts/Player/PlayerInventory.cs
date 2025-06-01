using Mirror;
using UnityEngine;

namespace RedstoneinventeGameStudio
{
    public class PlayerInventory : NetworkBehaviour
    {
        [Header("Inventory Configuration")]
        [SerializeField] private InventoryItemData[] inventorySlots;

        [Header("Active Item Display")]
        [SerializeField] private int activeItemIndex = 0;

        [Header("Input")]
        [SerializeField] private KeyCode[] hotkeys = {
            KeyCode.Alpha1, KeyCode.Alpha2, KeyCode.Alpha3,
            KeyCode.Alpha4, KeyCode.Alpha5
        };

        public InventoryItemData CurrentActiveSlot => 
            inventorySlots != null && activeItemIndex < inventorySlots.Length 
                ? inventorySlots[activeItemIndex] 
                : null;

        public InventoryItemData CurrentActiveItem => 
            CurrentActiveSlot;

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
            UIManager.Instance?.UpdateInventoryUI(inventorySlots);
        }

        public bool AddItem(InventoryItemData item)
        {
            if (item == null) return false;

            for (int i = 0; i < inventorySlots.Length; i++)
            {
                if (inventorySlots[i] != null)
                {
                    bool success = inventorySlots[i];
                    if (success)
                    {
                        if (i == activeItemIndex)
                        {
                            UpdateActiveItem();
                        }
                        UIManager.Instance?.UpdateInventoryUI(inventorySlots);
                        return true;
                    }
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

            inventorySlots[slotIndex]=null;
            
            if (slotIndex == activeItemIndex)
            {
                UpdateActiveItem();
            }
            
            UIManager.Instance?.UpdateInventoryUI(inventorySlots);
            return true;
        }

        protected virtual void OnActiveItemChanged()
        {
            string itemName = CurrentActiveItem.itemName ?? "None";
            Debug.Log($"Active item changed to: {itemName} (Index: {activeItemIndex})");
        }

        public void OnInventorySlotChanged(int slotIndex)
        {
            if (slotIndex == activeItemIndex)
            {
                UpdateActiveItem();
            }
            else
            {
                UIManager.Instance.UpdateInventoryUI(inventorySlots);
            }
        }

        // Network synchronization methods
        [ClientRpc]
        public void RpcSyncInventory(InventoryItemData[] items)
        {
            if (inventorySlots == null) return;

            for (int i = 0; i < Mathf.Min(items.Length, inventorySlots.Length); i++)
            {
                if (inventorySlots[i] != null)
                {
                    if (items[i] != null)
                    {
                        inventorySlots[i] = items[i];
                    }
                    else
                    {
                        inventorySlots[i] = null;
                    }
                }
            }
            
            UpdateActiveItem();
        }

        [Command]
        public void CmdAddItem(InventoryItemData item)
        {
            if (AddItem(item))
            {
                // Sync to all clients
                var items = new InventoryItemData[inventorySlots.Length];
                for (int i = 0; i < inventorySlots.Length; i++)
                {
                    items[i] = inventorySlots[i];
                }
                RpcSyncInventory(items);
            }
        }
    }
}
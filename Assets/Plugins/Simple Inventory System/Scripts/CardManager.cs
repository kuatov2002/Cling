using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace RedstoneinventeGameStudio
{
    public class CardManager : MonoBehaviour, IPointerDownHandler, IPointerEnterHandler, IPointerExitHandler
    {
#nullable enable
        public InventoryItemData? itemData;
        public bool isOccupied;
#nullable disable

        [SerializeField] bool useAsDrag;
        [SerializeField] GameObject emptyCard;

        [SerializeField] TMP_Text itemName;
        [SerializeField] Image itemIcon;

        private void Awake()
        {
            if (useAsDrag)
            {
                ItemDraggingManager.dragCard = this;
                isOccupied = true;

                gameObject.SetActive(false);
            }

            if (itemData == null)
            {
                RefreshDisplay();
            }
            else
            {
                SetItem(itemData);
            }
        }

        void IPointerDownHandler.OnPointerDown(PointerEventData eventData)
        {
            if (useAsDrag || !isOccupied)
            {
                return;
            }

            ItemDraggingManager.fromCard = this;
            TooltipManagerInventory.UnSetToolTip();
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (isOccupied)
            {
                ItemDraggingManager.toCard = ItemDraggingManager.fromCard;

                if (ItemDraggingManager.toCard == default)
                {
                    TooltipManagerInventory.SetTooltip(itemData);
                }

                return;
            }

            ItemDraggingManager.toCard = this;
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (!isOccupied)
            {
                return;
            }

            TooltipManagerInventory.UnSetToolTip();
        }

        public bool SetItem(InventoryItemData itemData)
        {
            if ((isOccupied && !useAsDrag) || itemData == null)
            {
                return false;
            }

            this.itemData = itemData;
            this.itemName.text = itemData.name;
            this.itemIcon.sprite = itemData.itemIcon;

            this.isOccupied = true;

            RefreshDisplay();

            return true;
        }

        public void UnSetItem()
        {
            itemData = null;
            this.isOccupied = false;

            RefreshDisplay();
        }

        void RefreshDisplay()
        {
            emptyCard.SetActive(!isOccupied);
        }
    }

}
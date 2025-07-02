using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

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

    private Outline outline;

    private void Awake()
    {
        if (useAsDrag && !ItemDraggingManager.dragCard)
        {
            ItemDraggingManager.dragCard = this;
            isOccupied = true;

            gameObject.SetActive(false);
        }
        
        
        outline = GetComponent<Outline>();
        if (outline)
        {
            outline.enabled = false;
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

            if (!ItemDraggingManager.toCard)
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

    public bool SetItem(string itemName, Sprite itemIcon)
    {
        if ((isOccupied && !useAsDrag) || !itemData)
        {
            return false;
        }

        this.itemName.text = itemName;
        this.itemIcon.sprite = itemIcon;

        isOccupied = true;

        RefreshDisplay();

        return true;
    }

    public void UnSetItem()
    {
        itemData = null;
        this.isOccupied = false;

        RefreshDisplay();
    }

    public void SetActive(bool isActive)
    {
        if (outline)
        {
            outline.enabled = isActive;
        }
    }

    void RefreshDisplay()
    {
        emptyCard.SetActive(!isOccupied);
    }
}
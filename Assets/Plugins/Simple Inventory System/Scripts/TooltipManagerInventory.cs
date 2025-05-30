using TMPro;
using Unity.VisualScripting;
using UnityEngine;

namespace RedstoneinventeGameStudio
{
    public class TooltipManagerInventory : MonoBehaviour
    {
        public static TooltipManagerInventory instance;

        [SerializeField] TMP_Text tooltip;
        [SerializeField] TMP_Text desc;

        public void Awake()
        {
            instance = this;
            gameObject.SetActive(false);
        }

        public static void SetTooltip(InventoryItemData inventoryItemData)
        {
            instance.gameObject.SetActive(true);
            instance.tooltip.text = inventoryItemData.itemTooltip;
            instance.desc.text = inventoryItemData.itemDescription;
        }

        public static void UnSetToolTip()
        {
            instance.gameObject.SetActive(false);
        }
    }
}
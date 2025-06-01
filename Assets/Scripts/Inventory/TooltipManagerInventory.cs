using TMPro;
using Unity.VisualScripting;
using UnityEngine;

namespace RedstoneinventeGameStudio
{
    public class TooltipManagerInventory : MonoBehaviour
    {
        public static TooltipManagerInventory Instance;

        [SerializeField] TMP_Text tooltip;
        [SerializeField] TMP_Text desc;

        public void Awake()
        {
            if (Instance==null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
                gameObject.SetActive(false);
            }
            else
            {
                Destroy(gameObject);
            }

        }

        public static void SetTooltip(InventoryItemData inventoryItemData)
        {
            Instance.gameObject.SetActive(true);
            Instance.tooltip.text = inventoryItemData.itemTooltip;
            Instance.desc.text = inventoryItemData.itemDescription;
        }

        public static void UnSetToolTip()
        {
            Instance.gameObject.SetActive(false);
        }
    }
}
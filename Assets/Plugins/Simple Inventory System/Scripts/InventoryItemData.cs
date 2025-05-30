using UnityEngine;

namespace RedstoneinventeGameStudio
{
    [CreateAssetMenu(fileName = "Inventory Item", menuName = "Inventory Item")]
    public class InventoryItemData : ScriptableObject
    {
        public string itemName;
        public string itemDescription;

        public string itemTooltip;
        public Sprite itemIcon;
    }
}
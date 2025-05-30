using UnityEngine;

namespace RedstoneinventeGameStudio
{
    public class ItemDraggingManager : MonoBehaviour
    {
        public static CardManager dragCard;

        public static CardManager fromCard;
        public static CardManager toCard;

        [SerializeField] Vector3 tooltipOffset;
        [SerializeField] Vector3 draggingCardOffset;

        private void Update()
        {
            if (Input.GetKeyUp(KeyCode.Mouse0) && fromCard != default)
            {
                if (toCard != default)
                {
                    toCard.SetItem(dragCard.itemData);
                }
                else if (fromCard != default)
                {
                    fromCard.SetItem(dragCard.itemData);
                }

                toCard = default;
                fromCard = default;

                dragCard.gameObject.SetActive(false);
            }

            if (Input.GetKeyDown(KeyCode.Mouse0) && fromCard != default)
            {
                dragCard.SetItem(fromCard.itemData);
                fromCard.UnSetItem();

                dragCard.gameObject.SetActive(true);
            }

            dragCard.transform.position = Input.mousePosition + draggingCardOffset;
            TooltipManagerInventory.instance.transform.position = Input.mousePosition + tooltipOffset;
        }
    }

}
using System;
using UnityEngine;


    public class ItemDraggingManager : MonoBehaviour
    {
        public static ItemDraggingManager Instance;
        
        public static CardManager dragCard;

        public static CardManager fromCard;
        public static CardManager toCard;

        [SerializeField] Vector3 tooltipOffset;
        [SerializeField] Vector3 draggingCardOffset;


        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void Update()
        {
            if (dragCard == null) return;

            if (Input.GetKeyUp(KeyCode.Mouse0) && fromCard != null)
            {
                if (toCard != null)
                {
                    toCard.SetItem(dragCard.itemData.itemName, dragCard.itemData.itemIcon);
                }
                else
                {
                    fromCard.SetItem(dragCard.itemData.itemName, dragCard.itemData.itemIcon);
                }

                toCard = null;
                fromCard = null;

                dragCard.gameObject.SetActive(false);
            }

            if (Input.GetKeyDown(KeyCode.Mouse0) && fromCard != null)
            {
                dragCard.SetItem(fromCard.itemData.itemName, fromCard.itemData.itemIcon);
                fromCard.UnSetItem();

                dragCard.gameObject.SetActive(true);
            }

            dragCard.transform.position = Input.mousePosition + draggingCardOffset;
            if (TooltipManagerInventory.Instance != null)
                TooltipManagerInventory.Instance.transform.position = Input.mousePosition + tooltipOffset;
        }
    }
    
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
            if (Input.GetKeyUp(KeyCode.Mouse0) && fromCard != default)
            {
                if (toCard != default)
                {
                    toCard.SetItem(dragCard.itemData.itemName, dragCard.itemData.itemIcon);
                }
                else if (fromCard != default)
                {
                    fromCard.SetItem(dragCard.itemData.itemName, dragCard.itemData.itemIcon);
                }

                toCard = default;
                fromCard = default;

                dragCard.gameObject.SetActive(false);
            }

            if (Input.GetKeyDown(KeyCode.Mouse0) && fromCard != default)
            {
                dragCard.SetItem(fromCard.itemData.itemName, fromCard.itemData.itemIcon);
                fromCard.UnSetItem();

                dragCard.gameObject.SetActive(true);
            }

            dragCard.transform.position = Input.mousePosition + draggingCardOffset;
            TooltipManagerInventory.Instance.transform.position = Input.mousePosition + tooltipOffset;
        }
    }
    
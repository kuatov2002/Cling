using System.Collections.Generic;
using Mirror;
using UnityEngine;

public class ItemShop : NetworkBehaviour, IInteractable
{
    [SerializeField] private int itemCost = 3;
    [SerializeField] private List<BaseItem> availableItems = new();
    
    public string InteractText => $"Press F to buy random item ({itemCost}$)";
    
    private PlayerInventory _playerInventory;

    public void Interact()
    {
        if (_playerInventory && CanPurchaseItem())
        {
            CmdPurchaseItem();
        }
    }

    [Command(requiresAuthority = false)]
    private void CmdPurchaseItem(NetworkConnectionToClient sender = null)
    {
        var playerInventory = sender.identity.GetComponent<PlayerInventory>();
        if (!playerInventory || !CanPurchaseItem(playerInventory)) return;

        if (playerInventory.SpendMoney(itemCost))
        {
            BaseItem randomItem = GetRandomItem();
            if (randomItem)
            {
                playerInventory.AddItem(randomItem);
            }
        }
    }

    private bool CanPurchaseItem(PlayerInventory inventory = null)
    {
        var targetInventory = inventory ?? _playerInventory;
        return targetInventory && 
               targetInventory.Money >= itemCost && 
               availableItems.Count > 0;
    }

    private BaseItem GetRandomItem()
    {
        if (availableItems.Count == 0) return null;
        
        int randomIndex = Random.Range(0, availableItems.Count);
        return availableItems[randomIndex];
    }

    private void OnTriggerEnter(Collider other)
    {
        var networkIdentity = other.GetComponent<NetworkIdentity>();
        if (networkIdentity && networkIdentity.isLocalPlayer)
        {
            _playerInventory = other.GetComponent<PlayerInventory>();
            UIManager.Instance.UpdateInteractText(InteractText);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        var networkIdentity = other.GetComponent<NetworkIdentity>();
        if (networkIdentity && networkIdentity.isLocalPlayer)
        {
            _playerInventory = null;
            
            if (UIManager.Instance.GetInteractText() == InteractText)
            {
                UIManager.Instance.UpdateInteractText(string.Empty);
            }
        }
    }

    private void Update()
    {
        if (_playerInventory && Input.GetKeyDown(KeyCode.F))
        {
            Interact();
        }
    }

    private void Start()
    {
        ValidateConfiguration();
    }

    private void ValidateConfiguration()
    {
        var collider = GetComponent<Collider>();
        if (!collider)
        {
            Debug.LogWarning($"ItemShop {name} requires a Collider component set as trigger");
        }
        else if (!collider.isTrigger)
        {
            Debug.LogWarning($"ItemShop {name} Collider should be set as trigger");
        }

        if (availableItems.Count == 0)
        {
            Debug.LogWarning($"ItemShop {name} has no available items configured");
        }
    }
}

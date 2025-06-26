using Mirror;
using UnityEngine;

public class AmmoShop : NetworkBehaviour, IInteractable
{
    [SerializeField] private int ammoCost = 1;
    
    public string InteractText => $"Press F to buy ammo ({ammoCost}$)";
    
    // Public property to access ammo cost
    public int AmmoCost => ammoCost;
    
    private Gun _playerGun;

    public void Interact()
    {
        if (_playerGun && _playerGun.CanAddAmmo())
        {
            _playerGun.CmdAddAmmo();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        var networkIdentity = other.GetComponent<NetworkIdentity>();
        if (networkIdentity && networkIdentity.isLocalPlayer)
        {
            _playerGun = other.GetComponent<Gun>();
            // Set this shop as current ammo shop for the gun
            if (_playerGun)
            {
                _playerGun.SetCurrentAmmoShop(this);
            }
        }
        
        UIManager.Instance.UpdateInteractText(InteractText);
    }

    private void OnTriggerExit(Collider other)
    {
        var networkIdentity = other.GetComponent<NetworkIdentity>();
        if (networkIdentity && networkIdentity.isLocalPlayer)
        {
            // Clear the current ammo shop reference from gun
            if (_playerGun)
            {
                _playerGun.ClearCurrentAmmoShop();
            }

            _playerGun = null;
        }
        
        if (UIManager.Instance.GetInteractText() == InteractText)
            UIManager.Instance.UpdateInteractText(string.Empty);
    }

    private void Update()
    {
        if (_playerGun && Input.GetKeyDown(KeyCode.F))
        {
            Interact();
        }
    }

    private void Start()
    {
        var collider = GetComponent<Collider>();
        if (!collider)
        {
            Debug.LogWarning($"AmmoShop {name} requires a Collider component set as trigger");
        }
        else if (!collider.isTrigger)
        {
            Debug.LogWarning($"AmmoShop {name} Collider should be set as trigger");
        }
    }
}
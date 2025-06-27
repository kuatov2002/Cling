using Mirror;
using UnityEngine;

public class AmmoShop : NetworkBehaviour, IInteractable
{
    [SerializeField] private int ammoCost = 1;
    
    public string InteractText => $"Press F to buy ammo ({ammoCost}$)";
    
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
            if (_playerGun)
            {
                _playerGun.SetCurrentAmmoShop(this);
            }
            
            // Only update UI for local player
            UIManager.Instance.UpdateInteractText(InteractText);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        var networkIdentity = other.GetComponent<NetworkIdentity>();
        if (networkIdentity && networkIdentity.isLocalPlayer)
        {
            if (_playerGun)
            {
                _playerGun.ClearCurrentAmmoShop();
            }

            _playerGun = null;
            
            // Only clear UI for local player
            if (UIManager.Instance.GetInteractText() == InteractText)
            {
                UIManager.Instance.UpdateInteractText(string.Empty);
            }
        }
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
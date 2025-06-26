using Mirror;
using UnityEngine;

public class AmmoShop : NetworkBehaviour, IInteractable
{
    [SerializeField] private int ammoCost = 1;
    
    public string InteractText => $"Press F to buy ammo ({ammoCost}$)";
    
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
        }
    }

    private void OnTriggerExit(Collider other)
    {
        var networkIdentity = other.GetComponent<NetworkIdentity>();
        if (networkIdentity && networkIdentity.isLocalPlayer)
        {
            _playerGun = null;
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
using UnityEngine;

public class ExtraClip : BaseItem
{
    [SerializeField] private int ammoAmount = 3;
    
    public override void Use(PlayerInventory playerInventory = null)
    {
        if (playerInventory == null) return;
        
        Gun gun = playerInventory.GetComponent<Gun>();
        if (gun != null)
        {
            // Add ammo directly to the gun
            gun.AddAmmo(ammoAmount);
            Debug.Log($"Used {itemName} - Restored {ammoAmount} ammo");
        }
    }
}

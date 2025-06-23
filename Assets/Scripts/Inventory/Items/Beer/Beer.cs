using UnityEngine;

public class Beer : BaseItem
{
    [SerializeField] private float healAmount = 50f;
    
    public override void Use(PlayerInventory playerInventory = null)
    {
        if (playerInventory == null) return;
        
        PlayerHealth playerHealth = playerInventory.GetComponent<PlayerHealth>();
        if (playerHealth != null)
        {
            playerHealth.TakeDamage(-healAmount); // Negative damage = healing
            Debug.Log($"Used {data.itemName} - Healed for {healAmount}");
        }
    }
}
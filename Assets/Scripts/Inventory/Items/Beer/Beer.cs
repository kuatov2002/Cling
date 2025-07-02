using UnityEngine;

public class Beer : BaseItem
{
    [SerializeField] private float healAmount = 50f;
    
    public override void Use(PlayerInventory playerInventory = null)
    {
        if (!playerInventory) return;
        
        PlayerHealth playerHealth = playerInventory.GetComponent<PlayerHealth>();
        if (playerHealth)
        {
            playerHealth.TakeDamage(-healAmount); // Negative damage = healing
            Debug.Log($"Used {itemName} - Healed for {healAmount}");
        }
    }
}
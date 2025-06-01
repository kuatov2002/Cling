using UnityEngine;

public class Beer : BaseItem
{
    [SerializeField] private float healAmount = 50f;
    
    public override void Use()
    {
        if (!isServer) 
        {
            CmdUseItem();
            return;
        }
        
        PlayerHealth playerHealth = GetComponent<PlayerHealth>();
        if (playerHealth != null)
        {
            playerHealth.TakeDamage(healAmount*(-1));
            Debug.Log($"Used {data.itemName} - Healed for {healAmount}");
        }
    }
}
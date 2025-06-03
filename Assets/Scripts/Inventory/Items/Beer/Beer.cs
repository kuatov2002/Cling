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
        
        // Get the player through connection authority
        GameObject player = connectionToClient.identity.gameObject;
        PlayerHealth playerHealth = player.GetComponent<PlayerHealth>();
        
        if (playerHealth)
        {
            playerHealth.TakeDamage(-healAmount); // Negative damage = healing
            Debug.Log($"Used {data.itemName} - Healed for {healAmount}");
        }
    }
}
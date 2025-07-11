using Mirror;
using UnityEngine;

public class UncleSamInventory : PlayerInventory
{
    [Header("Uncle Sam Configuration")]
    [SerializeField, Range(0, 1)] private float cashbackChance = 0.5f;
    [SerializeField] private int cashbackMoney = 1;

    [Server]
    public override bool SpendMoney(int amount)
    {
        if (money >= amount)
        {
            money -= amount;
           
            // Check for cashback
            if (Random.value <= cashbackChance)
            {
                money += cashbackMoney;
            }
           
            return true;
        }
       
        return false;
    }
}
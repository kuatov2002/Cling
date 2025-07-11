using Mirror;
using UnityEngine;

public class CrazyJackGun : Gun
{
    [SerializeField] private float luckyChance = 0.4f;
    
    [Command]
    protected override void CmdFire(Vector3 shootDirection)
    {
        if (bulletAmount <= 0) return;
        
        LastFireTime = (float)NetworkTime.time;
        
        // Check if lucky shot (doesn't consume ammo)
        bool isLuckyShot = Random.value < luckyChance;
        
        if (!isLuckyShot)
        {
            bulletAmount--;
        }
        
        GameObject bullet = Instantiate(
            bulletPrefab, 
            gunTransform.position, 
            Quaternion.LookRotation(shootDirection)
        );
        NetworkServer.Spawn(bullet);

        Bullet bulletComponent = bullet.GetComponent<Bullet>();
        if (bulletComponent)
        {
            bulletComponent.RpcInitialize(shootDirection, damage);
        }
    }
}
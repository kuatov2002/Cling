using Mirror;
using UnityEngine;

public class AlucardGun : Gun
{
    [SerializeField] private float healAmount = 10f;
    
    [Command]
    protected override void CmdFire(Vector3 shootDirection)
    {
        if (bulletAmount <= 0) return;
        LastFireTime = (float)NetworkTime.time;
        bulletAmount--;
        GameObject bullet = Instantiate(
            bulletPrefab, 
            gunTransform.position, 
            Quaternion.LookRotation(shootDirection)
        );
        NetworkServer.Spawn(bullet);

        AlucardBullet bulletComponent = bullet.GetComponent<AlucardBullet>();
        if (bulletComponent)
        {
            bulletComponent.RpcInitialize(shootDirection, damage, netId, healAmount);
        }
    }
}
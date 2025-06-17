using Mirror;
using UnityEngine;

public class AlucardGun : Gun
{
    [SerializeField] private float healAmount = 10f;
    
    [Command]
    protected override void CmdFire(Vector3 shootDirection)
    {
        _lastFireTime = (float)NetworkTime.time;
        
        GameObject bullet = Instantiate(
            bulletPrefab, 
            gunTransform.position, 
            Quaternion.LookRotation(shootDirection)
        );
        NetworkServer.Spawn(bullet);

        AlucardBullet bulletComponent = bullet.GetComponent<AlucardBullet>();
        if (bulletComponent)
        {
            bulletComponent.Initialize(shootDirection, damage, netId, healAmount);
        }
    }
}
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

        AlucardBullet bulletComponent = bullet.GetComponent<AlucardBullet>();
        if (bulletComponent)
        {
            // Initialize BEFORE Spawn so SyncVars are included in spawn message
            bulletComponent.Initialize(shootDirection, damage, netId, healAmount);
        }

        NetworkServer.Spawn(bullet);
    }
}

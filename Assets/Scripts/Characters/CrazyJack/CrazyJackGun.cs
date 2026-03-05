using Mirror;
using UnityEngine;

public class CrazyJackGun : Gun
{
    [SerializeField] private float luckyChance = 0.4f;

    [Command]
    protected override void CmdFire(Vector3 shootDirection)
    {
        LastFireTime = (float)NetworkTime.time;

        // Lucky shot: chance to fire a double-damage bullet
        bool isLuckyShot = Random.value < luckyChance;
        float finalDamage = isLuckyShot ? damage * 2f : damage;

        GameObject bullet = Instantiate(
            bulletPrefab,
            gunTransform.position,
            Quaternion.LookRotation(shootDirection)
        );

        Bullet bulletComponent = bullet.GetComponent<Bullet>();
        if (bulletComponent)
        {
            bulletComponent.Initialize(shootDirection, finalDamage, netId);
        }

        NetworkServer.Spawn(bullet);
    }
}

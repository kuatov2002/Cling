using Mirror;
using UnityEngine;

public class AlucardBullet : Bullet
{
    [SyncVar] private float _healAmount;

    public void Initialize(Vector3 dir, float dmg, uint ownerNetId, float heal)
    {
        base.Initialize(dir, dmg, ownerNetId);
        _healAmount = heal;
    }

    [Server]
    protected override void ApplyDamage(IDamageable target)
    {
        if (target != null)
        {
            // Apply damage with kill attribution
            if (target is PlayerHealth playerHealth)
            {
                playerHealth.TakeDamageFrom(_damage, _ownerNetId);
            }
            else
            {
                target.TakeDamage(_damage);
            }

            // Heal the owner
            if (NetworkServer.spawned.TryGetValue(_ownerNetId, out NetworkIdentity ownerIdentity))
            {
                var ownerHealth = ownerIdentity.GetComponent<IHealable>();
                ownerHealth?.Heal(_healAmount);
            }
        }

        DestroySelf();
    }
}

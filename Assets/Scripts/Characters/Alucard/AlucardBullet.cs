using Mirror;
using UnityEngine;

public class AlucardBullet : Bullet
{
    private uint _ownerNetId;
    private float _healAmount;
    
    
    [ClientRpc]
    public void RpcInitialize(Vector3 dir, float dmg, uint ownerNetId, float heal)
    {
        _direction = dir.normalized;
        _damage = dmg;
        _ownerNetId = ownerNetId;
        _healAmount = heal;
    }
    
    [Server]
    protected override void ApplyDamage(IDamageable target)
    {
        if (target != null)
        {
            target.TakeDamage(_damage);
                    
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
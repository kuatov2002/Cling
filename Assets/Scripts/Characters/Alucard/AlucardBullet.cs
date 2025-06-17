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
    
    private void Update()
    {
        float moveDistance = speed * Time.deltaTime;
        Vector3 start = transform.position;
        Vector3 end = start + _direction * moveDistance;

        if (isServer)
        {
            if (Physics.Raycast(start, _direction, out RaycastHit hit, moveDistance))
            {
                var target = hit.collider.GetComponent<IDamageable>();
                if (target != null)
                {
                    target.TakeDamage(_damage);
                    
                    // Heal the owner
                    if (NetworkServer.spawned.TryGetValue(_ownerNetId, out NetworkIdentity ownerIdentity))
                    {
                        var ownerHealth = ownerIdentity.GetComponent<IDamageable>();
                        ownerHealth?.TakeDamage(-_healAmount);
                    }
                }

                DestroySelf();
                return;
            }
        }

        transform.position = end;
    }
}
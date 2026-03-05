using Mirror;
using UnityEngine;

/// <summary>
/// DEPRECATED: Projectile system replaced by hitscan in Gun.cs.
/// Kept as stub to avoid missing-script errors on existing bullet prefabs.
/// Existing bullet prefabs are no longer spawned by the gun system.
/// Safe to remove this class and its prefabs once all references are cleaned up.
/// </summary>
public class Bullet : NetworkBehaviour
{
    protected float speed = 900f;
    protected float lifeTime = 3f;
    protected float _damage;
    protected uint _ownerNetId;
    [SerializeField] protected ParticleSystem hitEffectPrefab;

    public virtual void Initialize(Vector3 dir, float dmg, uint ownerNetId = 0)
    {
        _damage = dmg;
        _ownerNetId = ownerNetId;
    }

    public override void OnStartServer()
    {
        // Auto-destroy after lifetime (in case this is accidentally spawned)
        Invoke(nameof(DestroySelf), lifeTime);
    }

    [Server]
    protected void DestroySelf()
    {
        NetworkServer.Destroy(gameObject);
    }
}

using Mirror;
using UnityEngine;

public class Bullet : NetworkBehaviour
{
    protected float speed = 900f;
    protected float lifeTime = 3f;

    protected float _damage;
    protected uint _ownerNetId;

    [SerializeField] protected ParticleSystem hitEffectPrefab;

    public override void OnStartServer()
    {
        Invoke(nameof(DestroySelf), lifeTime);
    }

    [Server]
    protected void DestroySelf()
    {
        NetworkServer.Destroy(gameObject);
    }

    public virtual void Initialize(Vector3 dir, float dmg, uint ownerNetId = 0)
    {
        _damage = dmg;
        _ownerNetId = ownerNetId;
    }

    protected virtual void Update()
    {
        Vector3 forward = transform.forward;
        float moveDistance = speed * Time.deltaTime;
        Vector3 start = transform.position;
        Vector3 end = start + forward * moveDistance;

        if (isServer)
        {
            if (Physics.Raycast(start, forward, out RaycastHit hit, moveDistance))
            {
                // Skip own owner's collider
                if (IsOwner(hit.collider))
                {
                    transform.position = end;
                    return;
                }

                // Skip friendly targets (no friendly fire)
                if (IsFriendlyTarget(hit.collider))
                {
                    transform.position = end;
                    return;
                }

                var target = hit.collider.GetComponent<IDamageable>();

                if (target != null)
                {
                    PlayHitEffectLocal(hit.point);
                    RpcPlayHitEffect(hit.point);
                    ApplyDamage(target);
                }

                return;
            }
        }

        transform.position = end;
    }

    [Server]
    private bool IsOwner(Collider col)
    {
        if (_ownerNetId == 0) return false;
        var identity = col.GetComponentInParent<NetworkIdentity>();
        return identity != null && identity.netId == _ownerNetId;
    }

    [Server]
    private bool IsFriendlyTarget(Collider targetCollider)
    {
        if (_ownerNetId == 0) return false;

        var targetTeam = targetCollider.GetComponent<PlayerTeam>();
        if (targetTeam == null) return false;

        Team ownerTeam = GetOwnerTeam();
        return ownerTeam != Team.None && targetTeam.CurrentTeam == ownerTeam;
    }

    [Server]
    protected Team GetOwnerTeam()
    {
        if (_ownerNetId != 0 &&
            NetworkServer.spawned.TryGetValue(_ownerNetId, out NetworkIdentity ownerIdentity))
        {
            return ownerIdentity.GetComponent<PlayerTeam>()?.CurrentTeam ?? Team.None;
        }
        return Team.None;
    }

    [Server]
    void PlayHitEffectLocal(Vector3 hitPoint)
    {
        if (hitEffectPrefab)
        {
            Instantiate(hitEffectPrefab, hitPoint, Quaternion.identity);
        }
    }

    [ClientRpc]
    protected void RpcPlayHitEffect(Vector3 hitPoint)
    {
        if (hitEffectPrefab)
        {
            Instantiate(hitEffectPrefab, hitPoint, Quaternion.identity);
        }
    }

    [Server]
    protected virtual void ApplyDamage(IDamageable target)
    {
        if (target is PlayerHealth playerHealth)
        {
            playerHealth.TakeDamageFrom(_damage, _ownerNetId);
        }
        else
        {
            target?.TakeDamage(_damage);
        }
        DestroySelf();
    }
}

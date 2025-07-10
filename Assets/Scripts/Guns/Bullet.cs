using Mirror;
using UnityEngine;

public class Bullet : NetworkBehaviour
{
    protected float speed = 900f;
    protected float lifeTime = 3f;

    protected float _damage;
    protected Vector3 _direction;
    
    [SerializeField] protected ParticleSystem hitEffectPrefab; // Префаб эффекта попадания

    public override void OnStartServer()
    {
        Invoke(nameof(DestroySelf), lifeTime);
    }

    [Server]
    protected void DestroySelf()
    {
        NetworkServer.Destroy(gameObject);
    }

    [ClientRpc]
    public void RpcInitialize(Vector3 dir, float dmg)
    {
        _direction = dir.normalized;
        _damage = dmg;
    }

    protected virtual void Update()
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
                    // Создаем эффект локально на сервере (для хоста)
                    PlayHitEffectLocal(hit.point, hit.normal);
                    // Отправляем RPC клиентам
                    RpcPlayHitEffect(hit.point, hit.normal);
                    ApplyDamage(target);
                }

                return;
            }
        }

        transform.position = end;
    }

    [Server]
    void PlayHitEffectLocal(Vector3 hitPoint, Vector3 hitNormal)
    {
        if (hitEffectPrefab)
        {
            Instantiate(hitEffectPrefab, hitPoint, Quaternion.LookRotation(hitNormal));
        }
    }

    [ClientRpc]
    protected void RpcPlayHitEffect(Vector3 hitPoint, Vector3 hitNormal)
    {
        if (hitEffectPrefab)
        {
            Instantiate(hitEffectPrefab, hitPoint, Quaternion.LookRotation(hitNormal));
        }
    }

    [Server]
    protected virtual void ApplyDamage(IDamageable target)
    {
        target?.TakeDamage(_damage);
        DestroySelf();
    }
}
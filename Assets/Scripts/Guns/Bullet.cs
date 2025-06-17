using Mirror;
using UnityEngine;

public class Bullet : NetworkBehaviour
{
    [SerializeField] protected float speed = 20f;
    [SerializeField] private float lifeTime = 3f;

    protected float _damage;
    protected Vector3 _direction;

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

        // Только сервер проверяет коллизии и наносит урон
        if (isServer)
        {
            if (Physics.Raycast(start, _direction, out RaycastHit hit, moveDistance))
            {
                var target = hit.collider.GetComponent<IDamageable>();
                ApplyDamage(target);
                return;
            }
        }

        // Все клиенты двигают пулю визуально
        transform.position = end;
    }

    [Server]
    protected virtual void ApplyDamage(IDamageable target)
    {
        target?.TakeDamage(_damage);
        DestroySelf();
    }
}
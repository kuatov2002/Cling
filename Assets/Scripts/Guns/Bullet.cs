using Mirror;
using UnityEngine;

public class Bullet : NetworkBehaviour
{
    [SerializeField] private float speed = 20f;
    [SerializeField] private float lifeTime = 3f;

    private float _damage;
    private Vector3 _direction;

    public override void OnStartServer()
    {
        Invoke(nameof(DestroySelf), lifeTime);
    }

    [Server]
    private void DestroySelf()
    {
        NetworkServer.Destroy(gameObject);
    }

    [ClientRpc]
    public void RpcInitialize(Vector3 dir, float dmg)
    {
        _direction = dir;
        _damage = dmg;
    }

    private void Update()
    {
        transform.position += _direction * (speed * Time.deltaTime);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!isServer) return;
        var target = other.GetComponent<IDamageable>();
        target?.TakeDamage(_damage);
        NetworkServer.Destroy(gameObject);
    }
}
using Mirror;
using UnityEngine;

public class Gun : NetworkBehaviour
{
    [SerializeField] private GameObject bulletPrefab;
    [SerializeField] private float damage = 20f;
    [SerializeField] private float cooldown = 0.5f;
    
    private float _lastFireTime = 0f;

    [Command]
    private void CmdFire()
    {
        // Spawn bullet on server
        GameObject bullet = Instantiate(bulletPrefab, transform.position, transform.rotation);
        NetworkServer.Spawn(bullet);

        // Initialize bullet with direction/damage via its RPC
        Bullet bulletComponent = bullet.GetComponent<Bullet>();
        if (bulletComponent != null)
        {
            bulletComponent.RpcInitialize(transform.forward, damage);
        }
    }

    [Client]
    public void Fire()
    {
        if (Time.time - _lastFireTime < cooldown) return;
        _lastFireTime = Time.time;
        CmdFire();
    }
}
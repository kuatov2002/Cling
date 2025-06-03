using Mirror;
using Unity.Cinemachine;
using UnityEngine;

public class Gun : NetworkBehaviour
{
    [SerializeField] private GameObject bulletPrefab;
    [SerializeField] private float damage = 20f;
    [SerializeField] private float cooldown = 0.5f;
    [SerializeField] private LineRenderer bulletTrajectory;
    
    private float _lastFireTime = 0f;
    private bool isCharged = false;

    public override void OnStartLocalPlayer()
    {
        bulletTrajectory.enabled = false;
    }
    
    public void Charge()
    {
        isCharged = true;
        bulletTrajectory.enabled = true;
    }
    
    [Client]
    public void Fire()
    {
        if (Time.time - _lastFireTime < cooldown) return;
        _lastFireTime = Time.time;

        // Берём направление «вперёд» от камеры...
        Vector3 rawDir = transform.forward;

        // …обнуляем наклон по оси Y (вертикальную составляющую) 
        // чтобы поворот по X (pitch) был как бы 0
        Vector3 flatDir = new Vector3(rawDir.x, 0f, rawDir.z).normalized;

        // Отправляем уже «плоское» направление на сервер
        CmdFire(flatDir);
        
        isCharged = false;
        bulletTrajectory.enabled = false;
    }

    [Command]
    private void CmdFire(Vector3 shootDirection)
    {
        // Создаём пулю на сервере, вращаем её так, 
        // чтобы она смотрела вдоль нашего «плоского» направления
        GameObject bullet = Instantiate(
            bulletPrefab, 
            transform.position, 
            Quaternion.LookRotation(shootDirection)
        );
        NetworkServer.Spawn(bullet);

        Bullet bulletComponent = bullet.GetComponent<Bullet>();
        if (bulletComponent != null)
        {
            bulletComponent.RpcInitialize(shootDirection, damage);
        }
    }
}
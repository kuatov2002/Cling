using System;
using Mirror;
using Unity.Cinemachine;
using UnityEngine;

public class Gun : NetworkBehaviour
{
    [SerializeField] private GameObject bulletPrefab;
    [SerializeField] private float damage = 20f;
    [SerializeField] private float cooldown = 0.5f;
    [SerializeField] private LineRenderer bulletTrajectory;
    
    private float _lastFireTime = -Mathf.Infinity;
    private bool isCharged = false;

    private void LateUpdate()
    {
        // Вычисляем прогресс кулдауна от 0 до 1
        float timeSinceLast = Time.time - _lastFireTime;
        float cooldownProgress = Mathf.Clamp01(timeSinceLast / cooldown);
        
        // Передаём прогресс в UI: 0 — только что выстрелили, 1 — кулдаун окончен
        UIManager.Instance.UpdateGunCooldown(cooldownProgress);
    }

    public override void OnStartLocalPlayer()
    {
        bulletTrajectory.enabled = false;
    }
    
    public void Charge()
    {
        if (Time.time - _lastFireTime < cooldown) return;
        isCharged = true;
        bulletTrajectory.enabled = true;
    }
    
    [Client]
    public void Fire()
    {
        if (!isCharged) return;
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
using Mirror;
using UnityEngine;

public class Gun : NetworkBehaviour
{
    [SerializeField] private GameObject bulletPrefab;
    [SerializeField] private float damage = 20f;
    [SerializeField] private float cooldown = 0.5f;
    
    [SyncVar] private float _lastFireTime = -Mathf.Infinity;
    private bool isCharged = false;
    private Camera playerCamera;

    private void LateUpdate()
    {
        // Обновляем UI только для локального игрока
        if (!isLocalPlayer) return;
        
        float timeSinceLast = Time.time - _lastFireTime;
        float cooldownProgress = Mathf.Clamp01(timeSinceLast / cooldown);
        UIManager.Instance.UpdateGunCooldown(cooldownProgress);
    }

    public override void OnStartLocalPlayer()
    {
        playerCamera = Camera.main;
    }
    
    public bool Charge()
    {
        if (Time.time - _lastFireTime < cooldown) return false;
        isCharged = true;
        return true;
    }
    
    [Client]
    public bool Fire()
    {
        if (!isCharged) return false;
        
        Vector3 shootDirection = GetShootDirection();
        CmdFire(shootDirection);
        
        isCharged = false;
        return true;
    }

    private Vector3 GetShootDirection()
    {
        if (playerCamera == null)
            playerCamera = Camera.main;

        Ray ray = playerCamera.ScreenPointToRay(new Vector3(Screen.width / 2f, Screen.height / 2f, 0f));
        
        Vector3 targetPoint;
        if (Physics.Raycast(ray, out RaycastHit hit, 1000f))
        {
            targetPoint = hit.point;
        }
        else
        {
            targetPoint = ray.origin + ray.direction * 1000f;
        }
        
        Vector3 direction = (targetPoint - transform.position).normalized;
        return direction;
    }

    [Command]
    private void CmdFire(Vector3 shootDirection)
    {
        // Обновляем время выстрела на сервере
        _lastFireTime = Time.time;
        
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
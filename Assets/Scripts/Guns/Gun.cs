using Mirror;
using UnityEngine;

public class Gun : NetworkBehaviour
{
    [SerializeField] private GameObject bulletPrefab;
    [SerializeField] private float damage = 20f;
    [SerializeField] private float cooldown = 0.5f;
    
    [SyncVar(hook = nameof(OnLastFireTimeChanged))] 
    private float _lastFireTime = -Mathf.Infinity;
    
    private bool isCharged = false;
    private Camera playerCamera;
    private float lastReportedProgress = -1f;

    private void Start()
    {
        if (isLocalPlayer)
            StartCoroutine(CooldownUIRoutine());
    }
    
    private System.Collections.IEnumerator CooldownUIRoutine()
    {
        while (true)
        {
            float timeSinceLast = (float)NetworkTime.time - _lastFireTime;
            float cooldownProgress = Mathf.Clamp01(timeSinceLast / cooldown);

            if (Mathf.Abs(cooldownProgress - lastReportedProgress) > 0.01f)
            {
                UIManager.Instance.UpdateGunCooldown(cooldownProgress);
                lastReportedProgress = cooldownProgress;
            }
            yield return new WaitForSeconds(0.01f); // Более частое обновление
        }
    }

    public override void OnStartLocalPlayer()
    {
        playerCamera = Camera.main;
    }
    
    public bool Charge()
    {
        if ((float)NetworkTime.time - _lastFireTime < cooldown) return false;
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
        Ray ray = playerCamera.ScreenPointToRay(new Vector3(Screen.width / 2f, Screen.height / 2f, 0f));
        
        Vector3 targetPoint;
        if (Physics.Raycast(ray, out RaycastHit hit, 300f))
        {
            targetPoint = hit.point;
        }
        else
        {
            targetPoint = ray.origin + ray.direction * 300f;
        }
        
        Vector3 direction = (targetPoint - transform.position).normalized;
        return direction;
    }

    [Command]
    private void CmdFire(Vector3 shootDirection)
    {
        _lastFireTime = (float)NetworkTime.time; // Используем NetworkTime
        
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

    private void OnLastFireTimeChanged(float oldVal, float newVal)
    {
        lastReportedProgress = -1f; // Принудительное обновление UI
    }
}
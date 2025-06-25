using Mirror;
using UnityEngine;
using UnityEngine.Serialization;

public class Gun : NetworkBehaviour
{
    [SerializeField] protected GameObject bulletPrefab;
    [SerializeField] protected float damage = 20f;
    [SerializeField] private float cooldown = 0.5f;
    [SerializeField] protected Transform gunTransform;
    [SerializeField] private int maxBulletAmount = 6; // Maximum bullets for this gun
    
    [SyncVar(hook = nameof(OnLastFireTimeChanged))]
    protected float LastFireTime = -Mathf.Infinity;
    
    // Make BulletAmount a SyncVar so it synchronizes across network
    [FormerlySerializedAs("BulletAmount")]
    [SyncVar(hook = nameof(OnBulletAmountChanged))]
    [SerializeField] protected int bulletAmount = 3;
    
    private bool isCharged = false;
    private Camera playerCamera;
    private float lastReportedProgress = -1f;

    private void Start()
    {
        if (isLocalPlayer)
        {
            StartCoroutine(CooldownUIRoutine());
            // Update UI immediately when starting
            UpdateBulletUI();
        }
    }
    
    private System.Collections.IEnumerator CooldownUIRoutine()
    {
        while (true)
        {
            float timeSinceLast = (float)NetworkTime.time - LastFireTime;
            float cooldownProgress = Mathf.Clamp01(timeSinceLast / cooldown);

            if (Mathf.Abs(cooldownProgress - lastReportedProgress) > 0.01f)
            {
                UIManager.Instance.UpdateGunCooldown(cooldownProgress);
                lastReportedProgress = cooldownProgress;
            }

            yield return new WaitForSeconds(0.01f);
        }
    }

    public override void OnStartLocalPlayer()
    {
        playerCamera = Camera.main;
        // Ensure UI is updated when local player starts
        UpdateBulletUI();
    }
    
    public bool Charge()
    {
        if ((float)NetworkTime.time - LastFireTime < cooldown || bulletAmount <= 0) return false;
        isCharged = true;
        return true;
    }
    
    [Client]
    public bool Fire()
    {
        if (!isCharged || bulletAmount <= 0) return false;
        
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
        
        Vector3 direction = (targetPoint - gunTransform.position).normalized;
        return direction;
    }

    [Command]
    protected virtual void CmdFire(Vector3 shootDirection)
    {
        // Check bullet amount on server before firing
        if (bulletAmount <= 0) return;
        
        LastFireTime = (float)NetworkTime.time;
        
        // Decrease bullet count on server
        bulletAmount--;
        
        GameObject bullet = Instantiate(
            bulletPrefab, 
            gunTransform.position, 
            Quaternion.LookRotation(shootDirection)
        );
        NetworkServer.Spawn(bullet);

        Bullet bulletComponent = bullet.GetComponent<Bullet>();
        if (bulletComponent)
        {
            bulletComponent.RpcInitialize(shootDirection, damage);
        }
    }

    private void OnLastFireTimeChanged(float oldVal, float newVal)
    {
        lastReportedProgress = -1f; // Force UI update
    }
    
    // Hook method called when BulletAmount changes
    private void OnBulletAmountChanged(int oldAmount, int newAmount)
    {
        // Only update UI for local player
        if (isLocalPlayer)
        {
            UpdateBulletUI();
        }
    }
    
    // Method to update bullet count in UI
    private void UpdateBulletUI()
    {
        if (UIManager.Instance)
        {
            UIManager.Instance.UpdateBulletCount(bulletAmount, maxBulletAmount);
        }
    }
}
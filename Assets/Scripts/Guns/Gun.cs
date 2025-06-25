using Mirror;
using UnityEngine;
using UnityEngine.Serialization;

public class Gun : NetworkBehaviour
{
    [SerializeField] protected GameObject bulletPrefab;
    [SerializeField] protected float damage = 20f;
    [SerializeField] private float cooldown = 0.5f;
    [SerializeField] protected Transform gunTransform;
    [SerializeField] private int maxBulletAmount = 6;
    [SerializeField] private float reloadInterval = 2f; // Time between auto reloads
    
    [SyncVar(hook = nameof(OnLastFireTimeChanged))]
    protected float LastFireTime = -Mathf.Infinity;
    
    [SyncVar(hook = nameof(OnLastReloadTimeChanged))]
    protected float LastReloadTime = -Mathf.Infinity;
    
    [FormerlySerializedAs("BulletAmount")]
    [SyncVar(hook = nameof(OnBulletAmountChanged))]
    [SerializeField] protected int bulletAmount = 3;
    
    private bool _isCharged = false;
    private Camera _playerCamera;
    private float _lastReportedProgress = -1f;

    private void Start()
    {
        if (isLocalPlayer)
        {
            StartCoroutine(CooldownUIRoutine());
            UpdateBulletUI();
        }
        
        if (isServer)
        {
            LastReloadTime = (float)NetworkTime.time;
            StartCoroutine(AutoReloadRoutine());
        }
    }
    
    private System.Collections.IEnumerator AutoReloadRoutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(0.1f);
            
            if (bulletAmount < maxBulletAmount && 
                (float)NetworkTime.time - LastReloadTime >= reloadInterval)
            {
                bulletAmount++;
                LastReloadTime = (float)NetworkTime.time;
            }
        }
    }
    
    private System.Collections.IEnumerator CooldownUIRoutine()
    {
        while (true)
        {
            float timeSinceLast = (float)NetworkTime.time - LastFireTime;
            float cooldownProgress = Mathf.Clamp01(timeSinceLast / cooldown);

            if (Mathf.Abs(cooldownProgress - _lastReportedProgress) > 0.01f)
            {
                UIManager.Instance.UpdateGunCooldown(cooldownProgress);
                _lastReportedProgress = cooldownProgress;
            }

            yield return new WaitForSeconds(0.01f);
        }
    }

    public override void OnStartLocalPlayer()
    {
        _playerCamera = Camera.main;
        UpdateBulletUI();
    }
    
    public bool Charge()
    {
        if ((float)NetworkTime.time - LastFireTime < cooldown || bulletAmount <= 0) return false;
        _isCharged = true;
        return true;
    }
    
    [Client]
    public bool Fire()
    {
        if (!_isCharged || bulletAmount <= 0) return false;
        
        Vector3 shootDirection = GetShootDirection();
        CmdFire(shootDirection);
        _isCharged = false;
        return true;
    }

    private Vector3 GetShootDirection()
    {
        Ray ray = _playerCamera.ScreenPointToRay(new Vector3(Screen.width / 2f, Screen.height / 2f, 0f));
        
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
        if (bulletAmount <= 0) return;
        
        LastFireTime = (float)NetworkTime.time;
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
        _lastReportedProgress = -1f;
    }
    
    private void OnLastReloadTimeChanged(float oldVal, float newVal)
    {
        // Optional: Add reload feedback here
    }
    
    private void OnBulletAmountChanged(int oldAmount, int newAmount)
    {
        if (isLocalPlayer)
        {
            UpdateBulletUI();
        }
    }
    
    private void UpdateBulletUI()
    {
        if (UIManager.Instance)
        {
            UIManager.Instance.UpdateBulletCount(bulletAmount, maxBulletAmount);
        }
    }
}
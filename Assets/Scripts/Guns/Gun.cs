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
    
    [SyncVar(hook = nameof(OnLastFireTimeChanged))]
    protected float LastFireTime = -Mathf.Infinity;
    
    [FormerlySerializedAs("BulletAmount")]
    [SyncVar(hook = nameof(OnBulletAmountChanged))]
    [SerializeField] protected int bulletAmount = 1;
    
    private bool _isCharged = false;
    private Camera _playerCamera;
    private float _lastReportedProgress = -1f;
    private PlayerInventory _playerInventory;
    private AmmoShop _currentAmmoShop; // Reference to current ammo shop

    private void Start()
    {
        _playerInventory = GetComponent<PlayerInventory>();
        
        if (isLocalPlayer)
        {
            StartCoroutine(CooldownUIRoutine());
            UpdateBulletUI();
        }
    }

    // Method to set the current ammo shop (called by AmmoShop)
    public void SetCurrentAmmoShop(AmmoShop ammoShop)
    {
        _currentAmmoShop = ammoShop;
    }

    // Method to clear the current ammo shop (called by AmmoShop)
    public void ClearCurrentAmmoShop()
    {
        _currentAmmoShop = null;
    }

    [Server]
    public void AddAmmo(int amount)
    {
        int ammoToAdd = Mathf.Min(amount, maxBulletAmount - bulletAmount);
        if (ammoToAdd > 0)
        {
            bulletAmount += ammoToAdd;
            Debug.Log($"Added {ammoToAdd} ammo. Current: {bulletAmount}/{maxBulletAmount}");
        }
    }

    
    [Command]
    public void CmdAddAmmo()
    {
        if (bulletAmount >= maxBulletAmount) return;
        
        // Get price from current ammo shop
        int ammoCost = _currentAmmoShop ? _currentAmmoShop.AmmoCost : 1; // Default to 1 if no shop
        
        if (_playerInventory && _playerInventory.Money >= ammoCost)
        {
            _playerInventory.SpendMoney(ammoCost);
            bulletAmount++;
        }
    }

    public bool CanAddAmmo()
    {
        // Get price from current ammo shop
        int ammoCost = _currentAmmoShop ? _currentAmmoShop.AmmoCost : 1; // Default to 1 if no shop
        
        return bulletAmount < maxBulletAmount && 
               _playerInventory && 
               _playerInventory.Money >= ammoCost;
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

            yield return new WaitForSeconds(0.017f);
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
    
    public void CancelCharge()
    {
        _isCharged = false;
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
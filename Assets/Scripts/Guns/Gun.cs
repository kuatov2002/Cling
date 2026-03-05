using Mirror;
using UnityEngine;

public class Gun : NetworkBehaviour
{
    [SerializeField] protected GameObject bulletPrefab;
    [SerializeField] protected float damage = 20f;
    [SerializeField] private float cooldown = 0.5f;
    [SerializeField] protected Transform gunTransform;

    [SyncVar(hook = nameof(OnLastFireTimeChanged))]
    protected float LastFireTime = -Mathf.Infinity;

    private bool _isCharged = false;
    private Camera _playerCamera;
    private float _lastReportedProgress = -1f;

    private void Start()
    {
        if (isLocalPlayer)
        {
            StartCoroutine(CooldownUIRoutine());
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
                UIManager.Instance?.UpdateGunCooldown(cooldownProgress);
                _lastReportedProgress = cooldownProgress;
            }

            yield return new WaitForSeconds(0.017f);
        }
    }

    public override void OnStartLocalPlayer()
    {
        _playerCamera = Camera.main;
    }

    public bool Charge()
    {
        if ((float)NetworkTime.time - LastFireTime < cooldown) return false;
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
        if (!_isCharged) return false;

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
        LastFireTime = (float)NetworkTime.time;

        GameObject bullet = Instantiate(
            bulletPrefab,
            gunTransform.position,
            Quaternion.LookRotation(shootDirection)
        );

        Bullet bulletComponent = bullet.GetComponent<Bullet>();
        if (bulletComponent)
        {
            bulletComponent.Initialize(shootDirection, damage, netId);
        }

        NetworkServer.Spawn(bullet);
    }

    private void OnLastFireTimeChanged(float oldVal, float newVal)
    {
        _lastReportedProgress = -1f;
    }
}

using UnityEngine;
using Mirror;
using System.Collections;

public class NeoHealth : PlayerHealth
{
    [SerializeField] private GameObject shield;
    [SerializeField] private float shieldRechargeTime = 10f;
    [SerializeField] private float shieldDuration = 2f;
    [SerializeField] private Sprite neoAbilityIcon;
    
    private bool _isShieldOnCooldown = false;
    private Coroutine _shieldCoroutine;
    private const string NeoAbilityName = "Neo Shield";

    public override void OnStartLocalPlayer()
    {
        if (isLocalPlayer)
        {
            RegisterNeoAbility();
        }
    }

    private void RegisterNeoAbility()
    {
        if (UIManager.Instance && neoAbilityIcon)
        {
            UIManager.Instance.RegisterAbility(NeoAbilityName, shieldRechargeTime, neoAbilityIcon);
        }
    }

    public override void TakeDamage(float damage)
    {
        if (!isServer) return;
        
        if (!_isShieldOnCooldown)
        {
            ActivateShield();
            return;
        }
        
        base.TakeDamage(damage);
    }

    [Server]
    private void ActivateShield()
    {
        if (_isShieldOnCooldown) return;
        
        _isShieldOnCooldown = true;
        
        // Send server time to clients for accurate cooldown tracking
        double serverTime = NetworkTime.time;
        RpcActivateShield(serverTime);
        
        if (_shieldCoroutine != null)
            StopCoroutine(_shieldCoroutine);
        _shieldCoroutine = StartCoroutine(ShieldSequence());
    }

    [Server]
    private IEnumerator ShieldSequence()
    {
        yield return new WaitForSeconds(shieldDuration);
        
        RpcDeactivateShield();
        
        yield return new WaitForSeconds(shieldRechargeTime);
        
        _isShieldOnCooldown = false;
        RpcShieldReady();
    }

    [ClientRpc]
    private void RpcActivateShield(double serverActivationTime)
    {
        if (shield)
            shield.SetActive(true);
            
        if (isLocalPlayer && UIManager.Instance)
        {
            // Calculate remaining cooldown based on server time
            double elapsed = NetworkTime.time - serverActivationTime;
            float remainingCooldown = Mathf.Max(0f, (shieldDuration + shieldRechargeTime) - (float)elapsed);
            
            UIManager.Instance.StartAbilityCooldownWithTime(NeoAbilityName, remainingCooldown);
        }
    }

    [ClientRpc]
    private void RpcDeactivateShield()
    {
        if (shield)
            shield.SetActive(false);
    }

    [ClientRpc]
    private void RpcShieldReady()
    {
        if (isLocalPlayer && UIManager.Instance)
        {
            UIManager.Instance.ForceAbilityReady(NeoAbilityName);
        }
    }

    protected override void Die()
    {
        if (_shieldCoroutine != null)
            StopCoroutine(_shieldCoroutine);
            
        if (isLocalPlayer && UIManager.Instance)
        {
            UIManager.Instance.UnregisterAbility(NeoAbilityName);
        }
        
        base.Die();
    }
}
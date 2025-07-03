using UnityEngine;
using Mirror;
using System.Collections;

public class NeoHealth : PlayerHealth
{
    [SerializeField] private GameObject shield;
    [SerializeField] private float shieldRechargeTime = 10f;
    [SerializeField] private float shieldDuration = 2f;
    [SerializeField] private float teleportRadius = 0.5f;
    [SerializeField] private LayerMask groundLayer = 1;
    
    private bool isShieldOnCooldown = false;
    private Coroutine shieldCoroutine;

    public override void TakeDamage(float damage)
    {
        if (!isServer) return;
        
        if (!isShieldOnCooldown)
        {
            ActivateShield();
            return; // First hit activates shield, no damage taken
        }
        
        base.TakeDamage(damage);
    }

    [Server]
    private void ActivateShield()
    {
        if (isShieldOnCooldown) return;
        
        isShieldOnCooldown = true;
        
        // Activate shield visual
        RpcActivateShield();
        
        // Teleport to random position
        TeleportToRandomPosition();
        
        // Start shield duration and cooldown
        if (shieldCoroutine != null)
            StopCoroutine(shieldCoroutine);
        shieldCoroutine = StartCoroutine(ShieldSequence());
    }

    [Server]
    private void TeleportToRandomPosition()
    {
        Vector3 randomDirection = Random.insideUnitSphere * teleportRadius;
        randomDirection.y = 0; // Keep on same height level
        
        Vector3 teleportPosition = transform.position + randomDirection;
        
        // Ensure position is on ground
        if (Physics.Raycast(teleportPosition + Vector3.up * 10f, Vector3.down, out RaycastHit hit, 20f, groundLayer))
        {
            teleportPosition = hit.point;
        }
        
        transform.position = teleportPosition;
        RpcTeleportEffect(teleportPosition);
    }

    [Server]
    private IEnumerator ShieldSequence()
    {
        // Shield active duration
        yield return new WaitForSeconds(shieldDuration);
        
        RpcDeactivateShield();
        
        // Shield cooldown
        yield return new WaitForSeconds(shieldRechargeTime);
        
        isShieldOnCooldown = false;
        RpcShieldReady();
    }

    [ClientRpc]
    private void RpcActivateShield()
    {
        if (shield)
            shield.SetActive(true);
    }

    [ClientRpc]
    private void RpcDeactivateShield()
    {
        if (shield)
            shield.SetActive(false);
    }

    [ClientRpc]
    private void RpcTeleportEffect(Vector3 position)
    {
        // Add teleport visual effects here if needed
        transform.position = position;
    }

    [ClientRpc]
    private void RpcShieldReady()
    {
        // Add shield ready notification/effect here if needed
    }

    private void OnDestroy()
    {
        if (shieldCoroutine != null)
            StopCoroutine(shieldCoroutine);
    }
}
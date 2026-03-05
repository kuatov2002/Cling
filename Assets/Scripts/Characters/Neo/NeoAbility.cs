using Mirror;
using UnityEngine;

/// <summary>
/// Neo's active ability: Dash — teleport 8m forward.
/// Passive: Plasma Shield (handled by NeoHealth, no modifier needed here).
/// </summary>
public class NeoAbility : ActiveAbility
{
    [Header("Dash Settings")]
    [SerializeField] private float dashDistance = 8f;

    public override string AbilityName => "Dash";
    public override string AbilityDescription => "Teleport 8m forward instantly.";
    public override string PassiveName => "Plasma Shield";
    public override string PassiveDescription => "Auto-activates a shield on hit, blocking 1 attack (10s cooldown).";
    public override float CooldownDuration => 10f;

    [Server]
    protected override void OnActivate()
    {
        CharacterController cc = GetComponent<CharacterController>();
        if (cc == null) return;

        Vector3 dashDirection = transform.forward;
        Vector3 startPos = transform.position;

        // Check for obstacles with a raycast
        bool blocked = Physics.Raycast(startPos, dashDirection, out RaycastHit hit, dashDistance);
        float actualDistance = blocked ? hit.distance - 0.5f : dashDistance;
        if (actualDistance <= 0f) return;

        Vector3 targetPos = startPos + dashDirection * actualDistance;

        // Teleport via CharacterController
        cc.enabled = false;
        transform.position = targetPos;
        cc.enabled = true;

        // Sync to all clients
        RpcDashEffect(startPos, targetPos);
    }

    [ClientRpc]
    private void RpcDashEffect(Vector3 from, Vector3 to)
    {
        // Reset prediction state on owning client
        PlayerMovement movement = GetComponent<PlayerMovement>();
        if (movement && isLocalPlayer)
        {
            movement.Teleport(to, transform.rotation);
        }
        else if (!isLocalPlayer)
        {
            // Non-owning clients: snap position
            transform.position = to;
        }
    }
}

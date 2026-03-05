using Mirror;
using UnityEngine;

/// <summary>
/// Phoenix's active ability: Flash — AoE 30 damage to enemies within 8m.
/// Passive: Rebirth (handled by PhoenixHealth — revive once with 50% HP).
/// </summary>
public class PhoenixAbility : ActiveAbility
{
    [Header("Flash Settings")]
    [SerializeField] private float aoeDamage = 30f;
    [SerializeField] private float aoeRadius = 8f;

    // Pre-allocated for OverlapSphere (zero GC)
    private static readonly Collider[] _aoeResults = new Collider[32];

    public override string AbilityName => "Flash";
    public override string AbilityDescription => "Deal 30 damage to all enemies within 8m.";
    public override string PassiveName => "Rebirth";
    public override string PassiveDescription => "Revive once per life with 50% HP on lethal damage.";
    public override float CooldownDuration => 14f;

    [Server]
    protected override void OnActivate()
    {
        Team myTeam = GetComponent<PlayerTeam>()?.CurrentTeam ?? Team.None;
        Vector3 center = transform.position;

        int count = Physics.OverlapSphereNonAlloc(center, aoeRadius, _aoeResults);

        for (int i = 0; i < count; i++)
        {
            Collider col = _aoeResults[i];
            if (col == null) continue;

            // Skip self
            NetworkIdentity targetIdentity = col.GetComponentInParent<NetworkIdentity>();
            if (targetIdentity == null || targetIdentity == netIdentity) continue;

            // Skip teammates
            PlayerTeam targetTeam = targetIdentity.GetComponent<PlayerTeam>();
            if (targetTeam != null && myTeam != Team.None && targetTeam.CurrentTeam == myTeam)
                continue;

            // Apply damage
            PlayerHealth targetHealth = targetIdentity.GetComponent<PlayerHealth>();
            if (targetHealth != null)
            {
                targetHealth.TakeDamageFrom(aoeDamage, netId);
            }
        }

        RpcFlashEffect(center);
    }

    [ClientRpc]
    private void RpcFlashEffect(Vector3 center)
    {
        // Visual/audio effect placeholder
        // TODO: spawn flash VFX at center
    }
}

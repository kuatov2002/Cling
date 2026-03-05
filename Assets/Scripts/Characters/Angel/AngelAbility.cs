using System.Collections;
using Mirror;
using UnityEngine;

/// <summary>
/// Angel's active ability: Healing Pulse — heal self and nearby teammates for 30 HP within 10m.
/// Passive: Regeneration — passively heals 2 HP per second.
/// </summary>
public class AngelAbility : ActiveAbility
{
    [Header("Healing Pulse Settings")]
    [SerializeField] private float pulseHealAmount = 30f;
    [SerializeField] private float pulseRadius = 10f;

    [Header("Passive: Regeneration")]
    [SerializeField] private float regenPerSecond = 2f;

    private Coroutine _regenCoroutine;

    // Pre-allocated for OverlapSphere (zero GC)
    private static readonly Collider[] _aoeResults = new Collider[32];

    public override string AbilityName => "Healing Pulse";
    public override string AbilityDescription => "Heal self and teammates within 10m for 30 HP.";
    public override string PassiveName => "Regeneration";
    public override string PassiveDescription => "Passively regenerate 2 HP per second.";
    public override float CooldownDuration => 16f;

    [Server]
    protected override void SetupPassive()
    {
        if (_regenCoroutine != null)
            StopCoroutine(_regenCoroutine);
        _regenCoroutine = StartCoroutine(RegenRoutine());
    }

    [Server]
    protected override void ResetPassive()
    {
        if (_regenCoroutine != null)
        {
            StopCoroutine(_regenCoroutine);
            _regenCoroutine = null;
        }
    }

    private IEnumerator RegenRoutine()
    {
        WaitForSeconds wait = new WaitForSeconds(1f);
        while (true)
        {
            yield return wait;

            IHealable healable = GetComponent<IHealable>();
            healable?.Heal(regenPerSecond);
        }
    }

    [Server]
    protected override void OnActivate()
    {
        Team myTeam = GetComponent<PlayerTeam>()?.CurrentTeam ?? Team.None;
        Vector3 center = transform.position;

        // Heal self first
        IHealable selfHealable = GetComponent<IHealable>();
        selfHealable?.Heal(pulseHealAmount);

        // Heal nearby teammates
        int count = Physics.OverlapSphereNonAlloc(center, pulseRadius, _aoeResults);

        for (int i = 0; i < count; i++)
        {
            Collider col = _aoeResults[i];
            if (col == null) continue;

            NetworkIdentity targetIdentity = col.GetComponentInParent<NetworkIdentity>();
            if (targetIdentity == null || targetIdentity == netIdentity) continue;

            // Only heal teammates
            PlayerTeam targetTeam = targetIdentity.GetComponent<PlayerTeam>();
            if (targetTeam == null || myTeam == Team.None || targetTeam.CurrentTeam != myTeam)
                continue;

            IHealable targetHealable = targetIdentity.GetComponent<IHealable>();
            targetHealable?.Heal(pulseHealAmount);
        }

        RpcHealPulseEffect(center);
    }

    [ClientRpc]
    private void RpcHealPulseEffect(Vector3 center)
    {
        // Visual/audio effect placeholder
        // TODO: spawn heal VFX at center
    }
}

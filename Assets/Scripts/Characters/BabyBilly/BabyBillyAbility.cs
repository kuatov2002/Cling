using Mirror;
using UnityEngine;

/// <summary>
/// BabyBilly's active ability: Rage — +50% damage for 5 seconds.
/// Passive: Hyperactivity — permanently +15% movement speed.
/// </summary>
public class BabyBillyAbility : ActiveAbility
{
    [Header("Rage Settings")]
    [SerializeField] private float damageBoostMultiplier = 1.5f;
    [SerializeField] private float rageDuration = 5f;

    [Header("Passive: Hyperactivity")]
    [SerializeField] private float passiveSpeedMultiplier = 1.15f;

    private Coroutine _buffCoroutine;

    public override string AbilityName => "Rage";
    public override string AbilityDescription => "+50% weapon damage for 5 seconds.";
    public override string PassiveName => "Hyperactivity";
    public override string PassiveDescription => "Permanently increases movement speed by 15%.";
    public override float CooldownDuration => 18f;

    [Server]
    protected override void SetupPassive()
    {
        PlayerMovement movement = GetComponent<PlayerMovement>();
        if (movement != null)
        {
            movement.SpeedMultiplier = passiveSpeedMultiplier;
        }
    }

    [Server]
    protected override void ResetPassive()
    {
        PlayerMovement movement = GetComponent<PlayerMovement>();
        if (movement != null)
            movement.SpeedMultiplier = 1f;

        Gun gun = GetComponent<Gun>();
        if (gun != null)
            gun.DamageMultiplier = 1f;

        if (_buffCoroutine != null)
        {
            StopCoroutine(_buffCoroutine);
            _buffCoroutine = null;
        }
    }

    [Server]
    protected override void OnActivate()
    {
        Gun gun = GetComponent<Gun>();
        if (gun == null) return;

        if (_buffCoroutine != null)
            StopCoroutine(_buffCoroutine);

        _buffCoroutine = StartTimedBuff(
            rageDuration,
            onStart: () => gun.DamageMultiplier = damageBoostMultiplier,
            onEnd: () => gun.DamageMultiplier = 1f
        );
    }
}

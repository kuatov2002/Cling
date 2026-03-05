using Mirror;
using UnityEngine;

/// <summary>
/// UncleSam's active ability: Battle Cry — +30% movement speed for 5 seconds.
/// Passive: Armor — permanently reduces incoming damage by 15%.
/// </summary>
public class UncleSamAbility : ActiveAbility
{
    [Header("Battle Cry Settings")]
    [SerializeField] private float speedBoostMultiplier = 1.3f;
    [SerializeField] private float boostDuration = 5f;

    [Header("Passive: Armor")]
    [SerializeField] private float armorDamageMultiplier = 0.85f;

    private Coroutine _buffCoroutine;

    public override string AbilityName => "Battle Cry";
    public override string AbilityDescription => "+30% movement speed for 5 seconds.";
    public override string PassiveName => "Armor";
    public override string PassiveDescription => "Permanently reduces incoming damage by 15%.";
    public override float CooldownDuration => 15f;

    [Server]
    protected override void SetupPassive()
    {
        PlayerHealth health = GetComponent<PlayerHealth>();
        if (health != null)
        {
            health.IncomingDamageMultiplier = armorDamageMultiplier;
        }
    }

    [Server]
    protected override void ResetPassive()
    {
        // Reset speed to default before SetupPassive re-applies armor
        PlayerMovement movement = GetComponent<PlayerMovement>();
        if (movement != null)
            movement.SpeedMultiplier = 1f;

        PlayerHealth health = GetComponent<PlayerHealth>();
        if (health != null)
            health.IncomingDamageMultiplier = 1f;

        if (_buffCoroutine != null)
        {
            StopCoroutine(_buffCoroutine);
            _buffCoroutine = null;
        }
    }

    [Server]
    protected override void OnActivate()
    {
        PlayerMovement movement = GetComponent<PlayerMovement>();
        if (movement == null) return;

        if (_buffCoroutine != null)
            StopCoroutine(_buffCoroutine);

        _buffCoroutine = StartTimedBuff(
            boostDuration,
            onStart: () => movement.SpeedMultiplier = speedBoostMultiplier,
            onEnd: () => movement.SpeedMultiplier = 1f
        );
    }
}

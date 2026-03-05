using Mirror;
using UnityEngine;

/// <summary>
/// CrazyJack's active ability: Va-Bank — next 5 shots are guaranteed critical hits (x2 damage).
/// Passive: Luck (handled by CrazyJackGun — 40% chance for double damage).
/// </summary>
public class CrazyJackAbility : ActiveAbility
{
    [Header("Va-Bank Settings")]
    [SerializeField] private int guaranteedCritCount = 5;

    public override string AbilityName => "Va-Bank";
    public override string AbilityDescription => "Next 5 shots deal guaranteed double damage.";
    public override string PassiveName => "Luck";
    public override string PassiveDescription => "40% chance to deal double damage on each hit.";
    public override float CooldownDuration => 16f;

    [Server]
    protected override void OnActivate()
    {
        CrazyJackGun gun = GetComponent<CrazyJackGun>();
        if (gun != null)
        {
            gun.ActivateGuaranteedCrits(guaranteedCritCount);
        }
    }
}

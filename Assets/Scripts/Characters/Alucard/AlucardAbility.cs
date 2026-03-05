using Mirror;
using UnityEngine;

/// <summary>
/// Alucard's active ability: Blood Feast — instantly heal 50 HP.
/// Passive: Vampirism (handled by AlucardGun — +10 HP per hit, no modifier needed here).
/// </summary>
public class AlucardAbility : ActiveAbility
{
    [Header("Blood Feast Settings")]
    [SerializeField] private float healAmount = 50f;

    public override string AbilityName => "Blood Feast";
    public override string AbilityDescription => "Instantly restore 50 HP.";
    public override string PassiveName => "Vampirism";
    public override string PassiveDescription => "Heal 10 HP for each successful hit on an enemy.";
    public override float CooldownDuration => 15f;

    [Server]
    protected override void OnActivate()
    {
        IHealable healable = GetComponent<IHealable>();
        healable?.Heal(healAmount);
    }
}

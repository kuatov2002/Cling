using Mirror;
using UnityEngine;

/// <summary>
/// Shadow's active ability: Cloak — become invisible for 4 seconds.
/// Passive: Shadow (handled by ShadowHealth — auto-invis 5s on taking damage).
/// </summary>
public class ShadowAbility : ActiveAbility
{
    [Header("Cloak Settings")]
    [SerializeField] private float cloakDuration = 4f;

    public override string AbilityName => "Cloak";
    public override string AbilityDescription => "Become invisible for 4 seconds.";
    public override string PassiveName => "Shadow";
    public override string PassiveDescription => "Automatically become invisible for 5s when taking damage.";
    public override float CooldownDuration => 12f;

    [Server]
    protected override void OnActivate()
    {
        ShadowHealth shadowHealth = GetComponent<ShadowHealth>();
        if (shadowHealth != null)
        {
            shadowHealth.ActivateManualInvisibility(cloakDuration);
        }
    }
}

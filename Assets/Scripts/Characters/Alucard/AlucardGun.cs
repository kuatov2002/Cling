using Mirror;
using UnityEngine;

/// <summary>
/// Alucard's hitscan weapon: damages target AND heals owner on hit.
/// </summary>
public class AlucardGun : Gun
{
    [SerializeField] private float healAmount = 10f;

    /// <summary>
    /// Override server hit: apply damage AND heal the shooter.
    /// </summary>
    [Server]
    protected override void OnServerHit(IDamageable target, RaycastHit hitInfo, float hitDamage)
    {
        // Deal damage to target
        base.OnServerHit(target, hitInfo, hitDamage);

        // Heal self
        var selfHealable = GetComponent<IHealable>();
        selfHealable?.Heal(healAmount);
    }
}

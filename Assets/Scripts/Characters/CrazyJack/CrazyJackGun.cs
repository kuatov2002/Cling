using Mirror;
using UnityEngine;

/// <summary>
/// CrazyJack's hitscan weapon: 40% chance to deal double damage.
/// Active ability "Va-bank" grants guaranteed crits for N shots.
/// </summary>
public class CrazyJackGun : Gun
{
    [SerializeField] private float luckyChance = 0.4f;

    // ── Guaranteed crit system (set by CrazyJackAbility) ────────
    private int _guaranteedCritShots;

    /// <summary>
    /// Activate guaranteed critical hits for the next <paramref name="count"/> shots.
    /// Called from CrazyJackAbility.OnActivate().
    /// </summary>
    [Server]
    public void ActivateGuaranteedCrits(int count)
    {
        _guaranteedCritShots = count;
    }

    /// <summary>
    /// Override server hit: guaranteed crit if active, otherwise 40% lucky roll.
    /// </summary>
    [Server]
    protected override void OnServerHit(IDamageable target, RaycastHit hitInfo, float hitDamage)
    {
        bool isCrit;

        if (_guaranteedCritShots > 0)
        {
            isCrit = true;
            _guaranteedCritShots--;
        }
        else
        {
            isCrit = Random.value < luckyChance;
        }

        float finalDamage = isCrit ? hitDamage * 2f : hitDamage;
        base.OnServerHit(target, hitInfo, finalDamage);
    }
}

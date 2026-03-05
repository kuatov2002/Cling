using Mirror;
using UnityEngine;

/// <summary>
/// CrazyJack's hitscan weapon: 40% chance to deal double damage.
/// </summary>
public class CrazyJackGun : Gun
{
    [SerializeField] private float luckyChance = 0.4f;

    /// <summary>
    /// Override server hit: roll lucky shot for double damage.
    /// </summary>
    [Server]
    protected override void OnServerHit(IDamageable target, RaycastHit hitInfo, float hitDamage)
    {
        bool isLuckyShot = Random.value < luckyChance;
        float finalDamage = isLuckyShot ? hitDamage * 2f : hitDamage;

        base.OnServerHit(target, hitInfo, finalDamage);
    }
}

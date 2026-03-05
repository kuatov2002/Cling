using UnityEngine;

/// <summary>
/// DEPRECATED: Alucard heal-on-hit logic moved to AlucardGun.OnServerHit().
/// Kept as stub to avoid missing-script errors on existing prefabs.
/// </summary>
public class AlucardBullet : Bullet
{
    private float _healAmount;

    public void Initialize(Vector3 dir, float dmg, uint ownerNetId, float heal)
    {
        base.Initialize(dir, dmg, ownerNetId);
        _healAmount = heal;
    }
}

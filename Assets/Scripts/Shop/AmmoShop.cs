using Mirror;
using UnityEngine;

/// <summary>
/// Deprecated: Ammo system removed in Hero Shooter conversion.
/// Kept as stub to avoid missing-script errors on existing scene objects.
/// Safe to remove this component and its GameObject from scenes.
/// </summary>
public class AmmoShop : NetworkBehaviour, IInteractable
{
    public string InteractText => string.Empty;

    public void Interact() { }
}

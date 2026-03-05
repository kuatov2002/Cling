using System.Collections;
using Mirror;
using UnityEngine;

/// <summary>
/// Abstract base class for character active abilities.
/// Handles: cooldown (SyncVar), UI registration, passive setup, respawn reset.
/// Subclasses implement OnActivate() and optionally SetupPassive() / ResetPassive().
/// </summary>
public abstract class ActiveAbility : NetworkBehaviour
{
    // ── Metadata (F1 info panel) ─────────────────────────────────
    public abstract string AbilityName { get; }
    public abstract string AbilityDescription { get; }
    public abstract string PassiveName { get; }
    public abstract string PassiveDescription { get; }
    public abstract float CooldownDuration { get; }

    [SerializeField] protected Sprite abilityIcon;

    /// <summary>Icon sprite for the ability HUD slot.</summary>
    public Sprite AbilityIcon => abilityIcon;

    // ── Cooldown ─────────────────────────────────────────────────
    [SyncVar] private float _cooldownEndTime = -Mathf.Infinity;

    /// <summary>True when the ability is off cooldown and ready to use.</summary>
    public bool IsReady => (float)NetworkTime.time >= _cooldownEndTime;

    /// <summary>Remaining cooldown seconds (0 if ready).</summary>
    public float CooldownRemaining => Mathf.Max(0f, _cooldownEndTime - (float)NetworkTime.time);

    // ── Lifecycle ────────────────────────────────────────────────

    public override void OnStartLocalPlayer()
    {
        base.OnStartLocalPlayer();
        RegisterAbilityUI();
        RegisterInfoPanel();
    }

    public override void OnStartServer()
    {
        base.OnStartServer();
        SetupPassive();
    }

    public override void OnStopLocalPlayer()
    {
        base.OnStopLocalPlayer();
        UnregisterAbilityUI();
    }

    // ── Server API ───────────────────────────────────────────────

    /// <summary>
    /// Called from PlayerMovement.OnServerTick when the player presses F.
    /// Checks cooldown, calls OnActivate(), starts cooldown, syncs UI.
    /// </summary>
    [Server]
    public void TryActivate()
    {
        if (!IsReady) return;

        OnActivate();

        _cooldownEndTime = (float)NetworkTime.time + CooldownDuration;
        RpcStartCooldown(CooldownDuration);
    }

    /// <summary>
    /// Implement ability logic here. Only called on server (guarded by TryActivate).
    /// Mirror does not allow [Server] on abstract/virtual — apply it on each override.
    /// </summary>
    protected abstract void OnActivate();

    /// <summary>
    /// Configure passive effects on the player (called once on server start).
    /// Override in subclasses that have passives (e.g., UncleSam armor, BabyBilly speed).
    /// </summary>
    protected virtual void SetupPassive() { }

    /// <summary>
    /// Reset passive effects (called on respawn before SetupPassive is re-applied).
    /// Override in subclasses that modify multipliers.
    /// </summary>
    protected virtual void ResetPassive() { }

    /// <summary>
    /// Called by PlayerHealth on respawn — resets cooldown and re-applies passives.
    /// </summary>
    [Server]
    public void OnRespawn()
    {
        _cooldownEndTime = -Mathf.Infinity;
        ResetPassive();
        SetupPassive();
        RpcResetCooldown();
    }

    // ── Client UI ────────────────────────────────────────────────

    private void RegisterAbilityUI()
    {
        if (UIManager.Instance && abilityIcon)
        {
            UIManager.Instance.RegisterAbility(AbilityName, CooldownDuration, abilityIcon);
        }
    }

    private void UnregisterAbilityUI()
    {
        if (UIManager.Instance)
        {
            UIManager.Instance.UnregisterAbility(AbilityName);
            UIManager.Instance.ClearAbilityInfo();
        }
    }

    private void RegisterInfoPanel()
    {
        if (UIManager.Instance)
        {
            UIManager.Instance.SetAbilityInfo(
                PassiveName, PassiveDescription,
                AbilityName, AbilityDescription,
                CooldownDuration, abilityIcon);
        }
    }

    [ClientRpc]
    private void RpcStartCooldown(float duration)
    {
        if (isLocalPlayer && UIManager.Instance)
        {
            UIManager.Instance.StartAbilityCooldownWithTime(AbilityName, duration);
        }
    }

    [ClientRpc]
    private void RpcResetCooldown()
    {
        if (isLocalPlayer && UIManager.Instance)
        {
            UIManager.Instance.ForceAbilityReady(AbilityName);
        }
    }

    // ── Helper: timed buff coroutine ─────────────────────────────

    /// <summary>
    /// Helper for timed buffs: invokes onStart, waits duration, invokes onEnd.
    /// Useful for speed/damage boosts.
    /// </summary>
    protected Coroutine StartTimedBuff(float duration, System.Action onStart, System.Action onEnd)
    {
        if (!isServer) return null;
        return StartCoroutine(TimedBuffRoutine(duration, onStart, onEnd));
    }

    private IEnumerator TimedBuffRoutine(float duration, System.Action onStart, System.Action onEnd)
    {
        onStart?.Invoke();
        yield return new WaitForSeconds(duration);
        onEnd?.Invoke();
    }
}

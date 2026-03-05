using System.Collections;
using Mirror;
using UnityEngine;
using UnityEngine.UI;

public class PlayerHealth : NetworkBehaviour, IDamageable, IHealable
{
    [SerializeField] protected float maxHealth = 100f;
    [SerializeField] private Image healthBar;
    [SerializeField] private ParticleSystem healEffect;

    [SyncVar(hook = nameof(OnHealthChanged))]
    protected float _currentHealth;

    private uint _lastAttackerNetId;
    private Coroutine _respawnCoroutine;

    // ── Damage modifier (set by ActiveAbility for passive armor) ────
    private float _incomingDamageMultiplier = 1f;

    /// <summary>Multiplier applied to all incoming damage. Set from ActiveAbility passives (e.g. UncleSam 0.85).</summary>
    public float IncomingDamageMultiplier
    {
        get => _incomingDamageMultiplier;
        set => _incomingDamageMultiplier = Mathf.Max(0f, value);
    }

    /// <summary>Current HP (read-only, for ability checks).</summary>
    public float CurrentHealth => _currentHealth;
    /// <summary>Max HP (read-only, for ability checks).</summary>
    public float MaxHealthValue => maxHealth;

    // ── Cached component arrays (zero GC in RpcSetPlayerVisible) ───
    private Renderer[] _cachedRenderers;
    private Collider[] _cachedColliders;
    private Canvas[] _cachedCanvases;

    private void Awake()
    {
        // Cache once — avoid per-call GetComponentsInChildren allocations
        _cachedRenderers = GetComponentsInChildren<Renderer>(true);
        _cachedColliders = GetComponentsInChildren<Collider>(true);
        _cachedCanvases = GetComponentsInChildren<Canvas>(true);
    }

    [ServerCallback]
    public override void OnStartServer()
    {
        _currentHealth = maxHealth;
    }

    public virtual void TakeDamage(float damage)
    {
        if (!isServer) return;
        _currentHealth = Mathf.Max(_currentHealth - damage * _incomingDamageMultiplier, 0f);
        if (_currentHealth == 0f) Die();
    }

    [Server]
    public void TakeDamageFrom(float damage, uint attackerNetId)
    {
        // Block friendly fire — reject damage from same-team players
        if (attackerNetId != 0 &&
            NetworkServer.spawned.TryGetValue(attackerNetId, out NetworkIdentity attackerIdentity))
        {
            Team attackerTeam = attackerIdentity.GetComponent<PlayerTeam>()?.CurrentTeam ?? Team.None;
            Team myTeam = GetComponent<PlayerTeam>()?.CurrentTeam ?? Team.None;
            if (attackerTeam != Team.None && myTeam != Team.None && attackerTeam == myTeam)
                return; // Friendly fire blocked
        }

        _lastAttackerNetId = attackerNetId;
        TakeDamage(damage);
    }

    public virtual void Heal(float healAmount)
    {
        if (!isServer) return;
        _currentHealth = Mathf.Min(_currentHealth + healAmount, maxHealth);
        // TODO: pool heal effects via NetworkPool instead of Instantiate
        if (healEffect)
            Instantiate(healEffect, transform.position, Quaternion.identity);
    }

    private void OnHealthChanged(float oldValue, float newValue)
    {
        if (healthBar)
            healthBar.fillAmount = newValue / maxHealth;
    }

    protected virtual void Die()
    {
        if (!isServer) return;

        GetComponent<PlayerState>().CurrentState = PlayerState.State.Dead;

        // Track death
        PlayerStats stats = GetComponent<PlayerStats>();
        stats?.AddDeath();

        // Get player info for notification
        PlayerState playerState = GetComponent<PlayerState>();
        PlayerTeam playerTeam = GetComponent<PlayerTeam>();
        string playerNickname = playerState?.PlayerNickname ?? "Unknown";
        Team team = playerTeam?.CurrentTeam ?? Team.None;

        // Credit kill to attacker
        CreditKillToAttacker(playerNickname);

        // Notify all clients
        RpcNotifyPlayerDeath(playerNickname, team);

        // Hide player (disable visuals/collision)
        RpcSetPlayerVisible(false);

        // Start respawn timer
        if (_respawnCoroutine != null)
            StopCoroutine(_respawnCoroutine);
        _respawnCoroutine = StartCoroutine(RespawnAfterDelay());
    }

    [Server]
    private void CreditKillToAttacker(string victimName)
    {
        if (_lastAttackerNetId == 0) return;
        if (!NetworkServer.spawned.TryGetValue(_lastAttackerNetId, out NetworkIdentity attackerIdentity)) return;

        // Safety net: don't award kills for friendly fire
        Team attackerTeam = attackerIdentity.GetComponent<PlayerTeam>()?.CurrentTeam ?? Team.None;
        Team victimTeam = GetComponent<PlayerTeam>()?.CurrentTeam ?? Team.None;
        if (attackerTeam != Team.None && victimTeam != Team.None && attackerTeam == victimTeam)
            return; // Team kill — no credit

        PlayerStats attackerStats = attackerIdentity.GetComponent<PlayerStats>();
        attackerStats?.AddKill();

        GameManager.Instance?.OnPlayerKilled(attackerTeam);

        string killerName = attackerIdentity.GetComponent<PlayerState>()?.PlayerNickname ?? "Unknown";
        NetworkGameEvents.Instance?.RpcPlayerKilled(killerName, victimName);
    }

    [Server]
    private IEnumerator RespawnAfterDelay()
    {
        float delay = GameManager.Instance ? GameManager.Instance.RespawnDelay : 3f;
        yield return new WaitForSeconds(delay);
        Respawn();
    }

    [Server]
    protected virtual void Respawn()
    {
        // Reset health
        _currentHealth = maxHealth;
        _lastAttackerNetId = 0;

        // Reset abilities for subclasses
        ResetAbilitiesForRespawn();

        // Move to team spawn point
        Team team = GetComponent<PlayerTeam>()?.CurrentTeam ?? Team.None;
        Transform spawnPoint = GameManager.Instance?.GetTeamSpawnPoint(team);
        if (spawnPoint != null)
        {
            RpcTeleportPlayer(spawnPoint.position, spawnPoint.rotation);
        }

        // Restore state
        GetComponent<PlayerState>().CurrentState = PlayerState.State.Alive;
        RpcSetPlayerVisible(true);
    }

    [Server]
    protected virtual void ResetAbilitiesForRespawn()
    {
        // Override in subclasses to reset character-specific state

        // Reset active ability cooldown + re-apply passives
        ActiveAbility ability = GetComponent<ActiveAbility>();
        if (ability != null)
            ability.OnRespawn();
    }

    [ClientRpc]
    private void RpcNotifyPlayerDeath(string playerNickname, Team playerTeam)
    {
        UIManager.Instance?.ShowNotification($"{playerNickname} was eliminated!");
    }

    [ClientRpc]
    private void RpcSetPlayerVisible(bool visible)
    {
        // Use cached arrays — zero GC
        for (int i = 0; i < _cachedRenderers.Length; i++)
        {
            if (_cachedRenderers[i])
                _cachedRenderers[i].enabled = visible;
        }

        for (int i = 0; i < _cachedColliders.Length; i++)
        {
            if (_cachedColliders[i])
                _cachedColliders[i].enabled = visible;
        }

        for (int i = 0; i < _cachedCanvases.Length; i++)
        {
            if (_cachedCanvases[i])
                _cachedCanvases[i].enabled = visible;
        }

        // Re-enable health bar on show
        if (visible && healthBar)
        {
            healthBar.fillAmount = _currentHealth / maxHealth;
        }
    }

    [ClientRpc]
    private void RpcTeleportPlayer(Vector3 position, Quaternion rotation)
    {
        // Use PlayerMovement.Teleport which properly resets prediction state
        PlayerMovement movement = GetComponent<PlayerMovement>();
        if (movement)
        {
            movement.Teleport(position, rotation);
        }
        else
        {
            // Fallback
            CharacterController cc = GetComponent<CharacterController>();
            if (cc)
            {
                cc.enabled = false;
                transform.SetPositionAndRotation(position, rotation);
                cc.enabled = true;
            }
            else
            {
                transform.SetPositionAndRotation(position, rotation);
            }
        }
    }
}

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

    [ServerCallback]
    public override void OnStartServer()
    {
        _currentHealth = maxHealth;
    }

    public virtual void TakeDamage(float damage)
    {
        if (!isServer) return;
        _currentHealth = Mathf.Max(_currentHealth - damage, 0f);
        if (_currentHealth == 0f) Die();
    }

    [Server]
    public void TakeDamageFrom(float damage, uint attackerNetId)
    {
        _lastAttackerNetId = attackerNetId;
        TakeDamage(damage);
    }

    public virtual void Heal(float healAmount)
    {
        if (!isServer) return;
        _currentHealth = Mathf.Min(_currentHealth + healAmount, maxHealth);
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

        PlayerStats attackerStats = attackerIdentity.GetComponent<PlayerStats>();
        attackerStats?.AddKill();

        Team attackerTeam = attackerIdentity.GetComponent<PlayerTeam>()?.CurrentTeam ?? Team.None;
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
    }

    [ClientRpc]
    private void RpcNotifyPlayerDeath(string playerNickname, Team playerTeam)
    {
        UIManager.Instance?.ShowNotification($"{playerNickname} was eliminated!");
    }

    [ClientRpc]
    private void RpcSetPlayerVisible(bool visible)
    {
        // Toggle all renderers
        var renderers = GetComponentsInChildren<Renderer>();
        foreach (var renderer in renderers)
        {
            renderer.enabled = visible;
        }

        // Toggle colliders
        var colliders = GetComponentsInChildren<Collider>();
        foreach (var col in colliders)
        {
            col.enabled = visible;
        }

        // Toggle UI elements (health bar, nickname)
        var canvases = GetComponentsInChildren<Canvas>();
        foreach (var canvas in canvases)
        {
            canvas.enabled = visible;
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

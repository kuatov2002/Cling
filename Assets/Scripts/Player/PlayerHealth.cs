using Mirror;
using UnityEngine;
using UnityEngine.UI;

public class PlayerHealth : NetworkBehaviour, IDamageable, IHealable
{
    [SerializeField] protected float maxHealth = 100f;
    [SerializeField] private Image healthBar;
    [SerializeField] private GameObject angelPrefab;
    
    [SyncVar(hook = nameof(OnHealthChanged))]
    protected float _currentHealth;

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

    public virtual void Heal(float healAmount)
    {
        if (!isServer) return;
        _currentHealth = Mathf.Min(_currentHealth + healAmount, maxHealth);
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
        
        // Получаем данные игрока для уведомления
        PlayerState playerState = GetComponent<PlayerState>();
        PlayerRole playerRole = GetComponent<PlayerRole>();
        
        string playerNickname = playerState?.PlayerNickname ?? "Unknown";
        RoleType playerRoleType = playerRole?.CurrentRole ?? RoleType.None;
        
        // Отправляем уведомление всем клиентам
        RpcNotifyPlayerDeath(playerNickname, playerRoleType);
        
        // Создаем ангела только для владельца игрока
        SpawnAngelForPlayer();
        
        // Уничтожаем игрока для всех
        NetworkServer.Destroy(gameObject);
    }

    [ClientRpc]
    private void RpcNotifyPlayerDeath(string playerNickname, RoleType playerRole)
    {
        if (UIManager.Instance != null)
        {
            UIManager.Instance.ShowNotification($"{playerNickname} ({playerRole}) died!");
        }
    }

    [TargetRpc]
    private void SpawnAngelForPlayer()
    {
        // Создаем ангела только локально у умершего игрока
        Instantiate(angelPrefab, transform.position, transform.rotation);
    }
}
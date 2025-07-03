using Mirror;
using UnityEngine;
using UnityEngine.UI;

public class PlayerHealth : NetworkBehaviour, IDamageable
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
        _currentHealth = Mathf.Clamp(_currentHealth - damage, 0f, maxHealth);
        if (_currentHealth == 0f) Die();
    }

    private void OnHealthChanged(float oldValue, float newValue)
    {
        if (healthBar)
            healthBar.fillAmount = newValue / maxHealth;
    }

    protected void Die()
    {
        if (!isServer) return;
    
        GetComponent<PlayerState>().CurrentState = PlayerState.State.Dead;
    
        // Вызываем RPC только для владельца игрока
        SpawnAngelForPlayer();
    
        // Уничтожаем игрока для всех
        NetworkServer.Destroy(gameObject);
    }

    [TargetRpc]
    private void SpawnAngelForPlayer()
    {
        // Создаем ангела только локально у умершего игрока
        Instantiate(angelPrefab, transform.position, transform.rotation);
    }
}

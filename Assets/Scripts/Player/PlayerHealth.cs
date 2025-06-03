using Mirror;
using UnityEngine;
using UnityEngine.UI;

public class PlayerHealth : NetworkBehaviour, IDamageable
{
    [SerializeField] private float maxHealth = 100f;
    [SerializeField] private Image healthBar;
    
    [SyncVar(hook = nameof(OnHealthChanged))]
    private float _currentHealth;

    private PlayerState _playerState;

    private void Awake()
    {
        _playerState = GetComponent<PlayerState>();
    }

    // Инициализируется и на сервере, и на хост‑клиенте
    [ServerCallback]
    public override void OnStartServer()
    {
        _currentHealth = maxHealth;
    }

    public void TakeDamage(float damage)
    {
        if (!isServer) return;
        _currentHealth = Mathf.Clamp(_currentHealth - damage, 0f, maxHealth);
        if (_currentHealth == 0f) Die();
    }

    private void OnHealthChanged(float oldValue, float newValue)
    {
        if (healthBar) healthBar.fillAmount = newValue / maxHealth;
        if (!isLocalPlayer) return;
        Debug.Log($"HP: {oldValue} → {newValue}");
    }

    private void Die()
    {
        _playerState.CurrentState = PlayerState.State.Dead;
        Destroy(gameObject);
    }
}
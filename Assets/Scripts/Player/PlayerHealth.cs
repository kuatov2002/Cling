using Mirror;
using UnityEngine;

public class PlayerHealth : NetworkBehaviour, IDamageable
{
    [SerializeField] private float maxHealth = 100f;

    [SyncVar(hook = nameof(OnHealthChanged))]
    private float _currentHealth;

    // Инициализируется и на сервере, и на хост‑клиенте
    [ServerCallback]
    public override void OnStartServer()
    {
        _currentHealth = maxHealth;
    }

    public void TakeDamage(float damage)
    {
        _currentHealth = Mathf.Max(0f, _currentHealth - damage);
        if (_currentHealth == 0f) Die();
    }

    private void OnHealthChanged(float oldValue, float newValue)
    {
        if (!isLocalPlayer) return;  // обновляем UI только для своего игрока
        Debug.Log($"HP: {oldValue} → {newValue}");
        // тут обновляем здоровье на экране
    }

    private void Die()
    {
        NetworkServer.Destroy(gameObject);
    }
}
using Mirror;
using UnityEngine;
using UnityEngine.UI;

public class PlayerHealth : NetworkBehaviour, IDamageable
{
    [SerializeField] protected float maxHealth = 100f;
    [SerializeField] private Image healthBar;

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
        GetComponent<PlayerState>().CurrentState = PlayerState.State.Dead;
        Destroy(gameObject);
    }
}

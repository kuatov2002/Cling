using UnityEngine;

public class PlayerHealth : MonoBehaviour, IDamageable
{
    [SerializeField] private float maxHealth = 100f;
    private float _currentHealth;

    private void Awake()
    {
        _currentHealth = maxHealth;
    }

    public void TakeDamage(float damage)
    {
        _currentHealth = Mathf.Max(0f, _currentHealth - damage);

        Debug.Log($"Игрок получил {damage} урона. Осталось здоровья: {_currentHealth}");

        if (_currentHealth == 0f)
        {
            Die();
        }
    }

    private void Die()
    {
        Debug.Log("Игрок пал");
        Destroy(gameObject);
        // Здесь можно добавить логику смерти: деактивация персонажа, экран "Game Over" и т.д.
    }
}
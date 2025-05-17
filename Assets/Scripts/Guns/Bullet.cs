using UnityEngine;

[RequireComponent(typeof(Collider),typeof(Rigidbody))]
public class Bullet : MonoBehaviour
{
    [SerializeField] private float speed = 20f;
    [SerializeField] private float lifeTime = 3f;

    private float _damage;
    private Vector3 _direction;

    private void Start()
    {
        Destroy(gameObject, lifeTime);
    }

    private void Update()
    {
        transform.position += _direction * (speed * Time.deltaTime);
    }

    public void SetDamage(float dmg)
    {
        _damage = dmg;
    }

    public void SetDirection(Vector3 dir)
    {
        _direction = dir;
    }
    
    private void OnTriggerEnter(Collider other)
    {
        IDamageable target = other.gameObject.GetComponent<IDamageable>();
        if (target != null)
        {
            target.TakeDamage(_damage);
        }

        Destroy(gameObject); // Уничтожаем пулю при любом другом столкновении
    }
}
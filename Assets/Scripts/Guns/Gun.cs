using UnityEngine;

public class Gun : MonoBehaviour
{
    [SerializeField] private GameObject bulletPrefab;
    [SerializeField] private float damage = 20f;
    [SerializeField] private float cooldown = 0.5f;
    
    private float _lastFireTime = 0f;

    public void Fire()
    {
        if (Time.time - _lastFireTime < cooldown) return;

        // Create the bullet at the gun's position with the gun's rotation
        GameObject bullet = Instantiate(bulletPrefab, transform.position, transform.rotation);
        Bullet bulletScript = bullet.GetComponent<Bullet>();

        if (bulletScript != null)
        {
            bulletScript.SetDamage(damage);
            
            // Use the gun's forward direction for bullet trajectory
            bulletScript.SetDirection(transform.forward);
        }

        _lastFireTime = Time.time;
    }
}
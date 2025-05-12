using UnityEngine;

public class Gun : MonoBehaviour
{
    [SerializeField] private GameObject bulletPrefab;
    [SerializeField] private float damage = 20f;
    [SerializeField] private float cooldown = 0.5f;
    [SerializeField] private Camera playerCamera; // Reference to the player's camera

    private float _lastFireTime = 0f;

    public void Fire()
    {
        if (Time.time - _lastFireTime < cooldown) return;

        // If camera reference is not assigned, try to find the main camera
        if (playerCamera == null)
        {
            playerCamera = Camera.main;
            if (playerCamera == null)
            {
                Debug.LogError("No camera found. Please assign a camera reference to the Gun script.");
                return;
            }
        }

        // Create the bullet at the gun's position but with the camera's rotation
        GameObject bullet = Instantiate(bulletPrefab, transform.position, Quaternion.identity);
        Bullet bulletScript = bullet.GetComponent<Bullet>();

        if (bulletScript != null)
        {
            bulletScript.SetDamage(damage);
            
            // Use the camera's forward direction instead of the gun's forward direction
            bulletScript.SetDirection(playerCamera.transform.forward);
        }

        _lastFireTime = Time.time;
    }
}
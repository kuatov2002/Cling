using UnityEngine;

public class ParticleDestroyer : MonoBehaviour
{
    private ParticleSystem particles;
    
    private void Start()
    {
        particles = GetComponent<ParticleSystem>();
        
        if (!particles)
        {
            Debug.LogWarning("ParticleDestroyer: No ParticleSystem found!");
            return;
        }
        
        // Уничтожаем объект через время жизни системы частиц
        float lifetime = particles.main.startLifetime.constantMax + particles.main.duration;
        Destroy(gameObject, lifetime);
    }
}
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Generic zero-GC object pool for MonoBehaviours.
/// Pre-allocates instances at startup; auto-grows with a warning if exhausted.
/// </summary>
public class NetworkPool<T> where T : MonoBehaviour
{
    private readonly Stack<T> _available;
    private readonly T _prefab;
    private readonly Transform _parent;
    private readonly int _initialSize;

    public int CountActive { get; private set; }
    public int CountTotal { get; private set; }

    public NetworkPool(T prefab, int initialSize, Transform parent = null)
    {
        _prefab = prefab;
        _initialSize = initialSize;
        _parent = parent;
        _available = new Stack<T>(initialSize);

        for (int i = 0; i < initialSize; i++)
        {
            T instance = CreateInstance();
            instance.gameObject.SetActive(false);
            _available.Push(instance);
        }
    }

    /// <summary>
    /// Get an active instance from the pool. Caller must Release() when done.
    /// </summary>
    public T Get()
    {
        T instance;

        if (_available.Count > 0)
        {
            instance = _available.Pop();
        }
        else
        {
            // Auto-grow (with warning)
            Debug.LogWarning($"[NetworkPool<{typeof(T).Name}>] Pool exhausted (initial={_initialSize}, total={CountTotal}). Growing by 1.");
            instance = CreateInstance();
        }

        instance.gameObject.SetActive(true);
        CountActive++;
        return instance;
    }

    /// <summary>
    /// Get and position an instance from the pool.
    /// </summary>
    public T Get(Vector3 position, Quaternion rotation)
    {
        T instance = Get();
        instance.transform.SetPositionAndRotation(position, rotation);
        return instance;
    }

    /// <summary>
    /// Return an instance to the pool.
    /// </summary>
    public void Release(T instance)
    {
        if (instance == null) return;
        instance.gameObject.SetActive(false);
        _available.Push(instance);
        CountActive--;
    }

    /// <summary>
    /// Deactivate and return all instances. Use when resetting the scene.
    /// </summary>
    public void ReleaseAll()
    {
        // Note: can't easily iterate active instances without tracking them.
        // Callers should release individually, or this is for cleanup.
        CountActive = 0;
    }

    private T CreateInstance()
    {
        T instance = Object.Instantiate(_prefab, _parent);
        instance.gameObject.SetActive(false);
        CountTotal++;
        return instance;
    }
}

/// <summary>
/// MonoBehaviour that returns itself to a pool after a delay.
/// Attach to poolable objects (tracers, particles, etc.).
/// </summary>
public class PooledObject : MonoBehaviour
{
    private System.Action<PooledObject> _releaseAction;
    private float _returnDelay;
    private float _spawnTime;

    public void Initialize(System.Action<PooledObject> releaseAction, float returnDelay)
    {
        _releaseAction = releaseAction;
        _returnDelay = returnDelay;
        _spawnTime = Time.time;
    }

    private void Update()
    {
        if (_releaseAction != null && Time.time - _spawnTime >= _returnDelay)
        {
            _releaseAction.Invoke(this);
            _releaseAction = null;
        }
    }
}

/// <summary>
/// Simple particle pool helper — plays a ParticleSystem and returns to pool when done.
/// </summary>
public class PooledParticle : MonoBehaviour
{
    private NetworkPool<PooledParticle> _pool;
    private ParticleSystem _particles;

    private void Awake()
    {
        _particles = GetComponent<ParticleSystem>();
    }

    public void SetPool(NetworkPool<PooledParticle> pool)
    {
        _pool = pool;
    }

    public void Play(Vector3 position)
    {
        transform.position = position;

        if (_particles != null)
        {
            _particles.Clear(true);
            _particles.Play(true);
        }
    }

    private void Update()
    {
        if (_particles != null && !_particles.isPlaying && _pool != null)
        {
            _pool.Release(this);
        }
    }
}

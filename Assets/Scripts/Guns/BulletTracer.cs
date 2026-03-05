using UnityEngine;

/// <summary>
/// Visual-only bullet tracer. NOT a NetworkBehaviour — runs locally, pooled.
/// Lerps a visual element from origin to hit point, then returns to pool.
/// </summary>
public class BulletTracer : MonoBehaviour
{
    [SerializeField] private float tracerSpeed = 200f;
    [SerializeField] private float maxLifetime = 0.5f;
    [SerializeField] private TrailRenderer trailRenderer;
    [SerializeField] private LineRenderer lineRenderer;

    private Vector3 _start;
    private Vector3 _end;
    private float _elapsed;
    private float _duration;
    private System.Action<BulletTracer> _releaseAction;
    private bool _active;

    /// <summary>
    /// Initialize and fire the tracer from origin to target.
    /// </summary>
    public void Fire(Vector3 origin, Vector3 target, System.Action<BulletTracer> releaseAction)
    {
        _start = origin;
        _end = target;
        _releaseAction = releaseAction;
        _elapsed = 0f;

        float distance = Vector3.Distance(origin, target);
        _duration = Mathf.Min(distance / tracerSpeed, maxLifetime);
        if (_duration < 0.01f) _duration = 0.01f;

        transform.position = origin;
        transform.LookAt(target);

        // Reset trail
        if (trailRenderer)
        {
            trailRenderer.Clear();
            trailRenderer.enabled = true;
        }

        // LineRenderer mode: draw instant line
        if (lineRenderer)
        {
            lineRenderer.enabled = true;
            lineRenderer.SetPosition(0, origin);
            lineRenderer.SetPosition(1, target);
        }

        _active = true;
    }

    private void Update()
    {
        if (!_active) return;

        _elapsed += Time.deltaTime;
        float t = Mathf.Clamp01(_elapsed / _duration);

        // Move tracer along path
        transform.position = Vector3.Lerp(_start, _end, t);

        if (t >= 1f)
        {
            ReturnToPool();
        }
    }

    private void ReturnToPool()
    {
        _active = false;

        if (trailRenderer)
            trailRenderer.enabled = false;
        if (lineRenderer)
            lineRenderer.enabled = false;

        _releaseAction?.Invoke(this);
        _releaseAction = null;
    }

    private void OnDisable()
    {
        _active = false;
    }
}

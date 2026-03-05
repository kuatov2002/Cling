using System.Collections.Generic;
using UnityEngine;
using Mirror;

/// <summary>
/// Client-side auto-aim assistance system.
/// Zero-GC: uses pre-allocated arrays and manual loops instead of LINQ.
/// </summary>
public class AutoAimSystem : NetworkBehaviour
{
    [SerializeField] private float aimRadius = 80f;
    [SerializeField] private float maxAimDistance = 100f;
    [SerializeField] private LayerMask targetLayerMask = -1;
    [SerializeField] private LayerMask obstacleLayerMask = -1;

    private Camera _playerCamera;
    private Team _myTeam;

    // ── Pre-allocated buffers (zero GC) ────────────────────────────
    private readonly Collider[] _overlapResults = new Collider[16];
    private readonly List<Transform> _targetCache = new List<Transform>(16);

    public override void OnStartLocalPlayer()
    {
        _playerCamera = Camera.main;
    }

    public Transform GetBestTarget(Vector3 aimDirection)
    {
        if (!isLocalPlayer || !_playerCamera) return null;

        // Cache team (avoids GetComponent every frame)
        var teamComp = GetComponent<PlayerTeam>();
        _myTeam = teamComp ? teamComp.CurrentTeam : Team.None;

        FindTargetsInRange();
        if (_targetCache.Count == 0) return null;

        Transform bestTarget = null;
        float bestScore = float.MaxValue;
        Vector3 screenCenter = new Vector3(Screen.width * 0.5f, Screen.height * 0.5f, 0f);

        for (int i = 0; i < _targetCache.Count; i++)
        {
            Transform target = _targetCache[i];
            if (!IsTargetValid(target)) continue;

            Vector3 screenPos = _playerCamera.WorldToScreenPoint(target.position);
            float distanceFromCenter = Vector2.Distance(
                new Vector2(screenPos.x, screenPos.y),
                new Vector2(screenCenter.x, screenCenter.y));

            if (distanceFromCenter <= aimRadius)
            {
                float score = CalculateTargetScore(target, aimDirection, distanceFromCenter);
                if (score < bestScore)
                {
                    bestScore = score;
                    bestTarget = target;
                }
            }
        }

        return bestTarget;
    }

    /// <summary>
    /// Find all valid targets in range. Zero-GC: uses NonAlloc + manual loops.
    /// </summary>
    private void FindTargetsInRange()
    {
        _targetCache.Clear();

        int count = Physics.OverlapSphereNonAlloc(
            transform.position, maxAimDistance, _overlapResults, targetLayerMask);

        for (int i = 0; i < count; i++)
        {
            Collider c = _overlapResults[i];
            if (c == null || c.transform == transform) continue;

            // Must have PlayerHealth (is a player)
            if (!c.TryGetComponent<PlayerHealth>(out _)) continue;

            // Skip teammates
            if (c.TryGetComponent<PlayerTeam>(out var targetTeam) &&
                _myTeam != Team.None &&
                targetTeam.CurrentTeam == _myTeam)
                continue;

            // Skip dead players
            if (c.TryGetComponent<PlayerState>(out var targetState) &&
                !targetState.IsAlive)
                continue;

            _targetCache.Add(c.transform);
        }
    }

    private bool IsTargetValid(Transform target)
    {
        Vector3 directionToTarget = (target.position - _playerCamera.transform.position).normalized;
        float distance = Vector3.Distance(_playerCamera.transform.position, target.position);

        return !Physics.Raycast(
            _playerCamera.transform.position, directionToTarget,
            distance, obstacleLayerMask);
    }

    private float CalculateTargetScore(Transform target, Vector3 aimDirection, float screenDistance)
    {
        Vector3 directionToTarget = (target.position - transform.position).normalized;
        float angleWeight = 1f - Vector3.Dot(aimDirection, directionToTarget);
        float distanceWeight = screenDistance / aimRadius;

        return angleWeight * 0.7f + distanceWeight * 0.3f;
    }

    public Vector3 GetAdjustedAimDirection(Vector3 originalDirection, Transform target)
    {
        if (!target) return originalDirection;
        return (target.position - _playerCamera.transform.position).normalized;
    }
}

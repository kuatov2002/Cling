using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Mirror;

public class AutoAimSystem : NetworkBehaviour
{
    private readonly float _aimRadius = 50f;
    private readonly float _maxAimDistance = 100f;
    [SerializeField] private LayerMask targetLayerMask = -1;
    [SerializeField] private LayerMask obstacleLayerMask = -1;
    
    private Camera playerCamera;
    private Transform currentTarget;
    
    public override void OnStartLocalPlayer()
    {
        playerCamera = Camera.main;
    }
    
    public Transform GetBestTarget(Vector3 aimDirection)
    {
        if (!isLocalPlayer) return null;
        
        var targets = FindTargetsInRange();
        if (targets.Count == 0) return null;
        
        Transform bestTarget = null;
        float bestScore = float.MaxValue;
        Vector3 screenCenter = new Vector3(Screen.width * 0.5f, Screen.height * 0.5f, 0f);
        
        foreach (var target in targets)
        {
            if (!IsTargetValid(target)) continue;
            
            Vector3 screenPos = playerCamera.WorldToScreenPoint(target.position);
            float distanceFromCenter = Vector2.Distance(screenPos, screenCenter);
            
            if (distanceFromCenter <= _aimRadius)
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
    
    private List<Transform> FindTargetsInRange()
    {
        var colliders = Physics.OverlapSphere(transform.position, _maxAimDistance, targetLayerMask);
        return colliders
            .Where(c => c.transform != transform && c.GetComponent<PlayerHealth>())
            .Select(c => c.transform)
            .ToList();
    }
    
    private bool IsTargetValid(Transform target)
    {
        Vector3 directionToTarget = (target.position - playerCamera.transform.position).normalized;
        float distance = Vector3.Distance(playerCamera.transform.position, target.position);
        
        return !Physics.Raycast(playerCamera.transform.position, directionToTarget, 
            distance, obstacleLayerMask);
    }
    
    private float CalculateTargetScore(Transform target, Vector3 aimDirection, float screenDistance)
    {
        Vector3 directionToTarget = (target.position - transform.position).normalized;
        float angleWeight = 1f - Vector3.Dot(aimDirection, directionToTarget);
        float distanceWeight = screenDistance / _aimRadius;
        
        return angleWeight * 0.7f + distanceWeight * 0.3f;
    }
    
    public Vector3 GetAdjustedAimDirection(Vector3 originalDirection, Transform target)
    {
        if (!target) return originalDirection;
        
        Vector3 targetDirection = (target.position - playerCamera.transform.position).normalized;
        return targetDirection;
    }
}
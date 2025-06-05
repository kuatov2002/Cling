using System;
using Mirror;
using UnityEngine;

public class PlayerRole : NetworkBehaviour
{
    [SerializeField] private GameObject sheriffStar;
    private PlayerState _player; 
    public PlayerState Player => _player;

    public event Action<RoleType> OnRoleChanged;

    [SyncVar(hook = nameof(OnRoleUpdated))]
    private RoleType _currentRole = RoleType.None;

    public RoleType CurrentRole
    {
        get => _currentRole;
        set
        {
            if (!isServer || _currentRole == value) return;
            _currentRole = value;
            // хук OnRoleUpdated автоматически вызовется на клиенте
            OnRoleChanged?.Invoke(value); // если нужен оповещать на сервере
        }
    }

    private void OnRoleUpdated(RoleType oldRole, RoleType newRole)
    {
        UpdateSheriffStarVisibility(newRole);
        if (isLocalPlayer && oldRole != newRole)
            OnRoleChanged?.Invoke(newRole);
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        _player = GetComponent<PlayerState>();
        UpdateSheriffStarVisibility(_currentRole);
    }

    public override void OnStartLocalPlayer()
    {
        base.OnStartLocalPlayer();
        OnRoleChanged += HandleRoleChanged;
        OnRoleChanged += UIManager.Instance.OnRoleChanged;
        OnRoleChanged?.Invoke(_currentRole);
    }

    private void HandleRoleChanged(RoleType newRole)
    {
        Debug.Log($"Player role changed to: {newRole}");
    }

    private void UpdateSheriffStarVisibility(RoleType role)
    {
        if (sheriffStar != null)
            sheriffStar.SetActive(role == RoleType.Sheriff);
    }

    private void OnDestroy()
    {
        if (isLocalPlayer)
        {
            OnRoleChanged -= HandleRoleChanged;
            if (UIManager.Instance != null)
                OnRoleChanged -= UIManager.Instance.OnRoleChanged;
        }
    }
}

public enum RoleType
{
    None,
    Sheriff,
    Deputy,
    Outlaw,
    Renegade
}
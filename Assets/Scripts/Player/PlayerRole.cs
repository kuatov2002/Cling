using System;
using Mirror;
using UnityEngine;

public class PlayerRole : NetworkBehaviour
{
    [SerializeField] private GameObject sheriffStar;

    private PlayerState _player; // Ссылка на PlayerState
    public PlayerState Player => _player; // Свойство для доступа к PlayerState

    public event Action<RoleType> OnRoleChanged;

    [SyncVar(hook = nameof(OnRoleUpdated))]
    private int _roleInt = 0;

    public RoleType CurrentRole
    {
        get => (RoleType)_roleInt;
        set
        {
            if (!isServer) return;

            if (CurrentRole != value)
            {
                _roleInt = (int)value;
                OnRoleChanged?.Invoke(value);
            }
        }
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        _player = GetComponent<PlayerState>(); // Получаем PlayerState
        UpdateSheriffStarVisibility((RoleType)_roleInt);
    }

    public override void OnStartLocalPlayer()
    {
        base.OnStartLocalPlayer();
        OnRoleChanged += HandleRoleChanged;
        OnRoleChanged += UIManager.Instance.OnRoleChanged;
    }

    private void HandleRoleChanged(RoleType newRole)
    {
        Debug.Log($"Player role changed to: {newRole}");
    }

    private void OnRoleUpdated(int oldValue, int newValue)
    {
        RoleType oldRole = (RoleType)oldValue;
        RoleType newRole = (RoleType)newValue;

        UpdateSheriffStarVisibility(newRole);

        if (isLocalPlayer && oldRole != newRole)
        {
            OnRoleChanged?.Invoke(newRole);
        }
    }

    private void UpdateSheriffStarVisibility(RoleType role)
    {
        if (sheriffStar != null)
        {
            sheriffStar.SetActive(role == RoleType.Sheriff);
        }
    }

    private void OnDestroy()
    {
        if (isLocalPlayer)
        {
            OnRoleChanged -= HandleRoleChanged;
            if (UIManager.Instance != null)
            {
                OnRoleChanged -= UIManager.Instance.OnRoleChanged;
            }
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
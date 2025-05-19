using Mirror;
using UnityEngine;
using System;

public class PlayerRole : NetworkBehaviour
{
    public event Action<RoleType> OnRoleChanged;

    public enum RoleType
    {
        None,
        Sheriff,
        Deputy,
        Outlaw,
        Renegade
    }

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

    public override void OnStartLocalPlayer()
    {
        OnRoleChanged += HandleRoleChanged;
        // Invoke initial role state
        OnRoleChanged?.Invoke(CurrentRole);
    }

    private void HandleRoleChanged(RoleType newRole)
    {
        Debug.Log($"Player role changed to: {newRole}");
        // Here you can update UI or other player characteristics based on the role
    }

    private void OnRoleUpdated(int oldValue, int newValue)
    {
        if (!isLocalPlayer) return;
        
        RoleType oldRole = (RoleType)oldValue;
        RoleType newRole = (RoleType)newValue;
        
        if (oldRole != newRole)
        {
            OnRoleChanged?.Invoke(newRole);
        }
    }
}
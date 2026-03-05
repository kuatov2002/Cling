using System;
using Mirror;
using UnityEngine;

public class PlayerState : NetworkBehaviour
{
    public event Action<State> OnStateChanged;

    public enum State
    {
        Alive,
        Cutscene,
        Dead
    }

    [SyncVar(hook = nameof(OnStateIntSynced))]
    private int _stateInt = 0;

    [SyncVar]
    private string _playerNickname = "";

    public State CurrentState
    {
        get => (State)_stateInt;
        set
        {
            if (CurrentState != value)
            {
                _stateInt = (int)value;
                // SyncVar hook fires automatically on clients via replication.
                // Fire event on server manually (hooks don't fire on server-side set).
                OnStateChanged?.Invoke(value);
            }
        }
    }

    /// <summary>
    /// SyncVar hook — fires on clients when _stateInt changes via server replication.
    /// Replaces the old RpcUpdateState pattern.
    /// </summary>
    private void OnStateIntSynced(int oldValue, int newValue)
    {
        OnStateChanged?.Invoke((State)newValue);
    }

    public bool IsAlive => CurrentState == State.Alive;
    public string PlayerNickname => _playerNickname;

    public override void OnStartLocalPlayer()
    {
        OnStateChanged += HandleStateChanged;
        OnStateChanged?.Invoke(CurrentState);

        // Set nickname from SaveData
        if (!string.IsNullOrEmpty(SaveData.Nickname))
        {
            CmdSetNickname(SaveData.Nickname);
        }
    }

    public override void OnStopLocalPlayer()
    {
        base.OnStopLocalPlayer();
        OnStateChanged -= HandleStateChanged;
    }

    private void HandleStateChanged(State newState)
    {
        Debug.Log($"Player state changed to: {newState}");
    }

    [Command]
    private void CmdSetNickname(string nickname)
    {
        _playerNickname = nickname;
        
        // Update visual on all clients
        PlayerVisual visual = GetComponent<PlayerVisual>();
        if (visual != null)
        {
            visual.SetPlayerNickname(nickname);
        }
    }

    // RpcUpdateState removed — SyncVar hook OnStateIntSynced handles client notification.

    public override void OnStartServer()
    {
        if (GameManager.Instance)
        {
            GameManager.Instance.RegisterPlayer(this);
        }
        else
        {
            Debug.LogError("GameManager instance is null when registering player");
        }
    }

    public override void OnStopServer()
    {
        if (GameManager.Instance)
        {
            GameManager.Instance.UnregisterPlayer(this);
        }
    }
    
    [Command]
    public void CmdSceneLoaded()
    {
        if (!isServer) return;

        Debug.Log($"Server received scene loaded from player {connectionToClient.connectionId}");
        GameManager.Instance?.OnServerReceivedClientSceneLoaded();
    }
}
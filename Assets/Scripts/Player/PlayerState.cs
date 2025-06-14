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

    [SyncVar]
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
                OnStateChanged?.Invoke(value);
                
                if (NetworkClient.active && NetworkClient.isConnected && isOwned)
                {
                    RpcUpdateState((int)value);
                }
            }
        }
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
        
        CmdConfirmSceneLoaded();
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

    [ClientRpc]
    private void RpcUpdateState(int state)
    {
        if (!isLocalPlayer)
        {
            _stateInt = state;
            OnStateChanged?.Invoke((State)state);
        }
    }

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
    private void CmdConfirmSceneLoaded()
    {
        Debug.Log("Client confirmed scene loaded");
        GameManager.Instance?.OnClientSceneLoaded();
    }

    [ClientRpc]
    private void RpcSceneLoaded() 
    {
        GameManager.Instance?.OnClientSceneLoaded();
    }
}
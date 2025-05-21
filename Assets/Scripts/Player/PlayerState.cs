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

    public State CurrentState
    {
        get => (State)_stateInt;
        set 
        { 
            if (CurrentState != value)
            {
                _stateInt = (int)value;
                OnStateChanged?.Invoke(value);
                
                // Only call RPC if we're connected to a server and we have authority
                if (NetworkClient.active && NetworkClient.isConnected && isOwned)
                {
                    RpcUpdateState((int)value);
                }
            }
        }
    }

    public override void OnStartLocalPlayer()
    {
        OnStateChanged += HandleStateChanged;
        OnStateChanged?.Invoke(CurrentState);
    }

    private void HandleStateChanged(State newState)
    {
        Debug.Log($"Player state changed to: {newState}");
    }

    [ClientRpc]
    private void RpcUpdateState(int state)
    {
        if (!isLocalPlayer)
        {
            // Prevent infinite recursion by updating _stateInt directly
            _stateInt = state;
            OnStateChanged?.Invoke((State)state);
        }
    }

    public override void OnStartServer()
    {
        if (GameManager.Instance != null)
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
        if (GameManager.Instance != null)
        {
            GameManager.Instance.UnregisterPlayer(this);
        }
    }
}
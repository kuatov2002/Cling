using System;
using Mirror;
using UnityEngine;

public class PlayerState : NetworkBehaviour
{
    public event Action<State> OnStateChanged; // Convention: capitalize event names

    public enum State
    {
        Alive,
        Cutscene,
        Dead
    }

    [SyncVar]
    private int _stateInt=0;

    public State CurrentState
    {
        get => (State)_stateInt;
        set 
        { 
            if (CurrentState != value)
            {
                _stateInt = (int)value;
                OnStateChanged?.Invoke(value);
                RpcUpdateState((int)value);
            }
        }
    }

    public override void OnStartLocalPlayer()
    {
        OnStateChanged += HandleStateChanged;
        OnStateChanged?.Invoke(CurrentState);
    }

    // Method to handle state changes
    private void HandleStateChanged(State newState)
    {
        Debug.Log($"Player state changed to: {newState}");
    }
    [ClientRpc]
    private void RpcUpdateState(int state)
    {
        if (!isLocalPlayer)
        {
            CurrentState = (State)state;
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
        GameManager.Instance.UnregisterPlayer(this);
    }
}

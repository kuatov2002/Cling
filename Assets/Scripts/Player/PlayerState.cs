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

    private State _currentState=State.Alive; // Renamed to avoid naming conflict with class

    public State CurrentState // Property name should differ from class name
    {
        get => _currentState; // Fixed syntax - curly braces instead of =>
        set 
        { 
            if (_currentState != value)
            {
                _currentState = value;
                OnStateChanged?.Invoke(_currentState); // Invoke event when state changes
            }
        }
    }

    public override void OnStartLocalPlayer()
    {
        if(!isLocalPlayer) return;
        OnStateChanged += HandleStateChanged;
        OnStateChanged?.Invoke(CurrentState);
    }

    // Method to handle state changes
    private void HandleStateChanged(State newState)
    {
        Debug.Log($"Player state changed to: {newState}");
    }
}

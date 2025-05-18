using System.Collections.Generic;
using Mirror;
using UnityEngine;

public class GameManager : NetworkBehaviour
{
    private List<PlayerState> _players= new List<PlayerState>();
    public static GameManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }
    
    [Server]
    public void RegisterPlayer(PlayerState player)
    {
        _players.Add(player);
        player.OnStateChanged += HandlePlayerStateChanged;
    }

    [Server]
    public void UnregisterPlayer(PlayerState player)
    {
        if (_players.Contains(player))
        {
            player.OnStateChanged -= HandlePlayerStateChanged;
            _players.Remove(player);
        }
    }

    private void HandlePlayerStateChanged(PlayerState.State newState)
    {
        if (newState == PlayerState.State.Dead)
        {
            CheckWinConditions();
        }
    }

    private void CheckWinConditions()
    {
        Debug.Log("Check");
    }

    [Server]
    private void ServerGameOver()
    {
        RpcGameOver();
    }

    [ClientRpc]
    private void RpcGameOver()
    {
        // Логика завершения игры на клиентах
        Debug.Log("Game Over");
    }
}
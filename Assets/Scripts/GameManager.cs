using System.Collections.Generic;
using System.Linq;
using Mirror;
using UnityEngine;

public class GameManager : NetworkBehaviour
{
    private List<PlayerState> _players= new List<PlayerState>();
    private List<PlayerRole> _playerRoles = new List<PlayerRole>();
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
        
        // Get the player's role component and register it
        PlayerRole playerRole = player.GetComponent<PlayerRole>();
        if (playerRole != null)
        {
            _playerRoles.Add(playerRole);
            
            // Assign roles if we have enough players
            if (_players.Count >= 3)
            {
                AssignPlayerRoles();
            }
        }
        else
        {
            Debug.LogError("PlayerRole component not found on player");
        }
        
        UpdateAllPlayerIndices();
    }

    [Server]
    public void UnregisterPlayer(PlayerState player)
    {
        if (_players.Contains(player))
        {
            player.OnStateChanged -= HandlePlayerStateChanged;
            _players.Remove(player);
            
            // Also remove the player role
            PlayerRole playerRole = player.GetComponent<PlayerRole>();
            if (playerRole != null && _playerRoles.Contains(playerRole))
            {
                _playerRoles.Remove(playerRole);
            }

            UpdateAllPlayerIndices();
        }
    }

    [Server]
    private void UpdateAllPlayerIndices()
    {
        // Find all PlayerVisual components and trigger their index updates
        foreach (PlayerState player in _players)
        {
            PlayerVisual visual = player.GetComponent<PlayerVisual>();
            if (visual != null)
            {
                // This will call the hook on all clients
                visual.SetPlayerIndex(GetPlayerIndex(player));
            }
        }
    }

    public int GetPlayerIndex(PlayerState player)
    {
        return _players.IndexOf(player);
    }

    [Server]
    private void AssignPlayerRoles()
    {
        // Don't assign roles if already assigned or not enough players
        if (_playerRoles.Count < 3)
            return;
        
        // Create a list to shuffle the roles
        List<RoleType> availableRoles = new List<RoleType>();
        
        // Add roles based on player count
        // Rules based on Bang! card game:
        // 4 players: 1 Sheriff, 1 Renegade, 2 Outlaws
        // 5 players: 1 Sheriff, 1 Renegade, 2 Outlaws, 1 Deputy
        // 6 players: 1 Sheriff, 1 Renegade, 3 Outlaws, 1 Deputy
        // 7 players: 1 Sheriff, 1 Renegade, 3 Outlaws, 2 Deputies
        
        int playerCount = _playerRoles.Count;
        
        // Always add one Sheriff
        availableRoles.Add(RoleType.Sheriff);
        
        // Renegades
        availableRoles.Add(RoleType.Renegade);
        
        
        // Outlaws
        int outlawCount = 0;
        if (playerCount == 4) outlawCount = 2;
        else if (playerCount <= 7) outlawCount = (playerCount - 2) / 2 + 1; // Formula for 5-7 players
        
        for (int i = 0; i < outlawCount; i++)
        {
            availableRoles.Add(RoleType.Outlaw);
        }
        
        // Deputies
        int deputyCount = 0;
        if (playerCount >= 5 && playerCount <= 6) deputyCount = 1;
        else if (playerCount == 7) deputyCount = 2;
        
        for (int i = 0; i < deputyCount; i++)
        {
            availableRoles.Add(RoleType.Deputy);
        }
        
        // Shuffle the roles
        availableRoles = availableRoles.OrderBy(_ => Random.value).ToList();
        
        // Assign roles to players
        for (int i = 0; i < _playerRoles.Count; i++)
        {
            _playerRoles[i].CurrentRole = availableRoles[i];
        }
        
        // Notify all players that roles have been assigned
        RpcRolesAssigned();
    }

    [ClientRpc]
    private void RpcRolesAssigned()
    {
        Debug.Log("All roles have been assigned!");
        // You could trigger UI elements to reveal roles here
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
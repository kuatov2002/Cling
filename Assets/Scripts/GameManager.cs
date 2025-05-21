using System.Collections.Generic;
using System.Linq;
using Mirror;
using UnityEngine;

public class GameManager : NetworkBehaviour
{
    private List<PlayerState> _players;
    private List<PlayerRole> _playerRoles;

    private Dictionary<NetworkConnection, int> _playerStableIndices;
    private int _nextPlayerIndex = 0;

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
        _playerStableIndices = new Dictionary<NetworkConnection, int>();
        _players = new List<PlayerState>();
        _playerRoles = new List<PlayerRole>();
        
        RoomManager.HostStopped+= OnNetworkStopped;
        RoomManager.ClientStopped+= OnNetworkStopped;
    }

    [Server]
    public void RegisterPlayer(PlayerState player)
    {
        _players.Add(player);
        player.OnStateChanged += HandlePlayerStateChanged;

        if (!_playerStableIndices.ContainsKey(player.connectionToClient))
        {
            _playerStableIndices[player.connectionToClient] = _nextPlayerIndex++;
        }

        PlayerRole playerRole = player.GetComponent<PlayerRole>();
        if (playerRole != null)
        {
            _playerRoles.Add(playerRole);
        
            // Only assign roles if we have enough players to do so
            if (_playerRoles.Count >= 4)
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

            PlayerRole playerRole = player.GetComponent<PlayerRole>();
            if (playerRole != null && _playerRoles.Contains(playerRole))
            {
                _playerRoles.Remove(playerRole);
            }
        }

        UpdateAllPlayerIndices();
    }

    [Server]
    private void UpdateAllPlayerIndices()
    {
        foreach (PlayerState player in _players)
        {
            PlayerVisual visual = player.GetComponent<PlayerVisual>();
            if (visual != null)
            {
                visual.SetPlayerIndex(GetPlayerStableIndex(player));
            }
        }
    }

    public int GetPlayerStableIndex(PlayerState player)
    {
        if (player == null || player.connectionToClient == null)
            return -1;

        if (_playerStableIndices.TryGetValue(player.connectionToClient, out int index))
        {
            return index;
        }

        return -1;
    }

    [Server]
    private void AssignPlayerRoles()
    {
        List<RoleType> availableRoles = new List<RoleType>();
        int playerCount = _playerRoles.Count;

        availableRoles.Add(RoleType.Sheriff);
        availableRoles.Add(RoleType.Renegade);

        int outlawCount = playerCount switch
        {
            4 => 2,
            5 => 2,
            6 => 3,
            7 => 3,
            _ => 0
        };

        for (int i = 0; i < outlawCount; i++)
        {
            availableRoles.Add(RoleType.Outlaw);
        }

        int deputyCount = playerCount switch
        {
            5 => 1,
            6 => 1,
            7 => 2,
            _ => 0
        };

        for (int i = 0; i < deputyCount; i++)
        {
            availableRoles.Add(RoleType.Deputy);
        }

        availableRoles = availableRoles.OrderBy(_ => Random.value).ToList();

        for (int i = 0; i < _playerRoles.Count; i++)
        {
            _playerRoles[i].CurrentRole = availableRoles[i];
        }

        RpcRolesAssigned();
    }

    [ClientRpc]
    private void RpcRolesAssigned()
    {
        Debug.Log("All roles have been assigned!");
    }

    private void HandlePlayerStateChanged(PlayerState.State newState)
    {
        if (newState == PlayerState.State.Dead)
        {
            CheckWinConditions();
        }
    }

    [Server]
    private void CheckWinConditions()
    {
        bool sheriffAlive = _playerRoles.Any(r => r.CurrentRole == RoleType.Sheriff && r.Player.IsAlive);

        if (!sheriffAlive)
        {
            ServerGameOver("Outlaws");
            return;
        }

        bool anyOutlawAlive = _playerRoles.Any(r => r.CurrentRole == RoleType.Outlaw && r.Player.IsAlive);
        bool anyRenegadeAlive = _playerRoles.Any(r => r.CurrentRole == RoleType.Renegade && r.Player.IsAlive);

        if (!anyOutlawAlive && !anyRenegadeAlive)
        {
            ServerGameOver("Sheriff");
            return;
        }

        int totalAlive = _playerRoles.Count(r => r.Player.IsAlive);

        if (totalAlive == 1)
        {
            var lastPlayer = _playerRoles.FirstOrDefault(r => r.Player.IsAlive);
            if (lastPlayer?.CurrentRole == RoleType.Renegade)
            {
                ServerGameOver("Renegade");
                return;
            }
        }
    }
    [Server]
    private void ServerGameOver(string winningTeam)
    {
        RpcGameOver(winningTeam);
    }

    [ClientRpc]
    private void RpcGameOver(string winningTeam)
    {
        Debug.Log($"{winningTeam} have won the game!");
        UIManager.Instance.Gameover(winningTeam);
        // Здесь можно добавить логику отображения результата, рестарт игры или переход на экран победы
    }
    
    private void OnNetworkStopped()
    {
        
    }
}
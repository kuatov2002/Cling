using System.Collections.Generic;
using System.Linq;
using Mirror;
using UnityEngine;
using Random = UnityEngine.Random;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    [Header("Game Configuration")]
    [SerializeField] private float gameStartDelay = 3f;
    [SerializeField] private bool autoAssignRoles = true;
    
    [SerializeField] private GameObject awakeObject;

    // Player Management
    private readonly List<PlayerState> _players = new();
    private readonly List<PlayerRole> _playerRoles = new();
    private readonly Dictionary<NetworkConnection, int> _playerStableIndices = new();
    private int _nextPlayerIndex = 0;

    // Game State
    private GameState _currentGameState = GameState.Waiting;
    private bool _gameInProgress = false;

    public enum GameState
    {
        Waiting,
        Starting,
        InProgress,
        Ended
    }

    #region Unity Lifecycle

    private void Awake()
    {
        InitializeSingleton();
        Debug.Log("GameManager Awake");
    }

    private void Start()
    {
        SubscribeToNetworkEvents();
        if (awakeObject) awakeObject.SetActive(true);
    }

    private void OnDestroy()
    {
        UnsubscribeFromNetworkEvents();
    }

    #endregion

    #region Initialization

    private void InitializeSingleton()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void SubscribeToNetworkEvents()
    {
        RoomManager.HostStopped += OnNetworkStopped;
        RoomManager.ClientStopped += OnNetworkStopped;
        RoomManager.GameStarted += OnGameStarted;
        RoomManager.PlayerDisconnected += OnPlayerDisconnected;
    }

    private void UnsubscribeFromNetworkEvents()
    {
        RoomManager.HostStopped -= OnNetworkStopped;
        RoomManager.ClientStopped -= OnNetworkStopped;
        RoomManager.GameStarted -= OnGameStarted;
        RoomManager.PlayerDisconnected -= OnPlayerDisconnected;
    }

    #endregion

    #region Network Events

    private void OnGameStarted()
    {
        Debug.Log("Game started event received");
        _currentGameState = GameState.Starting;
        
        Invoke(nameof(InitializeGame), gameStartDelay);
    }

    private void OnPlayerDisconnected(NetworkConnection conn)
    {
        HandlePlayerDisconnection(conn);
    }

    private void OnNetworkStopped()
    {
        ResetGameState();
    }

    #endregion

    #region Player Management

    public void RegisterPlayer(PlayerState player)
    {
        if (_players.Contains(player)) return;

        _players.Add(player);
        player.OnStateChanged += HandlePlayerStateChanged;

        AssignStableIndex(player);
        RegisterPlayerRole(player);
        UpdateAllPlayerIndices();

        Debug.Log($"Player registered. Total players: {_players.Count}");

        if (autoAssignRoles && CanAssignRoles())
        {
            AssignPlayerRoles();
        }
    }

    public void UnregisterPlayer(PlayerState player)
    {
        if (!_players.Contains(player)) return;

        player.OnStateChanged -= HandlePlayerStateChanged;
        _players.Remove(player);

        UnregisterPlayerRole(player);
        UpdateAllPlayerIndices();

        Debug.Log($"Player unregistered. Remaining players: {_players.Count}");

        if (_gameInProgress)
        {
            CheckWinConditions();
        }
    }

    private void AssignStableIndex(PlayerState player)
    {
        if (player.connectionToClient == null) return;

        if (!_playerStableIndices.ContainsKey(player.connectionToClient))
        {
            _playerStableIndices[player.connectionToClient] = _nextPlayerIndex++;
        }
    }

    private void RegisterPlayerRole(PlayerState player)
    {
        PlayerRole playerRole = player.GetComponent<PlayerRole>();
        if (playerRole != null && !_playerRoles.Contains(playerRole))
        {
            _playerRoles.Add(playerRole);
        }
        else if (playerRole == null)
        {
            Debug.LogError("PlayerRole component not found on player");
        }
    }

    private void UnregisterPlayerRole(PlayerState player)
    {
        PlayerRole playerRole = player.GetComponent<PlayerRole>();
        if (playerRole != null && _playerRoles.Contains(playerRole))
        {
            _playerRoles.Remove(playerRole);
        }
    }

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

    private void HandlePlayerDisconnection(NetworkConnection conn)
    {
        PlayerState disconnectedPlayer = _players.FirstOrDefault(p => p.connectionToClient == conn);
        if (disconnectedPlayer != null)
        {
            UnregisterPlayer(disconnectedPlayer);
        }

        if (_playerStableIndices.ContainsKey(conn))
        {
            _playerStableIndices.Remove(conn);
        }
    }

    public int GetPlayerStableIndex(PlayerState player)
    {
        if (player?.connectionToClient == null) return -1;
        
        return _playerStableIndices.TryGetValue(player.connectionToClient, out int index) ? index : -1;
    }

    #endregion

    #region Role Assignment

    private bool CanAssignRoles()
    {
        return _playerRoles.Count >= 4 && _currentGameState != GameState.Waiting;
    }
    
    private void AssignPlayerRoles()
    {
        if (!CanAssignRoles()) return;

        List<RoleType> availableRoles = GenerateRoleDistribution(_playerRoles.Count);
        ShuffleRoles(availableRoles);

        for (int i = 0; i < _playerRoles.Count && i < availableRoles.Count; i++)
        {
            _playerRoles[i].CurrentRole = availableRoles[i];
        }

        Debug.Log($"Roles assigned to {_playerRoles.Count} players");
        NetworkGameEvents.Instance?.RpcRolesAssigned();
    }
    
    private List<RoleType> GenerateRoleDistribution(int playerCount)
    {
        List<RoleType> roles = new();

        roles.Add(RoleType.Sheriff);
        roles.Add(RoleType.Renegade);

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
            roles.Add(RoleType.Outlaw);
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
            roles.Add(RoleType.Deputy);
        }

        return roles;
    }
    
    private void ShuffleRoles(List<RoleType> roles)
    {
        for (int i = 0; i < roles.Count; i++)
        {
            int randomIndex = Random.Range(i, roles.Count);
            (roles[i], roles[randomIndex]) = (roles[randomIndex], roles[i]);
        }
    }

    #endregion

    #region Game State Management
    
    private void InitializeGame()
    {
        _currentGameState = GameState.InProgress;
        _gameInProgress = true;

        if (autoAssignRoles && CanAssignRoles())
        {
            AssignPlayerRoles();
        }

        NetworkGameEvents.Instance?.RpcGameInitialized();
        Debug.Log("Game initialized and in progress");
    }
    
    private void HandlePlayerStateChanged(PlayerState.State newState)
    {
        if (!_gameInProgress) return;

        if (newState == PlayerState.State.Dead)
        {
            Debug.Log("Player died, checking win conditions");
            CheckWinConditions();
        }
    }
    
    private void CheckWinConditions()
    {
        if (!_gameInProgress || _currentGameState != GameState.InProgress) return;

        var aliveRoles = _playerRoles.Where(r => r.Player.IsAlive).ToList();

        bool sheriffAlive = aliveRoles.Any(r => r.CurrentRole == RoleType.Sheriff);
        if (!sheriffAlive)
        {
            EndGame("Outlaws");
            return;
        }

        bool anyOutlawAlive = aliveRoles.Any(r => r.CurrentRole == RoleType.Outlaw);
        bool anyRenegadeAlive = aliveRoles.Any(r => r.CurrentRole == RoleType.Renegade);

        if (!anyOutlawAlive && !anyRenegadeAlive)
        {
            EndGame("Sheriff");
            return;
        }

        if (aliveRoles.Count == 1)
        {
            var lastPlayer = aliveRoles.FirstOrDefault();
            if (lastPlayer?.CurrentRole == RoleType.Renegade)
            {
                EndGame("Renegade");
                return;
            }
        }
    }
    
    private void EndGame(string winningTeam)
    {
        _currentGameState = GameState.Ended;
        _gameInProgress = false;

        Debug.Log($"Game ended. Winner: {winningTeam}");
        NetworkGameEvents.Instance?.RpcGameOver(winningTeam);
    }

    public void ForceEndGame()
    {
        if (_gameInProgress)
        {
            EndGame("Game Terminated");
        }
    }

    private void ResetGameState()
    {
        _currentGameState = GameState.Waiting;
        _gameInProgress = false;
        _players.Clear();
        _playerRoles.Clear();
        _playerStableIndices.Clear();
        _nextPlayerIndex = 0;

        Debug.Log("Game state reset");
    }

    #endregion

    #region Public API

    public bool IsGameInProgress => _gameInProgress;
    public GameState CurrentGameState => _currentGameState;
    public int PlayerCount => _players.Count;
    public int AlivePlayerCount => _players.Count(p => p.IsAlive);

    public List<PlayerState> GetAlivePlayers()
    {
        return _players.Where(p => p.IsAlive).ToList();
    }

    public List<PlayerRole> GetPlayersWithRole(RoleType role)
    {
        return _playerRoles.Where(r => r.CurrentRole == role).ToList();
    }

    #endregion
    
}
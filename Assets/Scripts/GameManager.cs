using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Mirror;
using UnityEngine;

public class GameManager : MonoBehaviour // <-- Важно: NetworkBehaviour!
{
    public static GameManager Instance;

    [Header("Game Configuration")]
    [SerializeField] private float gameStartDelay = 3f;

    // Player Management
    private readonly List<PlayerState> _players = new();
    private readonly List<PlayerRole> _playerRoles = new();
    private readonly Dictionary<NetworkConnectionToClient, int> _playerStableIndices = new();
    private int _nextPlayerIndex = 0;

    // Game State
    private GameState _currentGameState = GameState.Waiting;
    private bool _gameInProgress = false;
    private bool _gameInitialized = false; // Защита от повторной инициализации

    private int _clientsSceneLoadedCount = 0;

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

        Application.targetFrameRate = 60;
        QualitySettings.vSyncCount = 1;
    }

    private void Start()
    {
        SubscribeToNetworkEvents();
    }

    private void OnDestroy()
    {
        UnsubscribeFromNetworkEvents();
    }

    #endregion

    #region Initialization

    private void InitializeSingleton()
    {
        if (Instance && Instance != this)
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

        // Спавним NetworkGameEvents, если его ещё нет
        SpawnNetworkGameEvents();

        // Ждём, пока все клиенты загрузят сцену — они сами сообщат через CmdSceneLoaded
        // Задержка gameStartDelay будет применена ПОСЛЕ загрузки всех сцен
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

        // Роли назначаем только после загрузки сцен всеми — не здесь!
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
        if (playerRole && !_playerRoles.Contains(playerRole))
        {
            _playerRoles.Add(playerRole);
        }
        else if (!playerRole)
        {
            Debug.LogError("PlayerRole component not found on player");
        }
    }

    private void UnregisterPlayerRole(PlayerState player)
    {
        PlayerRole playerRole = player.GetComponent<PlayerRole>();
        if (playerRole && _playerRoles.Contains(playerRole))
        {
            _playerRoles.Remove(playerRole);
        }
    }

    private void UpdateAllPlayerIndices()
    {
        foreach (PlayerState player in _players)
        {
            PlayerVisual visual = player.GetComponent<PlayerVisual>();
            if (visual)
            {
                visual.SetPlayerNickname(player.PlayerNickname);
            }
        }
    }

    private void HandlePlayerDisconnection(NetworkConnection conn)
    {
        PlayerState disconnectedPlayer = _players.FirstOrDefault(p => p.connectionToClient == conn);
        if (disconnectedPlayer)
        {
            UnregisterPlayer(disconnectedPlayer);
        }

        if (conn is NetworkConnectionToClient toClient)
            _playerStableIndices.Remove(toClient);
    }

    #endregion

    #region Role Assignment

    private bool CanAssignRoles()
    {
        return _playerRoles.Count >= 4 && _currentGameState != GameState.Waiting;
    }

    private void AssignPlayerRoles()
    {
        if (!NetworkServer.active) return;
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
        List<RoleType> roles = new()
        {
            RoleType.Sheriff,
            RoleType.Renegade
        };

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
        if (!NetworkServer.active) return;
        _gameInitialized = true;

        _currentGameState = GameState.InProgress;
        _gameInProgress = true;

        NetworkGameEvents.Instance?.RpcGameInitialized();

        Debug.Log("Game initialized and in progress");
    }

    // Вызывается сервером, когда клиент сообщает, что загрузил сцену
    public void OnServerReceivedClientSceneLoaded()
    {
        if (!NetworkServer.active) return;

        _clientsSceneLoadedCount++;
        Debug.Log($"Clients loaded scene: {_clientsSceneLoadedCount}/{_players.Count}");

        // Ждём, пока все загрузятся
        if (_clientsSceneLoadedCount >= _players.Count && CanAssignRoles())
        {
            AssignPlayerRoles();
            StartCoroutine(StartGameWithDelay());
        }
    }

    private IEnumerator StartGameWithDelay()
    {
        yield return new WaitForSeconds(gameStartDelay);
        InitializeGame();
    }

    private void HandlePlayerStateChanged(PlayerState.State newState)
    {
        if (!_gameInProgress || !NetworkServer.active) return;

        if (newState == PlayerState.State.Dead)
        {
            Debug.Log("Player died, checking win conditions");
            CheckWinConditions();
        }
    }

    private void CheckWinConditions()
    {
        if (!_gameInProgress || _currentGameState != GameState.InProgress || !NetworkServer.active) return;

        var aliveRoles = _playerRoles.Where(r => r.Player.IsAlive).ToList();

        bool sheriffAlive = aliveRoles.Any(r => r.CurrentRole == RoleType.Sheriff);
        bool anyOutlawAlive = aliveRoles.Any(r => r.CurrentRole == RoleType.Outlaw);
        bool anyRenegadeAlive = aliveRoles.Any(r => r.CurrentRole == RoleType.Renegade);

        // Check Renegade victory first (must be alone)
        if (aliveRoles.Count == 1 && anyRenegadeAlive)
        {
            EndGame("Renegade");
            return;
        }

        // Check Sheriff death (Outlaws and Renegades win)
        if (!sheriffAlive)
        {
            EndGame("Outlaws");
            return;
        }

        // Check Sheriff victory (no Outlaws or Renegades left)
        if (!anyOutlawAlive && !anyRenegadeAlive)
        {
            EndGame("Sheriff");
        }
    }

    private void EndGame(string winningTeam)
    {
        if (!NetworkServer.active) return;

        _currentGameState = GameState.Ended;
        _gameInProgress = false;

        Debug.Log($"Game ended. Winner: {winningTeam}");

        // Collect all player roles for the end game display
        List<PlayerRoleInfo> allPlayerRoles = new();
        foreach (var playerRole in _playerRoles)
        {
            if (playerRole.Player)
            {
                allPlayerRoles.Add(new PlayerRoleInfo
                {
                    playerName = playerRole.Player.PlayerNickname,
                    role = playerRole.CurrentRole
                });
            }
        }

        NetworkGameEvents.Instance?.RpcGameOver(winningTeam, allPlayerRoles.ToArray());
    }

    private void ResetGameState()
    {
        _currentGameState = GameState.Waiting;
        _gameInProgress = false;
        _gameInitialized = false;
        _clientsSceneLoadedCount = 0;
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

    #region Network Setup

    private void SpawnNetworkGameEvents()
    {
        if (!NetworkServer.active) return;

        if (FindObjectOfType<NetworkGameEvents>() == null)
        {
            GameObject go = new GameObject("NetworkGameEvents");
            go.AddComponent<NetworkGameEvents>();
            go.AddComponent<NetworkIdentity>();
            DontDestroyOnLoad(go);
            NetworkServer.Spawn(go);
            Debug.Log("NetworkGameEvents spawned on server");
        }
    }

    #endregion
}

[System.Serializable]
public struct PlayerRoleInfo
{
    public string playerName;
    public RoleType role;
}
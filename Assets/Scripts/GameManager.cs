using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Mirror;
using UnityEngine;

public class GameManager : NetworkBehaviour
{
    public static GameManager Instance;

    [Header("Match Configuration")]
    [SerializeField] private float matchDuration = 300f;
    [SerializeField] private int killsToWin = 25;
    [SerializeField] private float respawnDelay = 3f;
    [SerializeField] private float gameStartDelay = 3f;
    [SerializeField] private int minPlayersToStart = 1;

    // Player Management
    private readonly List<PlayerState> _players = new();
    private readonly List<PlayerTeam> _playerTeams = new();
    private readonly Dictionary<NetworkConnectionToClient, int> _playerStableIndices = new();
    private int _nextPlayerIndex = 0;

    // Match State (synced to all clients)
    [SyncVar(hook = nameof(OnMatchTimeChanged))]
    private float _matchTimeRemaining;

    [SyncVar(hook = nameof(OnRedKillsChanged))]
    private int _redTeamKills;

    [SyncVar(hook = nameof(OnBlueKillsChanged))]
    private int _blueTeamKills;

    // Game State
    private GameState _currentGameState = GameState.Waiting;
    private bool _gameInProgress = false;
    private bool _gameInitialized = false;
    private bool _timerRunning = false;
    private int _clientsSceneLoadedCount = 0;

    // Spawn Points
    private List<TeamSpawnPoint> _redSpawnPoints = new();
    private List<TeamSpawnPoint> _blueSpawnPoints = new();

    public float RespawnDelay => respawnDelay;

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
        if (Instance && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        // Framerate uncapped — simulation is tick-based (64 Hz) via NetworkTickManager
        // VSync off for minimum input latency
        QualitySettings.vSyncCount = 0;
    }

    private void Start()
    {
        SubscribeToNetworkEvents();
        CollectSpawnPoints();

        // GameManager only exists in the game scene,
        // so if we're here, the room transition already happened.
        // The GameStarted event fires BEFORE the scene loads,
        // so we'd miss it. Set state to Starting immediately.
        if (isServer)
        {
            _currentGameState = GameState.Starting;
            NetworkTickManager.OnTick += OnServerTick;
        }
    }

    /// <summary>
    /// Server-side tick update. Timer is tick-based (deterministic, frame-rate independent).
    /// </summary>
    private void OnServerTick(int tick)
    {
        if (!isServer || !_timerRunning) return;

        _matchTimeRemaining -= NetworkTickManager.TickDuration;
        if (_matchTimeRemaining <= 0f)
        {
            _matchTimeRemaining = 0f;
            _timerRunning = false;
            EndGameByTimeout();
        }

        // SyncVar hooks don't fire on the server — update UI manually on host
        UIManager.Instance?.UpdateMatchTimer(_matchTimeRemaining);
    }

    private void Update()
    {
        // Update() is now empty on server — timer logic moved to OnServerTick
    }

    private void OnDestroy()
    {
        UnsubscribeFromNetworkEvents();
        NetworkTickManager.OnTick -= OnServerTick;
        if (Instance == this) Instance = null;
    }

    #endregion

    #region Initialization

    private void SubscribeToNetworkEvents()
    {
        RoomManager.HostStopped += OnNetworkStopped;
        RoomManager.ClientStopped += OnNetworkStopped;
        RoomManager.PlayerDisconnected += OnPlayerDisconnected;
    }

    private void UnsubscribeFromNetworkEvents()
    {
        RoomManager.HostStopped -= OnNetworkStopped;
        RoomManager.ClientStopped -= OnNetworkStopped;
        RoomManager.PlayerDisconnected -= OnPlayerDisconnected;
    }

    private void CollectSpawnPoints()
    {
        var allSpawns = FindObjectsByType<TeamSpawnPoint>(FindObjectsSortMode.None);
        _redSpawnPoints = allSpawns.Where(s => s.team == Team.Red).ToList();
        _blueSpawnPoints = allSpawns.Where(s => s.team == Team.Blue).ToList();
    }

    #endregion

    #region Network Events

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
        RegisterPlayerTeam(player);
        UpdateAllPlayerIndices();

        Debug.Log($"Player registered. Total players: {_players.Count}");
    }

    public void UnregisterPlayer(PlayerState player)
    {
        if (!_players.Contains(player)) return;

        player.OnStateChanged -= HandlePlayerStateChanged;
        _players.Remove(player);

        UnregisterPlayerTeam(player);
        UpdateAllPlayerIndices();

        Debug.Log($"Player unregistered. Remaining players: {_players.Count}");
    }

    private void AssignStableIndex(PlayerState player)
    {
        if (player.connectionToClient == null) return;

        if (!_playerStableIndices.ContainsKey(player.connectionToClient))
        {
            _playerStableIndices[player.connectionToClient] = _nextPlayerIndex++;
        }
    }

    private void RegisterPlayerTeam(PlayerState player)
    {
        PlayerTeam playerTeam = player.GetComponent<PlayerTeam>();
        if (playerTeam && !_playerTeams.Contains(playerTeam))
        {
            _playerTeams.Add(playerTeam);
        }
        else if (!playerTeam)
        {
            Debug.LogError("PlayerTeam component not found on player");
        }
    }

    private void UnregisterPlayerTeam(PlayerState player)
    {
        PlayerTeam playerTeam = player.GetComponent<PlayerTeam>();
        if (playerTeam && _playerTeams.Contains(playerTeam))
        {
            _playerTeams.Remove(playerTeam);
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

    #region Team Assignment

    private bool CanStartGame()
    {
        return _playerTeams.Count >= minPlayersToStart && _currentGameState != GameState.Waiting;
    }

    [Server]
    private void AssignPlayerTeams()
    {
        if (!NetworkServer.active) return;
        if (!CanStartGame()) return;

        int redCount = 0;
        int blueCount = 0;

        for (int i = 0; i < _playerTeams.Count; i++)
        {
            // Alternate: assign to team with fewer players
            Team assignedTeam;
            if (redCount <= blueCount)
            {
                assignedTeam = Team.Red;
                redCount++;
            }
            else
            {
                assignedTeam = Team.Blue;
                blueCount++;
            }

            _playerTeams[i].CurrentTeam = assignedTeam;
        }

        Debug.Log($"Teams assigned: {redCount} Red, {blueCount} Blue");
    }

    #endregion

    #region Game State Management

    private void InitializeGame()
    {
        if (!NetworkServer.active) return;
        _gameInitialized = true;

        _currentGameState = GameState.InProgress;
        _gameInProgress = true;

        StartMatchTimer();

        NetworkGameEvents.Instance?.RpcGameInitialized();

        Debug.Log("Game initialized and in progress");
    }

    [Server]
    private void StartMatchTimer()
    {
        _matchTimeRemaining = matchDuration;
        _timerRunning = true;
    }

    public void OnServerReceivedClientSceneLoaded()
    {
        if (!NetworkServer.active) return;

        _clientsSceneLoadedCount++;
        Debug.Log($"Clients loaded scene: {_clientsSceneLoadedCount}/{_players.Count}");

        if (_clientsSceneLoadedCount >= _players.Count && CanStartGame())
        {
            AssignPlayerTeams();
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
        // No longer check win conditions on death — kills are tracked explicitly
    }

    #endregion

    #region Kill Tracking & Win Conditions

    [Server]
    public void OnPlayerKilled(Team killerTeam)
    {
        if (!_gameInProgress) return;

        if (killerTeam == Team.Red)
            _redTeamKills++;
        else if (killerTeam == Team.Blue)
            _blueTeamKills++;

        // SyncVar hooks don't fire on the server — update UI manually on host
        UIManager.Instance?.UpdateTeamScores(_redTeamKills, _blueTeamKills);

        CheckWinConditions();
    }

    [Server]
    private void CheckWinConditions()
    {
        if (!_gameInProgress || _currentGameState != GameState.InProgress) return;

        if (_redTeamKills >= killsToWin)
            EndGame("Red Team");
        else if (_blueTeamKills >= killsToWin)
            EndGame("Blue Team");
    }

    [Server]
    private void EndGameByTimeout()
    {
        if (!_gameInProgress) return;

        if (_redTeamKills > _blueTeamKills)
            EndGame("Red Team");
        else if (_blueTeamKills > _redTeamKills)
            EndGame("Blue Team");
        else
            EndGame("Draw");
    }

    private void EndGame(string winningTeam)
    {
        if (!NetworkServer.active) return;

        _currentGameState = GameState.Ended;
        _gameInProgress = false;
        _timerRunning = false;

        Debug.Log($"Game ended. Winner: {winningTeam}");

        List<PlayerMatchStat> allStats = new();
        foreach (var playerTeam in _playerTeams)
        {
            PlayerState playerState = playerTeam.GetComponent<PlayerState>();
            PlayerStats playerStats = playerTeam.GetComponent<PlayerStats>();
            if (playerState)
            {
                allStats.Add(new PlayerMatchStat
                {
                    playerName = playerState.PlayerNickname,
                    team = playerTeam.CurrentTeam,
                    kills = playerStats ? playerStats.Kills : 0,
                    deaths = playerStats ? playerStats.Deaths : 0
                });
            }
        }

        NetworkGameEvents.Instance?.RpcGameOver(winningTeam, allStats.ToArray());
    }

    private void ResetGameState()
    {
        _currentGameState = GameState.Waiting;
        _gameInProgress = false;
        _gameInitialized = false;
        _timerRunning = false;
        _clientsSceneLoadedCount = 0;
        _redTeamKills = 0;
        _blueTeamKills = 0;
        _players.Clear();
        _playerTeams.Clear();
        _playerStableIndices.Clear();
        _nextPlayerIndex = 0;

        Debug.Log("Game state reset");
    }

    #endregion

    #region Spawn Points

    public Transform GetTeamSpawnPoint(Team team)
    {
        var points = team == Team.Red ? _redSpawnPoints : _blueSpawnPoints;
        if (points.Count == 0)
        {
            // Fallback: use any available spawn point
            var allPoints = _redSpawnPoints.Concat(_blueSpawnPoints).ToList();
            if (allPoints.Count > 0)
                return allPoints[Random.Range(0, allPoints.Count)].transform;

            Debug.LogWarning("No team spawn points found! Using world origin.");
            return null;
        }

        return points[Random.Range(0, points.Count)].transform;
    }

    #endregion

    #region SyncVar Hooks

    private void OnMatchTimeChanged(float oldTime, float newTime)
    {
        UIManager.Instance?.UpdateMatchTimer(newTime);
    }

    private void OnRedKillsChanged(int oldKills, int newKills)
    {
        UIManager.Instance?.UpdateTeamScores(_redTeamKills, _blueTeamKills);
    }

    private void OnBlueKillsChanged(int oldKills, int newKills)
    {
        UIManager.Instance?.UpdateTeamScores(_redTeamKills, _blueTeamKills);
    }

    #endregion

    #region Public API

    public bool IsGameInProgress => _gameInProgress;
    public GameState CurrentGameState => _currentGameState;
    public int PlayerCount => _players.Count;
    public int RedTeamKills => _redTeamKills;
    public int BlueTeamKills => _blueTeamKills;

    // ── Zero-GC pre-allocated result lists ──────────────────────────
    private readonly List<PlayerState> _alivePlayersCache = new();
    private readonly List<PlayerTeam> _teamPlayersCache = new();

    public int AlivePlayerCount
    {
        get
        {
            int count = 0;
            for (int i = 0; i < _players.Count; i++)
                if (_players[i].IsAlive) count++;
            return count;
        }
    }

    /// <summary>
    /// Returns alive players. WARNING: returned list is shared/reused — do not cache the reference.
    /// </summary>
    public List<PlayerState> GetAlivePlayers()
    {
        _alivePlayersCache.Clear();
        for (int i = 0; i < _players.Count; i++)
            if (_players[i].IsAlive) _alivePlayersCache.Add(_players[i]);
        return _alivePlayersCache;
    }

    /// <summary>
    /// Returns players on a team. WARNING: returned list is shared/reused — do not cache the reference.
    /// </summary>
    public List<PlayerTeam> GetPlayersOnTeam(Team team)
    {
        _teamPlayersCache.Clear();
        for (int i = 0; i < _playerTeams.Count; i++)
            if (_playerTeams[i].CurrentTeam == team) _teamPlayersCache.Add(_playerTeams[i]);
        return _teamPlayersCache;
    }

    #endregion
}

[System.Serializable]
public struct PlayerMatchStat
{
    public string playerName;
    public Team team;
    public int kills;
    public int deaths;
}

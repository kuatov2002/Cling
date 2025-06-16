using System.Collections.Generic;
using Mirror;
using UnityEngine;

public class RoomManager : NetworkRoomManager
{
    public static event System.Action HostStopped;
    public static event System.Action ClientStopped;
    public static event System.Action<NetworkConnection> PlayerConnected;
    public static event System.Action<NetworkConnection> PlayerDisconnected;
    public static event System.Action GameStarted;
    public static event System.Action<int> PlayersCountChanged;

    [SerializeField] private int maxPlayers = 7;

    private readonly Dictionary<NetworkConnection, NetworkRoomPlayer> _roomPlayers = new();
    private int _currentPlayerCount = 0;

    [SerializeField] private List<CharacterData> characters = new List<CharacterData>();
    
    public override void Awake()
    {
        base.Awake();
        maxConnections = maxPlayers;
    }

    #region Server Callbacks

    public override void OnRoomServerConnect(NetworkConnectionToClient conn)
    {
        base.OnRoomServerConnect(conn);
        
        if (_currentPlayerCount >= maxPlayers)
        {
            conn.Disconnect();
            return;
        }

        _currentPlayerCount++;
        Debug.Log($"Player connected. Total players: {_currentPlayerCount}");
        
        PlayerConnected?.Invoke(conn);
        PlayersCountChanged?.Invoke(_currentPlayerCount);
    }

    public override void OnRoomServerDisconnect(NetworkConnectionToClient conn)
    {
        if (_roomPlayers.ContainsKey(conn))
        {
            _roomPlayers.Remove(conn);
        }

        _currentPlayerCount = Mathf.Max(0, _currentPlayerCount - 1);
        Debug.Log($"Player disconnected. Total players: {_currentPlayerCount}");
        
        PlayerDisconnected?.Invoke(conn);
        PlayersCountChanged?.Invoke(_currentPlayerCount);
        
        base.OnRoomServerDisconnect(conn);
    }

    public override void OnRoomServerAddPlayer(NetworkConnectionToClient conn)
    {
        base.OnRoomServerAddPlayer(conn);
        
        if (conn.identity != null)
        {
            NetworkRoomPlayer roomPlayer = conn.identity.GetComponent<NetworkRoomPlayer>();
            if (roomPlayer != null)
            {
                _roomPlayers[conn] = roomPlayer;
                Debug.Log($"Room player added for connection: {conn.connectionId}");
            }
        }
    }

    public override void OnRoomServerPlayersReady()
    {
        if (_currentPlayerCount < minPlayers)
        {
            Debug.LogWarning($"Not enough players to start. Need {minPlayers}, have {_currentPlayerCount}");
            return;
        }

        Debug.Log("All players ready. Starting game...");
        
        // Notify server/host
        GameStarted?.Invoke();
        
        // Send message to all clients
        NetworkServer.SendToAll(new GameStartedMessage());
        
        base.OnRoomServerPlayersReady();
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        NetworkClient.RegisterHandler<GameStartedMessage>(OnGameStartedMessage);
    }

    private void OnGameStartedMessage(GameStartedMessage msg)
    {
        // Only invoke on clients, not on host
        if (!NetworkServer.active)
        {
            GameStarted?.Invoke();
        }
    }

    public override GameObject OnRoomServerCreateGamePlayer(NetworkConnectionToClient conn, GameObject roomPlayer)
    {
        if (characters.Count == 0)
        {
            Debug.LogError("No player prefabs assigned in RoomManager.");
            return null;
        }

        // Select a random prefab
        int randomIndex = Random.Range(0, characters.Count);
        GameObject selectedPrefab = characters[randomIndex].characterPrefab;

        // Instantiate at start position or default
        Transform startPos = GetStartPosition();
        GameObject gamePlayer = startPos 
            ? Instantiate(selectedPrefab, startPos.position, startPos.rotation) 
            : Instantiate(selectedPrefab, Vector3.zero, Quaternion.identity);

        // Optional: Debug logging
        if (gamePlayer != null)
        {
            PlayerState playerState = gamePlayer.GetComponent<PlayerState>();
            if (playerState != null)
            {
                Debug.Log($"Game player created for connection: {conn.connectionId}");
            }
        }

        return gamePlayer;
    }

    #endregion

    #region Client Callbacks

    public override void OnRoomClientConnect()
    {
        base.OnRoomClientConnect();
        Debug.Log("Connected to room as client");
    }

    public override void OnRoomClientDisconnect()
    {
        base.OnRoomClientDisconnect();
        Debug.Log("Disconnected from room");
    }

    public override void OnRoomClientEnter()
    {
        base.OnRoomClientEnter();
        Debug.Log("Entered room lobby");
    }

    public override void OnRoomClientExit()
    {
        base.OnRoomClientExit();
        Debug.Log("Exited room lobby");
    }

    #endregion

    #region Network Lifecycle

    public override void OnStopHost()
    {
        CleanupNetworkState();
        HostStopped?.Invoke();
        base.OnStopHost();
        Debug.Log("Host stopped");
    }

    public override void OnStopClient()
    {
        CleanupNetworkState();
        ClientStopped?.Invoke();
        base.OnStopClient();
        Debug.Log("Client stopped");
    }

    public override void OnStopServer()
    {
        CleanupNetworkState();
        base.OnStopServer();
        Debug.Log("Server stopped");
    }

    #endregion

    #region Private Methods

    private void CleanupNetworkState()
    {
        _roomPlayers.Clear();
        _currentPlayerCount = 0;
    }

    #endregion

    #region Validation

    public override void OnRoomServerPlayersNotReady()
    {
        Debug.Log("Not all players are ready");
        base.OnRoomServerPlayersNotReady();
    }

    public override bool OnRoomServerSceneLoadedForPlayer(NetworkConnectionToClient conn, GameObject roomPlayer, GameObject gamePlayer) 
    {
        bool result = base.OnRoomServerSceneLoadedForPlayer(conn, roomPlayer, gamePlayer);
    
        if (result && NetworkGameEvents.Instance != null) 
        {
            conn.Send(new SceneLoadedMessage());
        }
    
        return result;
    }

    
    #endregion
}

public struct SceneLoadedMessage : NetworkMessage 
{
}

public struct GameStartedMessage : NetworkMessage
{
}
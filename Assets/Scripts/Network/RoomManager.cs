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
    private int _currentPlayerCount = 0;
    private int _teamAssignCounter = 0;

    [SerializeField] private List<CharacterData> characters = new List<CharacterData>();

    public List<CharacterData> Characters => characters;

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
        _currentPlayerCount = Mathf.Max(0, _currentPlayerCount - 1);
        Debug.Log($"Player disconnected. Total players: {_currentPlayerCount}");

        PlayerDisconnected?.Invoke(conn);
        PlayersCountChanged?.Invoke(_currentPlayerCount);

        base.OnRoomServerDisconnect(conn);
    }

    public override void OnRoomServerPlayersReady()
    {
        if (_currentPlayerCount < minPlayers)
        {
            Debug.LogWarning($"Not enough players to start. Need {minPlayers}, have {_currentPlayerCount}");
            return;
        }

        Debug.Log("All players ready. Starting game...");

        _teamAssignCounter = 0;

        GameStarted?.Invoke();
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

        // Get character selection from RoomPlayer (if set), otherwise random
        int characterIndex;
        RoomPlayer rp = roomPlayer.GetComponent<RoomPlayer>();
        if (rp != null && rp.selectedCharacterIndex >= 0 && rp.selectedCharacterIndex < characters.Count)
        {
            characterIndex = rp.selectedCharacterIndex;
        }
        else
        {
            // Random character (duplicates allowed)
            characterIndex = Random.Range(0, characters.Count);
        }

        GameObject selectedPrefab = characters[characterIndex].characterPrefab;

        Transform startPos = GetStartPosition();
        GameObject gamePlayer = startPos
            ? Instantiate(selectedPrefab, startPos.position, startPos.rotation)
            : Instantiate(selectedPrefab, Vector3.zero, Quaternion.identity);

        if (gamePlayer)
        {
            Debug.Log($"Game player created for connection: {conn.connectionId} with character index: {characterIndex}");
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

    private void CleanupNetworkState()
    {
        _currentPlayerCount = 0;
        _teamAssignCounter = 0;
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
        return result;
    }

    #endregion
}

public struct GameStartedMessage : NetworkMessage { }

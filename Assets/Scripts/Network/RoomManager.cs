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

    private readonly HashSet<int> _usedCharacterIndices = new();
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

        _usedCharacterIndices.Clear();

        GameStarted?.Invoke();

        // Отправляем сообщение клиентам (если нужно для UI)
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
        // Только на клиентах (не на хосте, если он одновременно сервер)
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

        int characterIndex = GetUniqueCharacterIndex();
        if (characterIndex == -1)
        {
            Debug.LogError("No more unique characters available.");
            return null;
        }

        GameObject selectedPrefab = characters[characterIndex].characterPrefab;

        Transform startPos = GetStartPosition();
        GameObject gamePlayer = startPos
            ? Instantiate(selectedPrefab, startPos.position, startPos.rotation)
            : Instantiate(selectedPrefab, Vector3.zero, Quaternion.identity);

        if (gamePlayer)
        {
            PlayerState playerState = gamePlayer.GetComponent<PlayerState>();
            if (playerState)
            {
                Debug.Log($"Game player created for connection: {conn.connectionId} with character index: {characterIndex}");
            }
        }

        return gamePlayer;
    }

    private int GetUniqueCharacterIndex()
    {
        if (_usedCharacterIndices.Count >= characters.Count)
        {
            return -1;
        }

        int attempts = 0;
        int maxAttempts = characters.Count * 2;

        while (attempts < maxAttempts)
        {
            int randomIndex = Random.Range(0, characters.Count);

            if (_usedCharacterIndices.Add(randomIndex))
            {
                return randomIndex;
            }

            attempts++;
        }

        for (int i = 0; i < characters.Count; i++)
        {
            if (_usedCharacterIndices.Add(i))
            {
                return i;
            }
        }

        return -1;
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
        _usedCharacterIndices.Clear();
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
        // Больше не отправляем SceneLoadedMessage — клиент сам сообщит через CmdSceneLoaded
        return result;
    }

    #endregion
}

public struct GameStartedMessage : NetworkMessage { }
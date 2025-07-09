using System.Collections.Generic;
using System.Linq;
using Mirror;
using UnityEditor;
using UnityEngine;

[DisallowMultipleComponent]
[AddComponentMenu("Network/Cling Network Discovery HUD")]
[HelpURL("https://mirror-networking.gitbook.io/docs/components/network-discovery")]
[RequireComponent(typeof(ClingNetworkDiscovery))]
public class ClingNetworkDiscoveryHUD : MonoBehaviour
{
    [Header("UI Configuration 🎨")]
    [SerializeField] private bool showPingInfo = true;
    [SerializeField] private bool showServerUptime = true;
    [SerializeField] private bool showPlayerCount = true;
    [SerializeField] private float refreshInterval = 2f;
    
    [Header("HUD Settings 📱")]
    [SerializeField] private Rect hudRect = new Rect(10, 10, 400, 600);
    [SerializeField] private int maxServersToShow = 10;

    private readonly Dictionary<long, CustomServerResponse> discoveredServers = new Dictionary<long, CustomServerResponse>();
    private Vector2 scrollViewPos = Vector2.zero;
    private float lastRefreshTime;
    private string playerNameInput = "Player";
    private string serverNameInput = "My Server";
    private int maxPlayersInput = 7;
    
    // Для автоматического обновления 🔄
    private string previousPlayerName = "";
    private string previousServerName = "";
    private int previousMaxPlayers = 0;

    public ClingNetworkDiscovery networkDiscovery;

#if UNITY_EDITOR
    void OnValidate()
    {
        if (Application.isPlaying) return;
        Reset();
    }

    void Reset()
    {
        networkDiscovery = GetComponent<ClingNetworkDiscovery>();

        // Добавляем обработчик событий для кастомного discovery
        if (Enumerable.Range(0, networkDiscovery.OnCustomServerFound.GetPersistentEventCount()).All(i => networkDiscovery.OnCustomServerFound.GetPersistentMethodName(i) != nameof(OnDiscoveredServer)))
        {
            UnityEditor.Events.UnityEventTools.AddPersistentListener(networkDiscovery.OnCustomServerFound, OnDiscoveredServer);
            UnityEditor.Undo.RecordObjects(new UnityEngine.Object[] { this, networkDiscovery }, "Set ClingNetworkDiscovery");
        }
    }
#endif

    void Start()
    {
        if (!networkDiscovery)
            networkDiscovery = GetComponent<ClingNetworkDiscovery>();
            
        // Инициализируем начальные значения
        playerNameInput = System.Environment.UserName ?? "Player";
        lastRefreshTime = Time.time;
        
        // Сохраняем начальные значения для сравнения 🔄
        previousPlayerName = playerNameInput;
        previousServerName = serverNameInput;
        previousMaxPlayers = maxPlayersInput;
    }

    void Update()
    {
        // Проверяем изменения и автоматически обновляем ⚡
        CheckForAutoUpdates();
    }

    void OnGUI()
    {
        if (NetworkManager.singleton == null)
            return;

        if (!NetworkClient.isConnected && !NetworkServer.active && !NetworkClient.active)
            DrawMainGUI();

        if (NetworkServer.active || NetworkClient.active)
            DrawConnectionControls();
    }

    void DrawMainGUI()
    {
        GUILayout.BeginArea(hudRect);
        GUILayout.BeginVertical("box");

        // Заголовок с эмодзи 🎮
        GUILayout.Label("🎮 Cling Network Discovery", GUI.skin.GetStyle("label"));
        GUILayout.Space(10);

        // Секция настроек клиента
        DrawClientSettings();
        GUILayout.Space(10);

        // Секция настроек сервера
        DrawServerSettings();
        GUILayout.Space(10);

        // Кнопки управления
        DrawControlButtons();
        GUILayout.Space(10);

        // Список найденных серверов
        DrawServersList();

        GUILayout.EndVertical();
        GUILayout.EndArea();
    }

    void DrawClientSettings()
    {
        GUILayout.Label("📱 Client Settings");
        GUILayout.BeginHorizontal();
        GUILayout.Label("Player Name:", GUILayout.Width(100));
        playerNameInput = GUILayout.TextField(playerNameInput, GUILayout.Width(200));
        GUILayout.EndHorizontal();
    }

    void DrawServerSettings()
    {
        GUILayout.Label("🖥️ Server Settings");
        
        GUILayout.BeginHorizontal();
        GUILayout.Label("Server Name:", GUILayout.Width(100));
        serverNameInput = GUILayout.TextField(serverNameInput, GUILayout.Width(200));
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        GUILayout.Label("Max Players:", GUILayout.Width(100));
        string maxPlayersStr = GUILayout.TextField(maxPlayersInput.ToString(), GUILayout.Width(50));
        if (int.TryParse(maxPlayersStr, out int newMaxPlayers))
        {
            maxPlayersInput = Mathf.Clamp(newMaxPlayers, 1, 100);
        }

        GUILayout.EndHorizontal();
    }

    void DrawControlButtons()
    {
        GUILayout.BeginHorizontal();

        // Кнопка поиска серверов
        if (GUILayout.Button("🔍 Find Servers"))
        {
            RefreshServerList();
        }

        // Кнопка автообновления
        bool autoRefresh = GUILayout.Toggle(Time.time - lastRefreshTime < refreshInterval, "Auto Refresh");
        if (autoRefresh && Time.time - lastRefreshTime >= refreshInterval)
        {
            RefreshServerList();
        }

        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();

        // Кнопка хоста
        if (GUILayout.Button("🏠 Start Host"))
        {
            StartHost();
        }

        // Кнопка сервера
        if (GUILayout.Button("🖥️ Start Server"))
        {
            StartServer();
        }

        GUILayout.EndHorizontal();
    }

    void DrawServersList()
    {
        GUILayout.Label($"🌐 Discovered Servers [{discoveredServers.Count}]:");

        if (discoveredServers.Count == 0)
        {
            GUILayout.Label("No servers found. Click 'Find Servers' to search! 🔍");
            return;
        }

        scrollViewPos = GUILayout.BeginScrollView(scrollViewPos, GUILayout.Height(300));

        var sortedServers = discoveredServers.Values
            .OrderBy(s => s.ping)
            .Take(maxServersToShow);

        foreach (var server in sortedServers)
        {
            DrawServerEntry(server);
        }

        GUILayout.EndScrollView();
    }

    void DrawServerEntry(CustomServerResponse server)
    {
        GUILayout.BeginVertical("box");
        
        // Основная информация сервера
        GUILayout.BeginHorizontal();
        
        string serverTitle = $"🎮 {server.serverName}";
        if (server.currentPlayers >= server.maxPlayers)
            serverTitle += " (FULL)";
            
        GUILayout.Label(serverTitle);
        
        if (server.currentPlayers < server.maxPlayers)
        {
            if (GUILayout.Button("Connect", GUILayout.Width(70)))
            {
                Connect(server);
            }
        }
        else
        {
            GUILayout.Button("FULL", GUILayout.Width(70));
        }
        
        GUILayout.EndHorizontal();

        // Детальная информация
        GUILayout.BeginHorizontal();
        
        string details = $"👤 Host: {server.hostPlayerName} | ";
        
        if (showPlayerCount)
            details += $"👥 Players: {server.currentPlayers}/{server.maxPlayers} | ";
            
        if (showPingInfo)
            details += $"📡 Ping: {server.ping}ms | ";
            
        if (showServerUptime)
            details += $"⏱️ Uptime: {server.serverUptime:F1}min | ";
            
        details += $"🌐 {server.EndPoint?.Address}:{server.EndPoint?.Port}";
        
        GUILayout.Label(details, GUI.skin.GetStyle("label"));
        GUILayout.EndHorizontal();

        GUILayout.EndVertical();
        GUILayout.Space(5);
    }

    void DrawConnectionControls()
    {
        GUILayout.BeginArea(new Rect(hudRect.x, hudRect.y + 40, 150, 100));
        GUILayout.BeginVertical("box");

        // Кнопка остановки в зависимости от режима
        if (NetworkServer.active && NetworkClient.isConnected)
        {
            if (GUILayout.Button("🛑 Stop Host"))
            {
                StopHost();
            }
        }
        else if (NetworkClient.isConnected)
        {
            if (GUILayout.Button("🛑 Stop Client"))
            {
                StopClient();
            }
        }
        else if (NetworkServer.active)
        {
            if (GUILayout.Button("🛑 Stop Server"))
            {
                StopServer();
            }
        }

        GUILayout.EndVertical();
        GUILayout.EndArea();
    }

    #region Auto-Update System ⚡

    /// <summary>
    /// Проверяет изменения и автоматически обновляет настройки
    /// </summary>
    void CheckForAutoUpdates()
    {
        bool hasChanges = false;

        // Проверяем изменение имени игрока
        if (previousPlayerName != playerNameInput)
        {
            networkDiscovery.UpdateClientInfo(playerNameInput);
            previousPlayerName = playerNameInput;
            hasChanges = true;
            Debug.Log($"📱 Player name auto-updated: {playerNameInput}");
        }

        // Проверяем изменение настроек сервера
        if (previousServerName != serverNameInput || previousMaxPlayers != maxPlayersInput)
        {
            networkDiscovery.UpdateServerInfo(serverNameInput, maxPlayersInput);
            previousServerName = serverNameInput;
            previousMaxPlayers = maxPlayersInput;
            hasChanges = true;
            Debug.Log($"🖥️ Server info auto-updated: {serverNameInput} (max: {maxPlayersInput})");
        }

        // Если были изменения и сервер активен, перезапускаем рекламу
        if (hasChanges && NetworkServer.active)
        {
            networkDiscovery.StopDiscovery();
            networkDiscovery.AdvertiseServer();
        }
    }

    #endregion

    #region Network Operations 🌐

    void RefreshServerList()
    {
        discoveredServers.Clear();
        networkDiscovery.StartDiscovery();
        lastRefreshTime = Time.time;
        Debug.Log("🔄 Refreshing server list...");
    }

    void StartHost()
    {
        discoveredServers.Clear();
        NetworkManager.singleton.StartHost();
        networkDiscovery.AdvertiseServer();
        Debug.Log("🏠 Starting host...");
    }

    void StartServer()
    {
        discoveredServers.Clear();
        NetworkManager.singleton.StartServer();
        networkDiscovery.AdvertiseServer();
        Debug.Log("🖥️ Starting server...");
    }

    void StopHost()
    {
        NetworkManager.singleton.StopHost();
        networkDiscovery.StopDiscovery();
        Debug.Log("🛑 Host stopped");
    }

    void StopClient()
    {
        NetworkManager.singleton.StopClient();
        networkDiscovery.StopDiscovery();
        Debug.Log("🛑 Client disconnected");
    }

    void StopServer()
    {
        NetworkManager.singleton.StopServer();
        networkDiscovery.StopDiscovery();
        Debug.Log("🛑 Server stopped");
    }

    void Connect(CustomServerResponse server)
    {
        networkDiscovery.StopDiscovery();
        NetworkManager.singleton.StartClient(server.uri);
        Debug.Log($"🔗 Connecting to {server.serverName} at {server.EndPoint}");
    }

    #endregion

    #region Event Handlers 📡

    public void OnDiscoveredServer(CustomServerResponse server)
    {
        Debug.Log($"🎮 Discovered Server: {server.serverName} | {server.EndPoint} | Players: {server.currentPlayers}/{server.maxPlayers}");
        
        // Сохраняем информацию о сервере
        discoveredServers[server.serverId] = server;
    }

    #endregion

    #region Utility Methods 🛠️

    /// <summary>
    /// Очистить список найденных серверов
    /// </summary>
    public void ClearServerList()
    {
        discoveredServers.Clear();
        Debug.Log("🗑️ Server list cleared");
    }

    /// <summary>
    /// Получить количество найденных серверов
    /// </summary>
    public int GetDiscoveredServersCount()
    {
        return discoveredServers.Count;
    }

    /// <summary>
    /// Получить информацию о конкретном сервере
    /// </summary>
    public CustomServerResponse? GetServerInfo(long serverId)
    {
        return discoveredServers.TryGetValue(serverId, out var server) ? server : null;
    }

    #endregion
}
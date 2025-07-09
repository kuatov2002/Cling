using System;
using System.Net;
using UnityEngine;
using Mirror;
using Mirror.Discovery;

    // Кастомный запрос - добавляйте свои поля сюда! 📤
    [Serializable]
    public struct CustomServerRequest : NetworkMessage
    {
        // Базовые поля
        public string playerName;
        
        // Ваши кастомные поля
        public int maxPlayers;
    }

    // Кастомный ответ - добавляйте свои поля сюда! 📥
    [Serializable]
    public struct CustomServerResponse : NetworkMessage
    {
        // Базовые поля Mirror
        public long serverId;
        public Uri uri;
    
        // ✅ Заменяем IPEndPoint на сериализуемые поля
        public string endPointAddress;
        public int endPointPort;
    
        // Информация о сервере
        public string serverName;
        public string hostPlayerName;
    
        // Состояние игры
        public int currentPlayers;
        public int maxPlayers;
    
        public float serverUptime;
        public int ping;
    
        // 🛠️ Вспомогательное свойство для работы с IPEndPoint
        public IPEndPoint EndPoint
        {
            get
            {
                if (string.IsNullOrEmpty(endPointAddress))
                    return null;
            
                return new IPEndPoint(IPAddress.Parse(endPointAddress), endPointPort);
            }
            set
            {
                if (value != null)
                {
                    endPointAddress = value.Address.ToString();
                    endPointPort = value.Port;
                }
                else
                {
                    endPointAddress = string.Empty;
                    endPointPort = 0;
                }
            }
        }
    }

    [Serializable]
    public class CustomServerFoundEvent : UnityEngine.Events.UnityEvent<CustomServerResponse> { }

    [DisallowMultipleComponent]
    [AddComponentMenu("Network/Custom Network Discovery")]
    public class ClingNetworkDiscovery : NetworkDiscoveryBase<CustomServerRequest, CustomServerResponse>
    {
        [Header("Server Configuration 🎮")]
        [SerializeField] private string serverName = "My Awesome Server";
        [SerializeField] private string hostPlayerName = "Host";
        [SerializeField] private int maxPlayers = 7;

        [Header("Client Configuration 📱")]
        [SerializeField] private string playerName = "Player";

        [Header("Events 🔔")]
        public CustomServerFoundEvent OnCustomServerFound = new CustomServerFoundEvent();

        private DateTime serverStartTime;
        private int currentPlayers = 0;

        public override void Start()
        {
            base.Start();
            serverStartTime = DateTime.Now;
        }

        #region Server Side 🖥️

        /// <summary>
        /// Обработка запроса от клиента
        /// Здесь вы можете фильтровать клиентов по вашим критериям! 🔍
        /// </summary>
        protected override CustomServerResponse ProcessRequest(CustomServerRequest request, IPEndPoint endpoint)
        {
            try
            {
                // Получаем текущее количество игроков
                currentPlayers = NetworkManager.singleton ? NetworkManager.singleton.numPlayers : 0;

                // Создаем ответ с полной информацией о сервере
                var response = new CustomServerResponse
                {
                    // Базовые поля Mirror
                    serverId = ServerId,
                    uri = transport.ServerUri(),
                    
                    // Информация о сервере
                    serverName = serverName,
                    hostPlayerName = hostPlayerName,
                    
                    // Состояние игры
                    currentPlayers = currentPlayers,
                    maxPlayers = maxPlayers,
                    
                    serverUptime = (float)(DateTime.Now - serverStartTime).TotalMinutes,
                    ping = 0 // Будет вычислен на клиенте
                };

                Debug.Log($"📡 Responding to discovery request from {request.playerName}");
                return response;
            }
            catch (NotImplementedException)
            {
                Debug.LogError($"Transport {transport} does not support network discovery");
                throw;
            }
        }

        /// <summary>
        /// Получить текущий игровой режим
        /// Реализуйте свою логику здесь! 🎯
        /// </summary>
        private int GetCurrentGameMode()
        {
            // Ваша логика определения текущего игрового режима
            return 1; // Например: 1 = Deathmatch, 2 = Capture the Flag, etc.
        }

        /// <summary>
        /// Получить список доступных карт
        /// </summary>
        private string[] GetAvailableMaps()
        {
            // Ваша логика получения доступных карт
            return new string[] { "Map1", "Map2", "Map3" };
        }

        #endregion

        #region Client Side 📱

        /// <summary>
        /// Создание запроса для отправки на сервер
        /// Настройте здесь данные, которые хотите отправить! 📤
        /// </summary>
        protected override CustomServerRequest GetRequest()
        {
            return new CustomServerRequest
            {
                // Базовые поля
                playerName = playerName,
                
                // Пользовательские предпочтения
                maxPlayers = maxPlayers,
            };
        }

        /// <summary>
        /// Обработка ответа от сервера
        /// Здесь вы можете фильтровать серверы по вашим критериям! 🔍
        /// </summary>
        protected override void ProcessResponse(CustomServerResponse response, IPEndPoint endpoint)
        {
            // ✅ Теперь устанавливаем endpoint через свойство
            response.EndPoint = endpoint;

            // Исправляем URI для корректного подключения
            if (response.uri != null)
            {
                UriBuilder realUri = new UriBuilder(response.uri)
                {
                    Host = endpoint.Address.ToString()
                };
                response.uri = realUri.Uri;
            }

            // Вычисляем примерный пинг
            response.ping = CalculatePing(endpoint);

            // Применяем фильтры клиента
            if (ShouldAcceptServer(response))
            {
                Debug.Log($"🎮 Found server: {response.serverName} ({response.currentPlayers}/{response.maxPlayers})");
        
                // Вызываем оба события для совместимости
                OnServerFound?.Invoke(response);
                OnCustomServerFound?.Invoke(response);
            }
            else
            {
                Debug.Log($"⚠️ Server {response.serverName} filtered out");
            }
        }

        /// <summary>
        /// Проверка, подходит ли сервер нашим критериям
        /// </summary>
        private bool ShouldAcceptServer(CustomServerResponse response)
        {
            // Проверяем, не заполнен ли сервер
            if (response.currentPlayers >= response.maxPlayers)
                return false;

            return true;
        }

        /// <summary>
        /// Примерный расчет пинга
        /// </summary>
        private int CalculatePing(IPEndPoint endpoint)
        {
            // Здесь можно реализовать реальный пинг-тест
            // Пока возвращаем случайное значение для демонстрации
            return UnityEngine.Random.Range(20, 150);
        }

        #endregion

        #region Public API 🛠️

        /// <summary>
        /// Обновить информацию о сервере
        /// </summary>
        public void UpdateServerInfo(string newServerName, int newMaxPlayers)
        {
            serverName = newServerName;
            maxPlayers = newMaxPlayers;
            
            Debug.Log($"📝 Server info updated: {serverName} (max: {maxPlayers})");
        }

        /// <summary>
        /// Обновить информацию о клиенте
        /// </summary>
        public void UpdateClientInfo(string newPlayerName)
        {
            playerName = newPlayerName;
            
            Debug.Log($"📝 Client info updated: {playerName}");
        }

        /// <summary>
        /// Получить список найденных серверов (для UI)
        /// </summary>
        public void RefreshServerList()
        {
            if (enableActiveDiscovery)
            {
                StopDiscovery();
                StartDiscovery();
                Debug.Log("🔄 Refreshing server list...");
            }
        }

        #endregion
    }

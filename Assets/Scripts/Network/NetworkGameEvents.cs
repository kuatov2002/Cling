using Mirror;
using UnityEngine;

public class NetworkGameEvents : NetworkBehaviour
{
    public static NetworkGameEvents Instance { get; private set; }

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
    public override void OnStartServer()
    {
        // Объект уже спавнится через NetworkServer.Spawn(), не нужно повторно
        Debug.Log("Server: GameEvents started");
    }

    [ClientRpc]
    public void RpcRolesAssigned()
    {
        Debug.Log("All roles have been assigned!");
    }

    [ClientRpc]
    public void RpcGameInitialized()
    {
        Debug.Log("Game has been initialized!");
    }

    [ClientRpc]
    public void RpcGameOver(string winningTeam)
    {
        Debug.Log($"{winningTeam} have won the game!");
        UIManager.Instance?.Gameover($"{winningTeam} have won the game!");
    }
    
    [ClientRpc]
    public void RpcSceneLoaded() {
        // Вызывается на клиентах после загрузки сцены
        Debug.Log("Scene loaded on client");
        GameManager.Instance?.OnClientSceneLoaded();
    }
}
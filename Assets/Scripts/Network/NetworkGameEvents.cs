using Mirror;
using UnityEngine;

public class NetworkGameEvents : NetworkBehaviour
{
    public static NetworkGameEvents Instance { get; private set; }

    private void Awake()
    {
        if (Instance && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    [Server]
    public override void OnStartServer()
    {
        Debug.Log("Server: NetworkGameEvents started");
    }

    [ClientRpc]
    public void RpcGameInitialized()
    {
        Debug.Log("Game has been initialized!");
    }

    [ClientRpc]
    public void RpcGameOver(string winningTeam, PlayerMatchStat[] playerStats)
    {
        Debug.Log($"{winningTeam} have won the game!");
        UIManager.Instance?.GameoverWithStats($"{winningTeam} wins!", playerStats);
    }

    [ClientRpc]
    public void RpcPlayerKilled(string killerName, string victimName)
    {
        UIManager.Instance?.ShowNotification($"{killerName} eliminated {victimName}");
    }
}

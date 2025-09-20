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

    // === RPC для клиентов ===

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
    public void RpcGameOver(string winningTeam, PlayerRoleInfo[] playerRoles)
    {
        Debug.Log($"{winningTeam} have won the game!");
        UIManager.Instance?.GameoverWithRoles($"{winningTeam} have won the game!", playerRoles);
    }
}
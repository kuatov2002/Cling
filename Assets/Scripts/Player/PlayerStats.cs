using Mirror;

public class PlayerStats : NetworkBehaviour
{
    [SyncVar] private int _kills;
    [SyncVar] private int _deaths;

    public int Kills => _kills;
    public int Deaths => _deaths;

    [Server]
    public void AddKill()
    {
        _kills++;
    }

    [Server]
    public void AddDeath()
    {
        _deaths++;
    }

    [Server]
    public void ResetStats()
    {
        _kills = 0;
        _deaths = 0;
    }
}

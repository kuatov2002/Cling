using Mirror;

public class PlayerRole : NetworkBehaviour
{
    public enum RoleType
    {
        Sheriff,
        Outlaw,
        Renegade
    }

    [SyncVar]
    public RoleType CurrentRole;
}


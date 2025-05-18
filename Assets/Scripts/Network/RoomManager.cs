using Mirror;
using UnityEngine;

public class RoomManager : NetworkRoomManager
{
    public override void OnRoomServerPlayersReady()
    {
        base.OnRoomServerPlayersReady();
        Debug.Log("Game Started");
    }
}

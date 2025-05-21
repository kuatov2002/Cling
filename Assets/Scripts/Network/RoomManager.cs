using Mirror;
using UnityEngine;

public class RoomManager : NetworkRoomManager
{
    public static event System.Action HostStopped;
    public static event System.Action ClientStopped;
    public override void OnRoomServerPlayersReady()
    {
        base.OnRoomServerPlayersReady();
        Debug.Log("Game Started");
    }

    public override void OnStopHost()
    {
        base.OnStopHost();
        HostStopped?.Invoke(); // Вызываем событие при остановке хоста
    }

    public override void OnStopClient()
    {
        base.OnStopClient();
        ClientStopped?.Invoke(); // Вызываем событие при остановке клиента
    }
}

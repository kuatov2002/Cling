using Mirror;

public class RoomPlayer : NetworkRoomPlayer
{
    [SyncVar(hook = nameof(OnCharacterIndexChanged))]
    public int selectedCharacterIndex = -1;

    [Command]
    public void CmdSelectCharacter(int characterIndex)
    {
        RoomManager rm = NetworkManager.singleton as RoomManager;
        if (rm != null && characterIndex >= 0 && characterIndex < rm.Characters.Count)
        {
            selectedCharacterIndex = characterIndex;
        }
    }

    private void OnCharacterIndexChanged(int oldVal, int newVal)
    {
        // Can be used to update lobby UI with selected character
    }
}

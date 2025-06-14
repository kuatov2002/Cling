using Mirror;
using TMPro;
using UnityEngine;

public class PlayerVisual : NetworkBehaviour
{
    [SerializeField] private TextMeshPro nickname;
    
    [SyncVar(hook = nameof(OnNicknameChanged))]
    private string playerNickname = "";

    public override void OnStartServer()
    {
        PlayerState playerState = GetComponent<PlayerState>();
        if (playerState != null)
        {
            playerNickname = playerState.PlayerNickname;
        }
    }

    [Server]
    public void SetPlayerNickname(string newNickname)
    {
        playerNickname = newNickname;
    }

    void OnNicknameChanged(string oldNickname, string newNickname)
    {
        if (!string.IsNullOrEmpty(newNickname))
        {
            nickname.text = newNickname;
        }
        else
        {
            nickname.text = "Waiting...";
        }
    }

    public override void OnStartClient()
    {
        if (!string.IsNullOrEmpty(playerNickname))
        {
            nickname.text = playerNickname;
        }
        else
        {
            nickname.text = "Waiting...";
        }
    }
}
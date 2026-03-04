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

    public void SetTeamColor(Team team)
    {
        if (nickname == null) return;

        nickname.color = team switch
        {
            Team.Red => new Color(1f, 0.3f, 0.3f),
            Team.Blue => new Color(0.3f, 0.5f, 1f),
            _ => Color.white
        };
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

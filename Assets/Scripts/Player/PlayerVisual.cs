using Mirror;
using TMPro;
using UnityEngine;

public class PlayerVisual : NetworkBehaviour
{
    [SerializeField] private TextMeshPro text;
    
    // SyncVar with a hook to update the visual whenever the value changes
    [SyncVar(hook = nameof(OnPlayerIndexChanged))]
    private int playerIndex = -1;

    // Called on the server when this player is initialized
    public override void OnStartServer()
    {
        // Get a reference to this player's PlayerState
        PlayerState playerState = GetComponent<PlayerState>();
        if (playerState != null && GameManager.Instance != null)
        {
            // Set the player index from the server
            playerIndex = GameManager.Instance.GetPlayerStableIndex(playerState);
        }
    }

    // Server method to update the player index
    [Server]
    public void SetPlayerIndex(int newIndex)
    {
        playerIndex = newIndex;
    }

    // This runs on all clients when the synced playerIndex changes
    void OnPlayerIndexChanged(int oldIndex, int newIndex)
    {
        // Update the visual text with the new index
        if (newIndex != -1)
        {
            text.text = $"{newIndex + 1}";
        }
        else
        {
            text.text = "Waiting...";
        }
    }

    // This ensures the text is updated when the object is enabled
    public override void OnStartClient()
    {
        // Make sure the text displays the current index
        if (playerIndex != -1)
        {
            text.text = $"{playerIndex + 1}";
        }
        else
        {
            text.text = "Waiting...";
        }
    }
}
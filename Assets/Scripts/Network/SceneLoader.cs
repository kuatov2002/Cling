using System.Collections;
using Mirror;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneLoader : NetworkBehaviour
{
    public override void OnStartClient()
    {
        base.OnStartClient();
        StartCoroutine(WaitForSceneFullyLoaded());
    }

    private IEnumerator WaitForSceneFullyLoaded()
    {
        yield return new WaitUntil(() => SceneManager.GetActiveScene().isLoaded);

        // Получаем PlayerState на этом объекте или у родителя
        PlayerState playerState = GetComponent<PlayerState>();

        if (isClient && playerState != null && playerState.isLocalPlayer)
        {
            playerState.CmdSceneLoaded();
            Debug.Log("Client reported scene loaded to server via PlayerState");
        }
        else
        {
            Debug.LogWarning("SceneLoader: PlayerState not found or not local player.");
        }
    }
}
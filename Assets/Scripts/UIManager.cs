using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class UIManager : MonoBehaviour
{
    public static UIManager Instance { get; private set; }
    [SerializeField] private List<PlayerRoleMapping> playerRoleMappings = new List<PlayerRoleMapping>();
    [SerializeField] private GameObject gameOverPanel;
    [SerializeField] private TextMeshProUGUI gameOverText;
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        
        if(gameOverPanel) gameOverPanel.SetActive(false);
        
        RoomManager.HostStopped+= OnNetworkStopped;
        RoomManager.ClientStopped+= OnNetworkStopped;
    }


    public void OnRoleChanged(RoleType newRole)
    {
        foreach (var roleMapping in playerRoleMappings)
        {
            roleMapping.gameObject.SetActive(roleMapping.playerRole == newRole);
        }
    }

    public void Gameover(string gameoverText)
    {
        gameOverPanel.SetActive(true);
        gameOverText.text = gameoverText;
        Cursor.visible = true;
        Cursor.lockState=CursorLockMode.None;
    }
    private void OnNetworkStopped()
    {
        // Деактивируем панель при остановке хоста или клиента
        if (gameOverPanel && gameOverPanel.activeSelf)
        {
            gameOverPanel.SetActive(false);
            gameOverText.text = "";
        }

        OnRoleChanged(RoleType.None);
        
        Cursor.visible = true;
        Cursor.lockState=CursorLockMode.None;
    }
}

[Serializable]
public class PlayerRoleMapping
{
    public RoleType playerRole;
    public GameObject gameObject; 
}

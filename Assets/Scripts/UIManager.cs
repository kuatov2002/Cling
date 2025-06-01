using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class UIManager : MonoBehaviour
{
    public static UIManager Instance;
    [SerializeField] private List<PlayerRoleMapping> playerRoleMappings = new List<PlayerRoleMapping>();
    [SerializeField] private GameObject gameOverPanel;
    [SerializeField] private TextMeshProUGUI gameOverText;

    [SerializeField] private CardManager[] slotsUI;


    private bool _cursorLocked;
    [SerializeField] private KeyCode lockKeyCode = KeyCode.LeftAlt;

    private UIState _uiState = UIState.Menu;
    
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        
        if (gameOverPanel) gameOverPanel.SetActive(false);
        
        RoomManager.HostStopped += OnNetworkStopped;
        RoomManager.ClientStopped += OnNetworkStopped;
        RoomManager.GameStarted += OnGameStarted;
    }

    private void Update()
    {
        // Можно оставить блокировку по клавише, если нужно
        if (Input.GetKeyDown(lockKeyCode) && _uiState==UIState.HUD)
        {
            LockCursor(!_cursorLocked);
        }
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
        LockCursor(false);
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
        _uiState = UIState.Menu;
        LockCursor(false);
    }
    private void OnGameStarted()
    {
        _uiState = UIState.HUD;
    }
    public void LockCursor(bool locked)
    {
        if (locked)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            _cursorLocked = true;
        }
        else
        {
            Cursor.visible = true;
            Cursor.lockState=CursorLockMode.None;
            _cursorLocked = false;
        }
    }

    public void UpdateInventoryUI(BaseItem[] slots)
    {
        for (int i = 0; i < slots.Length && i < slotsUI.Length; i++)
        {
            slotsUI[i].SetItem(slots[i].Data);
        }
    }
}

[Serializable]
public class PlayerRoleMapping
{
    public RoleType playerRole;
    public GameObject gameObject; 
}

public enum UIState
{
    Menu,
    HUD
}

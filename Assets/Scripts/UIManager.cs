using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

public class UIManager : MonoBehaviour
{
    public static UIManager Instance;
    [SerializeField] private List<PlayerRoleMapping> playerRoleMappings = new List<PlayerRoleMapping>();
    [SerializeField] private GameObject gameOverPanel;
    [SerializeField] private TextMeshProUGUI gameOverText;
    [SerializeField] private Image gunCooldown;
    [SerializeField] private CardManager[] slotsUI;

    private bool _cursorLocked;
    [SerializeField] private KeyCode lockKeyCode = KeyCode.LeftAlt;

    private UIState _uiState = UIState.Menu;

    [SerializeField] private GameObject menuUI;
    [SerializeField] private GameObject hud;
    
    private void Awake()
    {
        if (Instance && Instance != this)
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

    private void Start()
    {
        UpdateUIState();
    }

    private void Update()
    {
        if (Input.GetKeyDown(lockKeyCode) && _uiState == UIState.HUD)
        {
            LockCursor(!_cursorLocked);
        }
    }

    private void UpdateUIState()
    {
        if (menuUI) menuUI.SetActive(_uiState == UIState.Menu);
        if (hud) hud.SetActive(_uiState == UIState.HUD);
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
        if (gameOverPanel && gameOverPanel.activeSelf)
        {
            gameOverPanel.SetActive(false);
            gameOverText.text = "";
        }

        OnRoleChanged(RoleType.None);
        _uiState = UIState.Menu;
        UpdateUIState();
        LockCursor(false);
    }

    private void OnGameStarted()
    {
        _uiState = UIState.HUD;
        UpdateUIState();
    }

    private void LockCursor(bool locked)
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
            Cursor.lockState = CursorLockMode.None;
            _cursorLocked = false;
        }
    }

    private float _lastFill = -1f;

    public void UpdateGunCooldown(float cooldown)
    {
        float newFill = Mathf.Clamp01(cooldown);
        if (Mathf.Abs(newFill - _lastFill) > 0.005f)
        {
            gunCooldown.fillAmount = newFill;
            _lastFill = newFill;
        }
    }

    public void UpdateInventoryUI(BaseItem[] slots, int activeItemIndex = -1)
    {
        if (slots == null || slotsUI == null) return;
    
        for (int i = 0; i < slots.Length && i < slotsUI.Length; i++)
        {
            if (slots[i] && slots[i].Data)
            {
                slotsUI[i].SetItem(slots[i].Data);
            }
            else
            {
                slotsUI[i].UnSetItem();
            }
            
            slotsUI[i].SetActive(i == activeItemIndex);
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
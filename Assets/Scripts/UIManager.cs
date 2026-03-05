using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIManager : MonoBehaviour
{
    public static UIManager Instance;

    [Header("Game Over")]
    [SerializeField] private GameObject gameOverPanel;
    [SerializeField] private TextMeshProUGUI gameOverText;
    [SerializeField] private TextMeshProUGUI playerStatsText;

    [Header("HUD")]
    [SerializeField] private Image gunCooldown;
    [SerializeField] private CardManager[] slotsUI;
    [SerializeField] private TextMeshProUGUI bulletCounts;
    [SerializeField] private TextMeshProUGUI playerMoney;
    [SerializeField] TextMeshProUGUI interactText;
    [SerializeField] private NotificationSystem notificationSystem;
    [SerializeField] private AbilityUI abilityUI;
    [SerializeField] private AbilityInfoPanel abilityInfoPanel;

    [Header("Crosshair")]
    [SerializeField] private DynamicCrosshair dynamicCrosshair;

    [Header("Match Info")]
    [SerializeField] private TextMeshProUGUI matchTimerText;
    [SerializeField] private TextMeshProUGUI teamScoreText;

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

        if (notificationSystem == null)
        {
            notificationSystem = GetComponent<NotificationSystem>();
        }

        if (abilityUI == null)
        {
            abilityUI = GetComponent<AbilityUI>();
        }

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

    #region Match Info

    public void UpdateMatchTimer(float timeRemaining)
    {
        if (matchTimerText)
        {
            int minutes = Mathf.FloorToInt(timeRemaining / 60f);
            int seconds = Mathf.FloorToInt(timeRemaining % 60f);
            matchTimerText.text = $"{minutes:00}:{seconds:00}";
            matchTimerText.color = timeRemaining < 30f ? Color.red : Color.white;
        }
    }

    public void UpdateTeamScores(int redKills, int blueKills)
    {
        if (teamScoreText)
        {
            teamScoreText.text = $"<color=red>{redKills}</color> - <color=#4488FF>{blueKills}</color>";
        }
    }

    #endregion

    #region Game Over

    public void GameoverWithStats(string gameoverText, PlayerMatchStat[] playerStats)
    {
        if (gameOverPanel) gameOverPanel.SetActive(true);
        if (this.gameOverText) this.gameOverText.text = gameoverText;

        if (playerStatsText && playerStats != null)
        {
            string statsText = "\n";
            foreach (var stat in playerStats)
            {
                string teamColor = stat.team == Team.Red ? "red" : "#4488FF";
                statsText += $"<color={teamColor}>{stat.playerName}</color> — K:{stat.kills} D:{stat.deaths}\n";
            }

            playerStatsText.text = statsText;
        }

        LockCursor(false);
    }

    #endregion

    #region Network Events

    private void OnNetworkStopped()
    {
        if (gameOverPanel && gameOverPanel.activeSelf)
        {
            gameOverPanel.SetActive(false);
            if (gameOverText) gameOverText.text = "";
            if (playerStatsText) playerStatsText.text = "";
        }

        ClearAbilityInfo();

        _uiState = UIState.Menu;
        UpdateUIState();
        LockCursor(false);
    }

    private void OnGameStarted()
    {
        _uiState = UIState.HUD;
        UpdateUIState();
    }

    #endregion

    #region Cursor

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

    #endregion

    #region Gun UI

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

    public void UpdateCrosshairSpread(float spreadDegrees)
    {
        if (dynamicCrosshair) dynamicCrosshair.SetSpread(spreadDegrees);
    }

    public void UpdateBulletCount(int currentBullets, int maxBullets)
    {
        if (bulletCounts)
        {
            bulletCounts.text = $"{currentBullets} / {maxBullets}";

            float percentage = (float)currentBullets / maxBullets;

            if (percentage <= 0.25f)
            {
                bulletCounts.color = Color.red;
            }
            else if (percentage <= 0.5f)
            {
                bulletCounts.color = Color.yellow;
            }
            else
            {
                bulletCounts.color = Color.white;
            }
        }
    }

    #endregion

    #region Inventory UI

    public void UpdateMoney(int amount)
    {
        if (playerMoney)
        {
            playerMoney.text = $"{amount}$";
        }
    }

    public void UpdateInteractText(string text)
    {
        if (interactText)
        {
            interactText.text = text;
        }
    }

    public string GetInteractText()
    {
        return interactText.text;
    }

    public void UpdateInventoryUI(BaseItem[] slots, int activeItemIndex = -1)
    {
        if (slots == null || slotsUI == null) return;

        for (int i = 0; i < slots.Length && i < slotsUI.Length; i++)
        {
            if (slots[i])
            {
                slotsUI[i].SetItem(slots[i].itemName, slots[i].itemIcon);
            }
            else
            {
                slotsUI[i].UnSetItem();
            }

            slotsUI[i].SetActive(i == activeItemIndex);
        }
    }

    #endregion

    #region Notifications

    public void ShowNotification(string message)
    {
        if (notificationSystem != null)
        {
            notificationSystem.ShowNotification(message);
        }
    }

    #endregion

    #region Ability UI

    public void RegisterAbility(string abilityName, float cooldownDuration, Sprite icon)
    {
        if (abilityUI != null)
        {
            abilityUI.RegisterAbility(abilityName, cooldownDuration, icon);
        }
    }

    public void UnregisterAbility(string abilityName)
    {
        if (abilityUI != null)
        {
            abilityUI.UnregisterAbility(abilityName);
        }
    }

    public void StartAbilityCooldown(string abilityName)
    {
        if (abilityUI != null)
        {
            abilityUI.StartCooldown(abilityName);
        }
    }

    public bool IsAbilityReady(string abilityName)
    {
        if (abilityUI != null)
        {
            return abilityUI.IsAbilityReady(abilityName);
        }

        return false;
    }

    public float GetAbilityCooldownProgress(string abilityName)
    {
        if (abilityUI != null)
        {
            return abilityUI.GetCooldownProgress(abilityName);
        }

        return 1f;
    }

    public void StartAbilityCooldownWithTime(string abilityName, float remainingTime)
    {
        if (abilityUI != null)
        {
            abilityUI.StartCooldownWithTime(abilityName, remainingTime);
        }
    }

    public void ForceAbilityReady(string abilityName)
    {
        if (abilityUI != null)
        {
            abilityUI.ForceAbilityReady(abilityName);
        }
    }

    #endregion

    #region Ability Info Panel (F1)

    /// <summary>Populate the F1 ability info panel with character ability data.</summary>
    public void SetAbilityInfo(
        string passiveName, string passiveDesc,
        string activeName, string activeDesc,
        float cooldown, Sprite icon)
    {
        if (abilityInfoPanel != null)
        {
            abilityInfoPanel.SetAbilityInfo(passiveName, passiveDesc, activeName, activeDesc, cooldown, icon);
        }
    }

    /// <summary>Clear the F1 ability info panel.</summary>
    public void ClearAbilityInfo()
    {
        if (abilityInfoPanel != null)
        {
            abilityInfoPanel.ClearInfo();
        }
    }

    #endregion
}

public enum UIState
{
    Menu,
    HUD
}

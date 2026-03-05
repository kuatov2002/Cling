using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Valorant-style ability info panel. Shown while the info key is held (default F1).
/// Displays character passive + active ability name, description, and cooldown.
/// </summary>
public class AbilityInfoPanel : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private TextMeshProUGUI passiveNameText;
    [SerializeField] private TextMeshProUGUI passiveDescriptionText;
    [SerializeField] private TextMeshProUGUI activeNameText;
    [SerializeField] private TextMeshProUGUI activeDescriptionText;
    [SerializeField] private TextMeshProUGUI cooldownText;
    [SerializeField] private Image abilityIconImage;

    [Header("Input")]
    [SerializeField] private KeyCode infoKey = KeyCode.F1;

    private bool _hasData;

    private void Start()
    {
        if (panelRoot)
            panelRoot.SetActive(false);
    }

    private void Update()
    {
        if (!_hasData) return;

        if (Input.GetKeyDown(infoKey))
        {
            if (panelRoot) panelRoot.SetActive(true);
        }
        else if (Input.GetKeyUp(infoKey))
        {
            if (panelRoot) panelRoot.SetActive(false);
        }
    }

    /// <summary>
    /// Populate the panel with ability data. Called when the local player spawns.
    /// </summary>
    public void SetAbilityInfo(
        string passiveName, string passiveDesc,
        string activeName, string activeDesc,
        float cooldown, Sprite icon)
    {
        if (passiveNameText) passiveNameText.text = passiveName;
        if (passiveDescriptionText) passiveDescriptionText.text = passiveDesc;
        if (activeNameText) activeNameText.text = activeName;
        if (activeDescriptionText) activeDescriptionText.text = activeDesc;
        if (cooldownText) cooldownText.text = $"Cooldown: {cooldown:F0}s  |  Key: F";
        if (abilityIconImage && icon) abilityIconImage.sprite = icon;

        _hasData = true;
    }

    /// <summary>Clear the panel (on disconnect / player destroy).</summary>
    public void ClearInfo()
    {
        _hasData = false;
        if (panelRoot) panelRoot.SetActive(false);
    }
}

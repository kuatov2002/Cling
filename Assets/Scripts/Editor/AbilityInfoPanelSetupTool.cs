#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Editor tool: creates the AbilityInfoPanel UI under the existing HUD canvas
/// and wires it into UIManager.
/// Run from menu: Tools → Setup Ability Info Panel
/// </summary>
public static class AbilityInfoPanelSetupTool
{
    [MenuItem("Tools/Setup Ability Info Panel")]
    public static void Setup()
    {
        // Find UIManager in scene
        UIManager uiManager = Object.FindFirstObjectByType<UIManager>();
        if (uiManager == null)
        {
            Debug.LogError("[AbilityInfoPanel] UIManager not found in scene.");
            return;
        }

        // Check if already set up
        SerializedObject uiManagerSO = new SerializedObject(uiManager);
        SerializedProperty panelProp = uiManagerSO.FindProperty("abilityInfoPanel");
        if (panelProp != null && panelProp.objectReferenceValue != null)
        {
            Debug.Log("[AbilityInfoPanel] Already set up. Skipping.");
            return;
        }

        // Find HUD container (child named "HUD" or similar)
        Transform hud = uiManager.transform.Find("HUD");
        if (hud == null)
        {
            // Try to find any Canvas child
            foreach (Transform child in uiManager.transform)
            {
                if (child.GetComponent<Canvas>() != null || child.name.Contains("HUD"))
                {
                    hud = child;
                    break;
                }
            }
        }
        if (hud == null) hud = uiManager.transform;

        // Create AbilityInfoPanel root
        GameObject panelRoot = new GameObject("AbilityInfoPanel");
        panelRoot.transform.SetParent(hud, false);

        // Add RectTransform — center of screen, semi-transparent background
        RectTransform panelRect = panelRoot.AddComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.3f, 0.3f);
        panelRect.anchorMax = new Vector2(0.7f, 0.7f);
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;

        // Background image
        Image bg = panelRoot.AddComponent<Image>();
        bg.color = new Color(0f, 0f, 0f, 0.85f);

        // Layout group for text elements
        VerticalLayoutGroup layout = panelRoot.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(20, 20, 15, 15);
        layout.spacing = 8f;
        layout.childAlignment = TextAnchor.UpperLeft;
        layout.childControlHeight = true;
        layout.childControlWidth = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        // ContentSizeFitter
        ContentSizeFitter fitter = panelRoot.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        // === Create text elements ===
        // Header: "PASSIVE"
        CreateLabel(panelRoot.transform, "PassiveHeader", "PASSIVE", 16, FontStyles.Bold, new Color(0.3f, 0.8f, 1f));
        TextMeshProUGUI passiveName = CreateLabel(panelRoot.transform, "PassiveName", "—", 14, FontStyles.Bold, Color.white);
        TextMeshProUGUI passiveDesc = CreateLabel(panelRoot.transform, "PassiveDesc", "—", 12, FontStyles.Normal, new Color(0.8f, 0.8f, 0.8f));

        // Spacer
        GameObject spacer = new GameObject("Spacer");
        spacer.transform.SetParent(panelRoot.transform, false);
        LayoutElement spacerLE = spacer.AddComponent<LayoutElement>();
        spacerLE.preferredHeight = 10f;

        // Header: "ACTIVE (F)"
        CreateLabel(panelRoot.transform, "ActiveHeader", "ACTIVE  [F]", 16, FontStyles.Bold, new Color(1f, 0.6f, 0.2f));
        TextMeshProUGUI activeName = CreateLabel(panelRoot.transform, "ActiveName", "—", 14, FontStyles.Bold, Color.white);
        TextMeshProUGUI activeDesc = CreateLabel(panelRoot.transform, "ActiveDesc", "—", 12, FontStyles.Normal, new Color(0.8f, 0.8f, 0.8f));
        TextMeshProUGUI cooldownText = CreateLabel(panelRoot.transform, "CooldownText", "Cooldown: —", 12, FontStyles.Italic, new Color(0.6f, 0.6f, 0.6f));

        // Icon placeholder (hidden for now — will show if abilityIcon is set)
        GameObject iconGO = new GameObject("AbilityIcon");
        iconGO.transform.SetParent(panelRoot.transform, false);
        Image iconImage = iconGO.AddComponent<Image>();
        iconImage.color = Color.white;
        LayoutElement iconLE = iconGO.AddComponent<LayoutElement>();
        iconLE.preferredHeight = 48f;
        iconLE.preferredWidth = 48f;
        iconGO.SetActive(false); // hidden by default

        // Add AbilityInfoPanel component
        AbilityInfoPanel panel = panelRoot.AddComponent<AbilityInfoPanel>();

        // Wire serialized fields via SerializedObject
        SerializedObject panelSO = new SerializedObject(panel);
        panelSO.FindProperty("panelRoot").objectReferenceValue = panelRoot;
        panelSO.FindProperty("passiveNameText").objectReferenceValue = passiveName;
        panelSO.FindProperty("passiveDescriptionText").objectReferenceValue = passiveDesc;
        panelSO.FindProperty("activeNameText").objectReferenceValue = activeName;
        panelSO.FindProperty("activeDescriptionText").objectReferenceValue = activeDesc;
        panelSO.FindProperty("cooldownText").objectReferenceValue = cooldownText;
        panelSO.FindProperty("abilityIconImage").objectReferenceValue = iconImage;
        panelSO.ApplyModifiedProperties();

        // Wire into UIManager
        panelProp.objectReferenceValue = panel;
        uiManagerSO.ApplyModifiedProperties();

        // Start hidden
        panelRoot.SetActive(false);

        EditorUtility.SetDirty(uiManager);
        EditorUtility.SetDirty(panelRoot);

        Debug.Log("[AbilityInfoPanel] Created and wired successfully!");
        EditorUtility.DisplayDialog("Ability Info Panel", "AbilityInfoPanel created and wired to UIManager.", "OK");
    }

    private static TextMeshProUGUI CreateLabel(Transform parent, string name, string text, float fontSize, FontStyles style, Color color)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);

        TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.fontStyle = style;
        tmp.color = color;
        tmp.enableAutoSizing = false;
        tmp.overflowMode = TextOverflowModes.Overflow;
        tmp.textWrappingMode = TextWrappingModes.Normal;

        // Preferred height for layout
        LayoutElement le = go.AddComponent<LayoutElement>();
        le.preferredHeight = fontSize + 8f;

        return tmp;
    }
}
#endif

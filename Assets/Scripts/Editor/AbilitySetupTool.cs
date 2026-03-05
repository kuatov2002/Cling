#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System;
using System.Collections.Generic;

/// <summary>
/// Editor tool: adds ActiveAbility components to character prefabs
/// and wires the activeAbility reference in PlayerMovement.
/// Run from menu: Tools → Setup Abilities on Prefabs
/// Safe to run multiple times — skips prefabs that already have the ability.
/// </summary>
public static class AbilitySetupTool
{
    // Map: prefab path → ability component type
    private static readonly Dictionary<string, Type> PrefabAbilityMap = new()
    {
        { "Assets/Prefabs/Characters/Neo/Neo.prefab",            typeof(NeoAbility) },
        { "Assets/Prefabs/Characters/Alucard/Alucard.prefab",    typeof(AlucardAbility) },
        { "Assets/Prefabs/Characters/Phoenix/Phoenix.prefab",    typeof(PhoenixAbility) },
        { "Assets/Prefabs/Characters/Shadow/Shadow.prefab",      typeof(ShadowAbility) },
        { "Assets/Prefabs/Characters/CrazyJack/CrazyJack.prefab", typeof(CrazyJackAbility) },
        { "Assets/Prefabs/Characters/UncleSam/UncleSam.prefab",  typeof(UncleSamAbility) },
        { "Assets/Prefabs/Characters/BabyBilly/PlayerModel.prefab", typeof(BabyBillyAbility) },
        { "Assets/Prefabs/Characters/Angel/Angel.prefab",        typeof(AngelAbility) },
    };

    [MenuItem("Tools/Setup Abilities on Prefabs")]
    public static void SetupAbilities()
    {
        int modified = 0;
        int skipped = 0;

        foreach (var kvp in PrefabAbilityMap)
        {
            string path = kvp.Key;
            Type abilityType = kvp.Value;

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
            {
                Debug.LogWarning($"[AbilitySetup] Prefab not found: {path}");
                continue;
            }

            // Check if ability already exists
            if (prefab.GetComponent(abilityType) != null)
            {
                Debug.Log($"[AbilitySetup] Skipped {prefab.name} — already has {abilityType.Name}");
                skipped++;
                continue;
            }

            // Open prefab for editing
            string prefabPath = AssetDatabase.GetAssetPath(prefab);
            GameObject prefabRoot = PrefabUtility.LoadPrefabContents(prefabPath);

            // Add ability component
            ActiveAbility ability = (ActiveAbility)prefabRoot.AddComponent(abilityType);
            Debug.Log($"[AbilitySetup] Added {abilityType.Name} to {prefabRoot.name}");

            // Wire activeAbility in PlayerMovement via SerializedObject
            PlayerMovement movement = prefabRoot.GetComponent<PlayerMovement>();
            if (movement != null)
            {
                SerializedObject so = new SerializedObject(movement);
                SerializedProperty prop = so.FindProperty("activeAbility");
                if (prop != null)
                {
                    prop.objectReferenceValue = ability;
                    so.ApplyModifiedProperties();
                    Debug.Log($"[AbilitySetup] Wired activeAbility on {prefabRoot.name}");
                }
                else
                {
                    Debug.LogWarning($"[AbilitySetup] Could not find 'activeAbility' property on PlayerMovement for {prefabRoot.name}");
                }
            }
            else
            {
                Debug.LogWarning($"[AbilitySetup] No PlayerMovement on {prefabRoot.name}");
            }

            // Save prefab
            PrefabUtility.SaveAsPrefabAsset(prefabRoot, prefabPath);
            PrefabUtility.UnloadPrefabContents(prefabRoot);
            modified++;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[AbilitySetup] Done! Modified: {modified}, Skipped: {skipped}");
        EditorUtility.DisplayDialog("Ability Setup Complete",
            $"Modified: {modified} prefabs\nSkipped: {skipped} (already had abilities)", "OK");
    }
}
#endif

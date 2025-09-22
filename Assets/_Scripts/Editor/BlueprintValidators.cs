#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class BlueprintValidators
{
    [MenuItem("Tools/Data Validation/Ensure Gear Has Blueprints")]
    public static void EnsureGearHasBlueprints()
    {
        // Categories we treat as gear
        var gearCategories = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase)
        { "Armor", "Optics", "Pack" };

        // Index all blueprints by unlock target
        var bpByTarget = new HashSet<Object>();
        foreach (var g in AssetDatabase.FindAssets("t:BlueprintSO"))
        {
            var path = AssetDatabase.GUIDToAssetPath(g);
            var bp = AssetDatabase.LoadAssetAtPath<BlueprintSO>(path);
            if (bp?.unlocks != null) bpByTarget.Add(bp.unlocks);
        }

        int missing = 0;
        foreach (var g in AssetDatabase.FindAssets("t:ItemSO"))
        {
            var path = AssetDatabase.GUIDToAssetPath(g);
            var item = AssetDatabase.LoadAssetAtPath<ItemSO>(path);
            if (item == null) continue;

            // treat anything with category Armor/Optics/Pack as gear needing a blueprint
            if (!string.IsNullOrEmpty(item.category) && gearCategories.Contains(item.category))
            {
                if (!bpByTarget.Contains(item))
                {
                    Debug.LogWarning($"[Blueprint] Missing blueprint for gear item: {item.name} (category: {item.category})", item);
                    missing++;
                }
            }
        }

        if (missing == 0) Debug.Log("<color=green>All gear items have blueprints.</color>");
        else Debug.LogWarning($"Missing blueprints: {missing}. See warnings above.");
    }
}
#endif

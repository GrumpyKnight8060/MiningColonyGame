#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public static class BlueprintBatchTools
{
    [MenuItem("Tools/Blueprints/Create for Selected Items")]
    public static void CreateForSelectedItems()
    {
        int created = 0;
        foreach (var obj in Selection.objects)
        {
            if (obj is ItemSO item)
            {
                string bpName = $"BP: {item.name}";
                string folder = "Assets/_Data/Blueprints";

                CsvTools.EnsureFolder(folder);
                string path = $"{folder}/{CsvTools.SanitizeFileName(bpName)}.asset";
                var existing = AssetDatabase.LoadAssetAtPath<BlueprintSO>(path);
                if (existing == null)
                {
                    var bp = ScriptableObject.CreateInstance<BlueprintSO>();
                    bp.name = bpName;
                    bp.blueprintName = bpName;
                    bp.tier = Mathf.Clamp(item.tier, 1, 6);
                    bp.unlocks = item;
                    AssetDatabase.CreateAsset(bp, path);
                    created++;
                }
            }
        }
        AssetDatabase.SaveAssets();
        Debug.Log($"[BlueprintBatchTools] Created {created} blueprint(s) for selected ItemSO(s).");
    }
}
#endif

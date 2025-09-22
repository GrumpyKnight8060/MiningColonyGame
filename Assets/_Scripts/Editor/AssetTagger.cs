// Assets/_Scripts/Editor/AssetTagger.cs
#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class AssetTagger
{
    private static readonly string[] Roots = { "Assets/_Data", "Assets" };

    [MenuItem("Tools/Asset Tags/Tag All Assets (kind:*)")]
    public static void TagAll()
    {
        int tagged = 0;
        var guids = AssetDatabase.FindAssets("t:ScriptableObject", Roots);
        foreach (var g in guids)
        {
            var path = AssetDatabase.GUIDToAssetPath(g);
            var obj = AssetDatabase.LoadAssetAtPath<Object>(path);
            if (obj == null) continue;

            if (!TryInferKind(obj, out var kind)) continue;

            var labels = AssetDatabase.GetLabels(obj);
            var stripped = StripKindLabels(labels);
            var newLabels = new List<string>(stripped) { KindLabel(kind) };
            AssetDatabase.SetLabels(obj, newLabels.ToArray());
            tagged++;
        }
        AssetDatabase.SaveAssets();
        Debug.Log($"[AssetTagger] Tagged {tagged} ScriptableObject(s) with kind:* labels.");
    }

    private static bool TryInferKind(Object obj, out AssetKind kind)
    {
        kind = AssetKind.Item; // default
        if (obj == null) return false;
        string t = obj.GetType().Name;

        if (t == nameof(RecipeSO)) { kind = AssetKind.Recipe; return true; }
        if (t == nameof(BlueprintSO)) { kind = AssetKind.Blueprint; return true; }
        if (t == nameof(StructureSO)) { kind = AssetKind.Structure; return true; }
        if (t == nameof(ResourceSO)) { kind = AssetKind.Resource; return true; }
        if (t == nameof(ItemSO)) { kind = AssetKind.Item; return true; }

        // unknown ScriptableObject types -> treat as Item
        kind = AssetKind.Item;
        return true;
    }

    private static string KindLabel(AssetKind k) => $"kind:{k.ToString().ToLowerInvariant()}";

    private static string[] StripKindLabels(string[] labels)
    {
        var outLabels = new List<string>(labels.Length);
        foreach (var l in labels) if (!l.StartsWith("kind:")) outLabels.Add(l);
        return outLabels.ToArray();
    }
}
#endif

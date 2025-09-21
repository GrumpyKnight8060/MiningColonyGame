#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public enum AssetKind
{
    item,       // includes Resources, Items, components, etc.
    structure,  // buildable world structures
    recipe,     // RecipeSO
    blueprint   // BlueprintSO
}

public static class AssetKindHelper
{
    // Map type -> default kind (edit this if your class names differ)
    public static bool TryInferKind(Object obj, out AssetKind kind)
    {
        kind = AssetKind.item;
        if (obj == null) return false;

        var t = obj.GetType();
        var name = t.Name;

        // Common mappings – adjust names if your classes differ:
        if (name == "RecipeSO") { kind = AssetKind.recipe; return true; }
        if (name == "BlueprintSO") { kind = AssetKind.blueprint; return true; }
        if (name == "StructureSO") { kind = AssetKind.structure; return true; }

        // Treat all "stackable" variants as items (Resources, Items, etc.)
        if (name == "ItemSO" || name == "ResourceSO" || name == "StackableSO") { kind = AssetKind.item; return true; }

        // Fallback: default to item
        kind = AssetKind.item;
        return true;
    }

    public static string KindLabel(AssetKind k) => $"kind:{k}";

    public static AssetKind? GetExistingKindLabel(string[] labels)
    {
        foreach (var l in labels)
        {
            if (l == "kind:item") return AssetKind.item;
            if (l == "kind:structure") return AssetKind.structure;
            if (l == "kind:recipe") return AssetKind.recipe;
            if (l == "kind:blueprint") return AssetKind.blueprint;
        }
        return null;
    }

    public static string[] StripKindLabels(string[] labels)
    {
        System.Collections.Generic.List<string> outLabels = new();
        foreach (var l in labels)
        {
            if (l.StartsWith("kind:")) continue;
            outLabels.Add(l);
        }
        return outLabels.ToArray();
    }
}
#endif

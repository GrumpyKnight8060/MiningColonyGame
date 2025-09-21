#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public static class AssetTagger
{
    // Configure which folders to scan (add more if needed)
    private static readonly string[] Roots =
    {
        "Assets/_Data",          // your ScriptableObjects live here
        "Assets/_Scripts/Data"   // if you keep data assets alongside scripts
    };

    [MenuItem("Tools/Asset Tags/Tag All Assets (item/structure/recipe/blueprint)")]
    public static void TagAll()
    {
        int tagged = 0;
        foreach (var root in Roots)
        {
            var guids = AssetDatabase.FindAssets("t:ScriptableObject", new[] { root });
            foreach (var g in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(g);
                var obj = AssetDatabase.LoadAssetAtPath<Object>(path);
                if (obj == null) continue;

                if (!AssetKindHelper.TryInferKind(obj, out var kind)) continue;

                var labels = AssetDatabase.GetLabels(obj);
                var stripped = AssetKindHelper.StripKindLabels(labels);
                // Ensure single kind label
                var newLabels = new System.Collections.Generic.List<string>(stripped) { AssetKindHelper.KindLabel(kind) };
                AssetDatabase.SetLabels(obj, newLabels.ToArray());
                tagged++;
            }
        }
        AssetDatabase.SaveAssets();
        Debug.Log($"[AssetTagger] Tagged {tagged} ScriptableObject(s) with kind:item/structure/recipe/blueprint.");
    }
}
#endif

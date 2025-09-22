// Assets/_Scripts/Editor/ValidateGameAssets.cs
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public static class ValidateGameAssets
{
    [MenuItem("Tools/Data Validation/Validate Game Assets")]
    public static void ValidateAll()
    {
        int total = 0, kindMismatches = 0, badRecipeOutputs = 0;

        var guids = AssetDatabase.FindAssets("t:ScriptableObject");
        foreach (var g in guids)
        {
            var path = AssetDatabase.GUIDToAssetPath(g);
            var obj = AssetDatabase.LoadAssetAtPath<Object>(path);
            if (obj == null) continue;
            total++;

            // Ensure Kind matches concrete type, when available
            if (obj is IConstructible ic)
            {
                if (obj is ResourceSO && ic.Kind != AssetKind.Resource) { kindMismatches++; Debug.LogError($"[Kind] {path} should be Resource."); }
                if (obj is ItemSO && ic.Kind != AssetKind.Item) { kindMismatches++; Debug.LogError($"[Kind] {path} should be Item."); }
                if (obj is StructureSO && ic.Kind != AssetKind.Structure) { kindMismatches++; Debug.LogError($"[Kind] {path} should be Structure."); }
                if (obj is RecipeSO && ic.Kind != AssetKind.Recipe) { kindMismatches++; Debug.LogError($"[Kind] {path} should be Recipe."); }
                if (obj is BlueprintSO && ic.Kind != AssetKind.Blueprint) { kindMismatches++; Debug.LogError($"[Kind] {path} should be Blueprint."); }
            }

            // Recipes must NOT output recipes/blueprints
            if (obj is RecipeSO recipe)
            {
                var so = new SerializedObject(recipe);
                var outputs = so.FindProperty("outputs") ?? so.FindProperty("output");
                if (outputs != null)
                {
                    if (outputs.isArray)
                    {
                        for (int i = 0; i < outputs.arraySize; i++)
                        {
                            var elem = outputs.GetArrayElementAtIndex(i);
                            var target = elem.FindPropertyRelative("target")
                                         ?? elem.FindPropertyRelative("item")
                                         ?? elem.FindPropertyRelative("resource");
                            var o = target != null ? target.objectReferenceValue : null;
                            if (o is RecipeSO || o is BlueprintSO)
                            {
                                badRecipeOutputs++;
                                Debug.LogError($"[Outputs] {path} outputs disallowed type: {o?.name} ({o?.GetType().Name}).", recipe);
                            }
                        }
                    }
                    else
                    {
                        var o = outputs.objectReferenceValue;
                        if (o is RecipeSO || o is BlueprintSO)
                        {
                            badRecipeOutputs++;
                            Debug.LogError($"[Outputs] {path} outputs disallowed type: {o?.name} ({o?.GetType().Name}).", recipe);
                        }
                    }
                }
            }
        }

        Debug.Log($"Validation complete. Scanned: {total} | Kind mismatches: {kindMismatches} | Bad recipe outputs: {badRecipeOutputs}");
        if (kindMismatches == 0 && badRecipeOutputs == 0)
            Debug.Log("<color=green>All good: kinds aligned and recipe outputs valid.</color>");
    }
}
#endif

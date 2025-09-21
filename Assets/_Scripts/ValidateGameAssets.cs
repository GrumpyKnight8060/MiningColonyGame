#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class ValidateGameAssets
{
    private static readonly string[] RecipeFolders = { "Assets/_Data/Recipes" };

    [MenuItem("Tools/Data Validation/Run All Validators")]
    public static void RunAll()
    {
        int a = ValidateRecipeOutputs();
        int b = ValidateTier2Recipes();
        Debug.Log($"[Validation] Done. Output violations: {a}, Tier-2 violations: {b}.");
    }

    /// RULE A: Recipe outputs must be item/structure (never recipe/blueprint)
    [MenuItem("Tools/Data Validation/Validate Recipe Outputs")]
    public static int ValidateRecipeOutputs()
    {
        int bad = 0;
        var recipes = LoadAssetsByType("RecipeSO", RecipeFolders);

        foreach (var r in recipes)
        {
            var so = new SerializedObject(r);
            // Try common outputs field names
            var outputs = FindFirstProp(so, "outputs", "recipeOutputs");
            if (outputs == null || !outputs.isArray) continue;

            for (int i = 0; i < outputs.arraySize; i++)
            {
                var row = outputs.GetArrayElementAtIndex(i);
                var target = row.FindPropertyRelative("target") ?? row.FindPropertyRelative("item") ?? row.FindPropertyRelative("resource");
                if (target == null) continue;

                var obj = target.objectReferenceValue as Object;
                if (obj == null) continue;

                var kind = GetKindLabel(obj);
                if (kind == null)
                {
                    Debug.LogWarning($"[Outputs] {r.name} has output '{obj.name}' with no kind label. Run Tag All Assets.", r);
                    continue;
                }

                if (kind == AssetKind.recipe || kind == AssetKind.blueprint)
                {
                    Debug.LogError($"[Outputs] {r.name} outputs '{obj.name}' labeled {kind}. Recipes must not output recipes/blueprints.", r);
                    bad++;
                }
            }
        }

        if (bad == 0) Debug.Log("<color=green>[Outputs]</color> All recipe outputs are valid (item/structure).");
        return bad;
    }

    /// RULE B: Tier-2 recipes must use Basic Assembler AND exactly one distinct input type (any quantity).
    [MenuItem("Tools/Data Validation/Validate Tier-2 Recipes")]
    public static int ValidateTier2Recipes()
    {
        int stationViol = 0, inputViol = 0, totalT2 = 0;

        var recipes = LoadAssetsByType("RecipeSO", RecipeFolders);
        foreach (var r in recipes)
        {
            var so = new SerializedObject(r);
            var tierProp = so.FindProperty("tier");
            if (tierProp == null || tierProp.intValue != 2) continue;
            totalT2++;

            // Stations
            var stations = FindFirstProp(so, "stations", "allowedStations", "craftStations");
            var stationDesc = ArrayEnumNames(stations);
            bool stationOK = IsExactlyBasicAssembler(stations);
            if (!stationOK)
            {
                Debug.LogWarning($"[T2 Station] {r.name} uses {stationDesc}. Expected: Basic Assembler.", r);
                stationViol++;
            }

            // Inputs
            var inputs = FindFirstProp(so, "inputs", "recipeInputs", "ingredients");
            int distinct = CountDistinctInputs(inputs, out string breakdown);
            if (distinct != 1)
            {
                Debug.LogWarning($"[T2 Inputs] {r.name} has {distinct} distinct input item types: {breakdown}. Expected: exactly 1 type (any amount).", r);
                inputViol++;
            }
        }

        Debug.Log($"[T2] Recipes checked: {totalT2} | Station violations: {stationViol} | Input violations: {inputViol}");
        return stationViol + inputViol;
    }

    // -------- Helpers --------

    private static List<Object> LoadAssetsByType(string typeName, string[] folders)
    {
        var list = new List<Object>();
        var guids = AssetDatabase.FindAssets($"t:{typeName}", folders);
        foreach (var g in guids)
        {
            var path = AssetDatabase.GUIDToAssetPath(g);
            var obj = AssetDatabase.LoadAssetAtPath<Object>(path);
            if (obj != null) list.Add(obj);
        }
        return list;
    }

    private static SerializedProperty FindFirstProp(SerializedObject so, params string[] names)
    {
        foreach (var n in names)
        {
            var p = so.FindProperty(n);
            if (p != null) return p;
        }
        return null;
    }

    private static string Normalize(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        var chars = s.Where(char.IsLetterOrDigit).ToArray();
        return new string(chars).ToLowerInvariant();
    }

    private static bool IsExactlyBasicAssembler(SerializedProperty stationsProp)
    {
        if (stationsProp == null || !stationsProp.isArray || stationsProp.arraySize != 1) return false;
        var e = stationsProp.GetArrayElementAtIndex(0);
        var names = e.enumDisplayNames;
        int idx = Mathf.Clamp(e.enumValueIndex, 0, names.Length - 1);
        var display = (names.Length > 0) ? names[idx] : e.enumValueIndex.ToString();
        return Normalize(display) == "basicassembler";
    }

    private static string ArrayEnumNames(SerializedProperty arr)
    {
        if (arr == null || !arr.isArray) return "(none)";
        var names = new System.Collections.Generic.List<string>();
        for (int i = 0; i < arr.arraySize; i++)
        {
            var e = arr.GetArrayElementAtIndex(i);
            if (e.propertyType == SerializedPropertyType.Enum)
            {
                var all = e.enumDisplayNames;
                int idx = Mathf.Clamp(e.enumValueIndex, 0, all.Length - 1);
                names.Add((all.Length > 0) ? all[idx] : e.enumValueIndex.ToString());
            }
        }
        return names.Count == 0 ? "(none)" : string.Join(", ", names);
    }

    private static int CountDistinctInputs(SerializedProperty inputsProp, out string breakdown)
    {
        breakdown = "(none)";
        if (inputsProp == null || !inputsProp.isArray || inputsProp.arraySize == 0) return 0;

        var items = new HashSet<string>();
        var parts = new System.Collections.Generic.List<string>();

        for (int i = 0; i < inputsProp.arraySize; i++)
        {
            var row = inputsProp.GetArrayElementAtIndex(i);
            var itemRef = row.FindPropertyRelative("item") ?? row.FindPropertyRelative("resource");
            var amtProp = row.FindPropertyRelative("amount") ?? row.FindPropertyRelative("qty");

            string itemName = "(null)";
            if (itemRef != null && itemRef.objectReferenceValue != null)
                itemName = itemRef.objectReferenceValue.name;

            int amt = (amtProp != null) ? amtProp.intValue : 0;
            items.Add(itemName);
            parts.Add($"{amt}x {itemName}");
        }

        breakdown = string.Join("; ", parts);
        return items.Count;
    }

    private static AssetKind? GetKindLabel(Object obj)
    {
        var labels = AssetDatabase.GetLabels(obj);
        var k = AssetKindHelper.GetExistingKindLabel(labels);
        return k;
    }
}
#endif

#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class ValidateT2Recipes
{
    private static readonly string[] RecipeSearchFolders = { "Assets/_Data/Recipes" };

    [MenuItem("Tools/Data Validation/Validate Tier-2 Recipes")]
    public static void ValidateT2()
    {
        var guids = AssetDatabase.FindAssets("t:RecipeSO", RecipeSearchFolders);
        if (guids.Length == 0)
        {
            Debug.LogWarning("No RecipeSO assets found in: " + string.Join(", ", RecipeSearchFolders));
            return;
        }

        int totalT2 = 0, stationViolations = 0, inputViolations = 0;

        foreach (var guid in guids)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var obj = AssetDatabase.LoadAssetAtPath<Object>(path);
            if (obj == null) continue;

            var so = new SerializedObject(obj);

            // Tier
            var tierProp = so.FindProperty("tier");
            if (tierProp == null) continue;
            int tier = tierProp.intValue;
            if (tier != 2) continue;
            totalT2++;

            // Stations
            var stationsProp = FindFirstProp(so, "stations", "allowedStations", "craftStations", "craftingStations");
            bool stationOk = IsBasicAssembler(stationsProp, out string stationDesc);
            if (!stationOk)
            {
                stationViolations++;
                Debug.LogWarning($"[T2 Station] {obj.name} at {path} uses {stationDesc}. Expected: Basic Assembler");
            }

            // Inputs
            var inputsProp = FindFirstProp(so, "inputs", "recipeInputs", "ingredients");
            var distinctCount = CountDistinctInputItems(inputsProp, out string breakdown);
            if (distinctCount != 1)
            {
                inputViolations++;
                Debug.LogWarning($"[T2 Inputs] {obj.name} at {path} has {distinctCount} distinct input types: {breakdown}. Expected: exactly 1 item type (any amount).");
            }
        }

        Debug.Log($"Tier-2 validation complete. T2 recipes: {totalT2} | Station violations: {stationViolations} | Input violations: {inputViolations}");
        if (stationViolations == 0 && inputViolations == 0)
            Debug.Log("<color=green>All Tier-2 recipes are compliant (Basic Assembler + single-input).</color>");
    }

    // ---------- Helpers ----------

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

    /// True if stations array is exactly one enum entry whose normalized name equals "basicassembler".
    private static bool IsBasicAssembler(SerializedProperty stationsProp, out string description)
    {
        description = "(none)";
        if (stationsProp == null || !stationsProp.isArray)
        {
            description = "(no stations array)";
            return false;
        }

        int size = stationsProp.arraySize;
        description = ArrayToNames(stationsProp);

        if (size != 1) return false;

        var elem = stationsProp.GetArrayElementAtIndex(0);
        var names = elem.enumDisplayNames; // Unity’s human-readable names
        int idx = Mathf.Clamp(elem.enumValueIndex, 0, names.Length - 1);
        string display = (names.Length > 0) ? names[idx] : elem.enumValueIndex.ToString();

        return Normalize(display) == "basicassembler";
    }

    private static string ArrayToNames(SerializedProperty arr)
    {
        if (arr == null || !arr.isArray) return "(not array)";
        List<string> names = new List<string>();
        for (int i = 0; i < arr.arraySize; i++)
        {
            var e = arr.GetArrayElementAtIndex(i);
            if (e.propertyType == SerializedPropertyType.Enum)
            {
                var all = e.enumDisplayNames;
                int idx = Mathf.Clamp(e.enumValueIndex, 0, all.Length - 1);
                names.Add((all.Length > 0) ? all[idx] : e.enumValueIndex.ToString());
            }
            else
            {
                names.Add(e.displayName);
            }
        }
        return string.Join(", ", names);
    }

    /// Counts distinct input item references (fields named 'item' or 'resource'). Returns breakdown for logging.
    private static int CountDistinctInputItems(SerializedProperty inputsProp, out string breakdown)
    {
        breakdown = "(none)";
        if (inputsProp == null || !inputsProp.isArray || inputsProp.arraySize == 0) return 0;

        var distinct = new HashSet<string>();
        List<string> parts = new List<string>();

        for (int i = 0; i < inputsProp.arraySize; i++)
        {
            var elem = inputsProp.GetArrayElementAtIndex(i);

            var itemRef = elem.FindPropertyRelative("item") ?? elem.FindPropertyRelative("resource");
            var amount = elem.FindPropertyRelative("amount") ?? elem.FindPropertyRelative("qty");

            string itemName = "(null)";
            if (itemRef != null && itemRef.objectReferenceValue != null)
                itemName = itemRef.objectReferenceValue.name;

            int a = amount != null ? amount.intValue : 0;

            distinct.Add(itemName);
            parts.Add($"{a}x {itemName}");
        }

        breakdown = string.Join("; ", parts);
        return distinct.Count;
    }
}
#endif

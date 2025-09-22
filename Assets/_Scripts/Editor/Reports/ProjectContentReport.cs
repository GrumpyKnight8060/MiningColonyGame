#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

// Assumes these classes exist in your runtime assembly:
// ResourceSO, ItemSO, StructureSO, RecipeSO, BlueprintSO
// And RecipeSO has: string recipeName, int tier, float craftTimeSeconds (or craftTime),
// List<RecipeSO.Input> inputs (with StackableSO or ScriptableObject item + int amount),
// List<RecipeSO.Output> outputs (with UnityEngine.Object target + int amount),
// List<RecipeSO.Station> stations (enum names).
//
// If your property names differ slightly, adjust the getters in the helpers below.

public static class ProjectContentReport
{
    private const string ReportFolder = "Assets/_Data/Reports";
    private const string RES_ITEMS_CSV = ReportFolder + "/resources_items.csv";
    private const string RECIPES_CSV = ReportFolder + "/recipes.csv";
    private const string BLUEPRINTS_CSV = ReportFolder + "/blueprints.csv";

    [MenuItem("Tools/Reports/Build Project Content Report")]
    public static void BuildReport()
    {
        EnsureFolder(ReportFolder);

        // Load everything up front
        var resources = LoadAll<ResourceSO>();
        var items = LoadAll<ItemSO>();
        var structures = LoadAll<StructureSO>();
        var recipes = LoadAll<RecipeSO>();
        var blueprints = LoadAll<BlueprintSO>();

        // Quick indices by name
        var byName = new Dictionary<string, UnityEngine.Object>(StringComparer.OrdinalIgnoreCase);
        AddToIndex(byName, resources);
        AddToIndex(byName, items);
        AddToIndex(byName, structures);

        // Map: unlock target -> blueprint(s)
        var blueprintsByTarget = new Dictionary<UnityEngine.Object, List<BlueprintSO>>();
        foreach (var bp in blueprints)
        {
            if (bp == null || bp.unlocks == null) continue;
            if (!blueprintsByTarget.TryGetValue(bp.unlocks, out var list))
                blueprintsByTarget[bp.unlocks] = list = new List<BlueprintSO>();
            list.Add(bp);
        }

        // 1) Export Resources & Items
        using (var sw = new StreamWriter(RES_ITEMS_CSV, false))
        {
            sw.WriteLine("Type,Name,Tier,Refined,BaseValue,MaxStack,Category");
            foreach (var r in resources.OrderBy(r => r.name))
            {
                // Try to read common fields if present; default otherwise
                int tier = GetInt(r, "tier", 1);
                bool refined = GetBool(r, "refined", false);
                int baseValue = GetInt(r, "baseValue", 1);
                int maxStack = GetInt(r, "maxStack", 100);
                sw.WriteLine($"Resource,\"{r.name}\",{tier},{refined},{baseValue},{maxStack},");
            }
            foreach (var it in items.OrderBy(i => i.name))
            {
                int tier = GetInt(it, "tier", 1);
                int maxStack = GetInt(it, "maxStack", 100);
                string category = GetString(it, "category", "");
                sw.WriteLine($"Item,\"{it.name}\",{tier},,{0},{maxStack},\"{category}\"");
            }
        }

        // 2) Export Recipes
        var recipeWarnings = new List<string>();
        using (var sw = new StreamWriter(RECIPES_CSV, false))
        {
            sw.WriteLine("RecipeName,Tier,Stations,Inputs,Outputs,Time,Unlocked");
            foreach (var rx in recipes.OrderBy(r => r.name))
            {
                string rName = GetString(rx, "recipeName", rx.name);
                int tier = GetInt(rx, "tier", 1);
                float time = GetFloat(rx, new[] { "craftTimeSeconds", "craftTime" }, 3f);
                bool unlocked = GetBool(rx, new[] { "unlockedByDefault", "unlocked" }, false);

                var stations = GetStations(rx);
                var inputs = GetInputs(rx, recipeWarnings, byName);
                var outputs = GetOutputs(rx, recipeWarnings, byName);

                sw.WriteLine($"{Csv(rName)},{tier},{Csv(string.Join("; ", stations))},{Csv(inputs)},{Csv(outputs)},{time},{unlocked}");

                // Validation rules you asked for:

                // Rule A: Tier 2 recipes should have >= 2 total input amount
                // (Your design: T2 must not be trivial single-input 1x; use multiples of same material)
                int totalInputStacks = CountDistinctInputs(rx);
                int totalInputAmount = SumInputAmounts(rx);
                if (tier == 2 && (totalInputStacks < 1 || totalInputAmount < 2))
                    recipeWarnings.Add($"[T2 Input Count] {rName} has too few input amount (sum={totalInputAmount}).");

                // Rule B: If any output is ItemSO or StructureSO, there should be a blueprint that unlocks it.
                foreach (var o in GetOutputTargets(rx))
                {
                    if (o is ItemSO || o is StructureSO)
                    {
                        if (!blueprintsByTarget.ContainsKey(o))
                            recipeWarnings.Add($"[Missing Blueprint] {rName} outputs '{o.name}' but no BlueprintSO unlocks it.");
                    }
                }

                // Rule C: Station/input mismatch based on your tier rules
                // Assembler rules: T1 BasicAssembler = 1 input; T3 IndustrialAssembler = 2; T4 AutomatedAssembler = 2 (1 may be liquid); T6 MassAssembler = 3
                // Smelters: T1 StoneSmelter = 1; T3 ElectricSmelter = 1; T4 BlastFurnace = 2; T6 QuantumSmelter = 2
                // Refineries: T3 BasicRefinery = 2 in / 2 out; T4 IndustrialRefinery = 3 in / 2 out; T6 MassRefinery = 3 in / 3 out
                ValidateStationRules(rx, recipeWarnings);
            }
        }

        // 3) Export Blueprints
        using (var sw = new StreamWriter(BLUEPRINTS_CSV, false))
        {
            sw.WriteLine("BlueprintName,Tier,Unlocks");
            foreach (var bp in blueprints.OrderBy(b => b.name))
            {
                string name = GetString(bp, "blueprintName", bp.name);
                int tier = GetInt(bp, "tier", 1);
                string unlocks = bp.unlocks ? bp.unlocks.name : "";
                sw.WriteLine($"{Csv(name)},{tier},{Csv(unlocks)}");
            }
        }

        AssetDatabase.Refresh();

        // Print a summary + TODOs
        Debug.Log($"<b>Report written:</b>\n{RES_ITEMS_CSV}\n{RECIPES_CSV}\n{BLUEPRINTS_CSV}");
        Debug.Log($"Counts: Resources={resources.Count}  Items={items.Count}  Structures={structures.Count}  Recipes={recipes.Count}  Blueprints={blueprints.Count}");

        if (recipeWarnings.Count == 0)
        {
            Debug.Log("<color=green>No recipe validation warnings.</color>");
        }
        else
        {
            Debug.LogWarning($"<b>Recipe validation warnings: {recipeWarnings.Count}</b>\n- " + string.Join("\n- ", recipeWarnings.Take(50)) + (recipeWarnings.Count > 50 ? "\n... (more)" : ""));
        }
    }

    // -------------------- Helpers --------------------

    private static void EnsureFolder(string path)
    {
        var parts = path.Split('/');
        if (parts.Length == 0 || parts[0] != "Assets") return;
        string cur = "Assets";
        for (int i = 1; i < parts.Length; i++)
        {
            string next = parts[i];
            if (!AssetDatabase.IsValidFolder($"{cur}/{next}"))
                AssetDatabase.CreateFolder(cur, next);
            cur = $"{cur}/{next}";
        }
    }

    private static List<T> LoadAll<T>() where T : UnityEngine.Object
    {
        var list = new List<T>();
        foreach (var guid in AssetDatabase.FindAssets($"t:{typeof(T).Name}"))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var obj = AssetDatabase.LoadAssetAtPath<T>(path);
            if (obj != null) list.Add(obj);
        }
        return list;
    }

    private static void AddToIndex(Dictionary<string, UnityEngine.Object> dict, IEnumerable<UnityEngine.Object> objs)
    {
        foreach (var o in objs)
        {
            if (!dict.ContainsKey(o.name))
                dict[o.name] = o;
        }
    }

    private static string Csv(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        if (s.IndexOfAny(new[] { ',', '"', '\n' }) >= 0)
            return "\"" + s.Replace("\"", "\"\"") + "\"";
        return s;
    }

    private static int GetInt(object obj, string field, int defVal)
    {
        var f = obj.GetType().GetField(field);
        if (f != null && f.FieldType == typeof(int)) return (int)f.GetValue(obj);
        return defVal;
    }
    private static bool GetBool(object obj, string field, bool defVal)
    {
        var f = obj.GetType().GetField(field);
        if (f != null && f.FieldType == typeof(bool)) return (bool)f.GetValue(obj);
        return defVal;
    }
    private static bool GetBool(object obj, string[] fields, bool defVal)
    {
        foreach (var field in fields)
        {
            var f = obj.GetType().GetField(field);
            if (f != null && f.FieldType == typeof(bool)) return (bool)f.GetValue(obj);
        }
        return defVal;
    }
    private static float GetFloat(object obj, string[] fields, float defVal)
    {
        foreach (var field in fields)
        {
            var f = obj.GetType().GetField(field);
            if (f != null && f.FieldType == typeof(float)) return (float)f.GetValue(obj);
        }
        return defVal;
    }
    private static string GetString(object obj, string field, string defVal)
    {
        var f = obj.GetType().GetField(field);
        if (f != null && f.FieldType == typeof(string)) return (string)f.GetValue(obj);
        return defVal;
    }

    // Recipe field readers that tolerate naming differences
    private static List<string> GetStations(RecipeSO rx)
    {
        var list = new List<string>();
        var f = rx.GetType().GetField("stations");
        if (f != null)
        {
            var val = f.GetValue(rx) as System.Collections.IEnumerable;
            if (val != null)
            {
                foreach (var s in val) list.Add(s.ToString());
            }
        }
        return list;
    }
    private static string GetInputs(RecipeSO rx, List<string> warnings, Dictionary<string, UnityEngine.Object> index)
    {
        var f = rx.GetType().GetField("inputs");
        var sb = new System.Text.StringBuilder();
        if (f != null)
        {
            var arr = f.GetValue(rx) as System.Collections.IEnumerable;
            if (arr != null)
            {
                bool first = true;
                foreach (var it in arr)
                {
                    var itemF = it.GetType().GetField("item") ?? it.GetType().GetField("input");
                    var amtF = it.GetType().GetField("amount") ?? it.GetType().GetField("quantity");
                    var obj = itemF != null ? itemF.GetValue(it) as UnityEngine.Object : null;
                    int amt = (amtF != null) ? (int)amtF.GetValue(it) : 1;

                    if (obj == null)
                    {
                        warnings.Add($"[Missing Input Ref] Recipe '{rx.name}' has a null input reference.");
                        continue;
                    }

                    if (!first) sb.Append("; ");
                    sb.Append(obj.name).Append(" x").Append(Mathf.Max(1, amt));
                    first = false;
                }
            }
        }
        return sb.ToString();
    }
    private static string GetOutputs(RecipeSO rx, List<string> warnings, Dictionary<string, UnityEngine.Object> index)
    {
        var f = rx.GetType().GetField("outputs");
        var sb = new System.Text.StringBuilder();
        if (f != null)
        {
            var arr = f.GetValue(rx) as System.Collections.IEnumerable;
            if (arr != null)
            {
                bool first = true;
                foreach (var it in arr)
                {
                    var tgtF = it.GetType().GetField("target") ?? it.GetType().GetField("output");
                    var amtF = it.GetType().GetField("amount") ?? it.GetType().GetField("outputQuantity");
                    var obj = tgtF != null ? tgtF.GetValue(it) as UnityEngine.Object : null;
                    int amt = 1;
                    if (amtF != null)
                    {
                        var v = amtF.GetValue(it);
                        if (v is int iv) amt = iv;
                        else if (v is float fv) amt = Mathf.RoundToInt(fv);
                    }

                    if (obj == null)
                    {
                        warnings.Add($"[Missing Output Ref] Recipe '{rx.name}' has a null output reference.");
                        continue;
                    }

                    if (!first) sb.Append("; ");
                    sb.Append(obj.name).Append(" x").Append(Mathf.Max(1, amt));
                    first = false;
                }
            }
        }
        return sb.ToString();
    }
    private static int CountDistinctInputs(RecipeSO rx)
    {
        var f = rx.GetType().GetField("inputs");
        if (f == null) return 0;
        var arr = f.GetValue(rx) as System.Collections.IEnumerable;
        if (arr == null) return 0;
        var set = new HashSet<UnityEngine.Object>();
        foreach (var it in arr)
        {
            var itemF = it.GetType().GetField("item") ?? it.GetType().GetField("input");
            var obj = itemF != null ? itemF.GetValue(it) as UnityEngine.Object : null;
            if (obj != null) set.Add(obj);
        }
        return set.Count;
    }
    private static int SumInputAmounts(RecipeSO rx)
    {
        var f = rx.GetType().GetField("inputs");
        if (f == null) return 0;
        var arr = f.GetValue(rx) as System.Collections.IEnumerable;
        if (arr == null) return 0;
        int sum = 0;
        foreach (var it in arr)
        {
            var amtF = it.GetType().GetField("amount") ?? it.GetType().GetField("quantity");
            int amt = (amtF != null) ? (int)amtF.GetValue(it) : 1;
            sum += Mathf.Max(1, amt);
        }
        return sum;
    }
    private static IEnumerable<UnityEngine.Object> GetOutputTargets(RecipeSO rx)
    {
        var f = rx.GetType().GetField("outputs");
        if (f == null) yield break;
        var arr = f.GetValue(rx) as System.Collections.IEnumerable;
        if (arr == null) yield break;
        foreach (var it in arr)
        {
            var tgtF = it.GetType().GetField("target") ?? it.GetType().GetField("output");
            var obj = tgtF != null ? tgtF.GetValue(it) as UnityEngine.Object : null;
            if (obj != null) yield return obj;
        }
    }

    private static void ValidateStationRules(RecipeSO rx, List<string> warnings)
    {
        // Derive counts
        int distinctInputs = CountDistinctInputs(rx);
        int outputs = GetOutputTargets(rx).Count();

        // Read stations by name
        var stations = GetStations(rx);

        foreach (var st in stations)
        {
            switch (st)
            {
                // Assemblers
                case "BasicAssembler":
                    if (distinctInputs > 1)
                        warnings.Add($"[Station Rule] {rx.name}: BasicAssembler supports 1 input, got {distinctInputs}.");
                    break;
                case "IndustrialAssembler":
                    if (distinctInputs > 2)
                        warnings.Add($"[Station Rule] {rx.name}: IndustrialAssembler supports 2 inputs, got {distinctInputs}.");
                    break;
                case "AutomatedAssembler":
                    if (distinctInputs > 2)
                        warnings.Add($"[Station Rule] {rx.name}: AutomatedAssembler supports 2 inputs (one may be liquid), got {distinctInputs}.");
                    break;
                case "MassAssembler":
                    if (distinctInputs > 3)
                        warnings.Add($"[Station Rule] {rx.name}: MassAssembler supports up to 3 inputs, got {distinctInputs}.");
                    break;

                // Smelters
                case "StoneSmelter":
                    if (distinctInputs > 1)
                        warnings.Add($"[Station Rule] {rx.name}: StoneSmelter supports 1 input, got {distinctInputs}.");
                    break;
                case "ElectricSmelter":
                    if (distinctInputs > 1)
                        warnings.Add($"[Station Rule] {rx.name}: ElectricSmelter supports 1 input, got {distinctInputs}.");
                    break;
                case "BlastFurnace":
                    if (distinctInputs > 2)
                        warnings.Add($"[Station Rule] {rx.name}: BlastFurnace supports 2 inputs, got {distinctInputs}.");
                    break;
                case "QuantumSmelter":
                    if (distinctInputs > 2)
                        warnings.Add($"[Station Rule] {rx.name}: QuantumSmelter supports 2 inputs, got {distinctInputs}.");
                    break;

                // Refineries (outputs also limited)
                case "BasicRefinery":
                    if (distinctInputs > 2)
                        warnings.Add($"[Station Rule] {rx.name}: BasicRefinery supports 2 inputs, got {distinctInputs}.");
                    if (outputs > 2)
                        warnings.Add($"[Station Rule] {rx.name}: BasicRefinery supports 2 outputs, got {outputs}.");
                    break;
                case "IndustrialRefinery":
                    if (distinctInputs > 3)
                        warnings.Add($"[Station Rule] {rx.name}: IndustrialRefinery supports 3 inputs, got {distinctInputs}.");
                    if (outputs > 2)
                        warnings.Add($"[Station Rule] {rx.name}: IndustrialRefinery supports 2 outputs, got {outputs}.");
                    break;
                case "MassRefinery":
                    if (distinctInputs > 3)
                        warnings.Add($"[Station Rule] {rx.name}: MassRefinery supports 3 inputs, got {distinctInputs}.");
                    if (outputs > 3)
                        warnings.Add($"[Station Rule] {rx.name}: MassRefinery supports 3 outputs, got {outputs}.");
                    break;
            }
        }
    }
}
#endif

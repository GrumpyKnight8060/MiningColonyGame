#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class RecipeImporter
{
    // CSV format (no quotes/commas in names):
    // recipeName,tier,stations,inputs,outputs,craftTimeSeconds,unlockedByDefault,iconPath,unlockTag
    //
    // stations: semicolon list of StationType names (e.g., "StoneSmelter;ElectricSmelter")
    // inputs:   semicolon list of tokens "Nx Name" with optional "~L" to mark liquid, e.g. "2x Oil~L;1x Water~L;1x Plastic"
    // outputs:  semicolon list of "Nx Name", e.g. "1x Plastic;1x Sulfur (Refined)"

    private const string CsvPath = "Assets/_Data/Import/recipes.csv";
    private const string DataDir = "Assets/_Data";
    private const string OutDir = "Assets/_Data/Recipes";
    private const string ItemsDir = "Assets/_Data/Items";

    // ---------- Station capability rules ----------
    private class StationCaps
    {
        public int maxInputs;
        public bool allowLiquidInput;
        public int maxOutputs;
        public int minTier;
    }

    private static readonly Dictionary<StationType, StationCaps> CAPS = new()
    {
        // Assemblers
        { StationType.BasicAssembler,      new StationCaps{ maxInputs=1, allowLiquidInput=false, maxOutputs=1, minTier=1 } },
        { StationType.IndustrialAssembler, new StationCaps{ maxInputs=2, allowLiquidInput=false, maxOutputs=1, minTier=3 } },
        { StationType.AutomatedAssembler,  new StationCaps{ maxInputs=2, allowLiquidInput=true,  maxOutputs=1, minTier=4 } },
        { StationType.MassAssembler,       new StationCaps{ maxInputs=3, allowLiquidInput=true,  maxOutputs=1, minTier=6 } },

        // Smelters
        { StationType.StoneSmelter,        new StationCaps{ maxInputs=1, allowLiquidInput=false, maxOutputs=1, minTier=1 } },
        { StationType.ElectricSmelter,     new StationCaps{ maxInputs=1, allowLiquidInput=false, maxOutputs=1, minTier=3 } },
        { StationType.BlastFurnace,        new StationCaps{ maxInputs=2, allowLiquidInput=false, maxOutputs=1, minTier=4 } },
        { StationType.QuantumSmelter,      new StationCaps{ maxInputs=2, allowLiquidInput=false, maxOutputs=1, minTier=6 } },

        // Refineries
        { StationType.BasicRefinery,       new StationCaps{ maxInputs=2, allowLiquidInput=true,  maxOutputs=2, minTier=3 } },
        { StationType.IndustrialRefinery,  new StationCaps{ maxInputs=3, allowLiquidInput=true,  maxOutputs=2, minTier=4 } },
        { StationType.MassRefinery,        new StationCaps{ maxInputs=3, allowLiquidInput=true,  maxOutputs=3, minTier=6 } },
    };

    [MenuItem("Tools/Data Import/Import Recipes CSV")]
    public static void ImportRecipes()
    {
        if (!File.Exists(CsvPath))
        {
            Debug.LogError($"recipes.csv not found at {CsvPath}");
            return;
        }

        EnsureFolder(DataDir);
        EnsureFolder(OutDir);
        EnsureFolder(ItemsDir);

        // Build lookup for all StackableSOs (Resources + Items)
        var stackables = new Dictionary<string, StackableSO>(StringComparer.OrdinalIgnoreCase);
        foreach (var guid in AssetDatabase.FindAssets("t:StackableSO", new[] { DataDir }))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var so = AssetDatabase.LoadAssetAtPath<StackableSO>(path);
            if (so != null && !string.IsNullOrEmpty(so.displayName))
                stackables[so.displayName] = so;
        }

        var lines = File.ReadAllLines(CsvPath);
        if (lines.Length <= 1)
        {
            Debug.LogWarning("recipes.csv has no data rows.");
            return;
        }

        int created = 0, updated = 0;
        var ci = CultureInfo.InvariantCulture;

        for (int i = 1; i < lines.Length; i++)
        {
            var raw = lines[i];
            if (string.IsNullOrWhiteSpace(raw)) continue;

            // naive CSV split (ensure your CSV has no commas inside names)
            var cols = raw.Split(',');
            if (cols.Length < 9)
            {
                Debug.LogWarning($"[Row {i + 1}] Expected 9 columns, got {cols.Length}. Skipping line.");
                continue;
            }

            string recipeName = cols[0].Trim();
            int tier = SafeInt(cols[1], 1, ci);
            var stationList = ParseStations(cols[2]);
            var inputList = ParseInputs(cols[3], stackables);
            var outputList = ParseOutputs(cols[4], stackables);
            int craftSeconds = SafeInt(cols[5], 0, ci);
            bool unlockedDefault = SafeBool(cols[6]);
            string iconPath = cols[7].Trim();
            string unlockTag = cols[8].Trim();

            if (stationList.Count == 0)
            {
                Debug.LogError($"[Row {i + 1}] '{recipeName}': no valid stations listed.");
                continue;
            }
            if (outputList.Count == 0)
            {
                Debug.LogError($"[Row {i + 1}] '{recipeName}': no valid outputs parsed.");
                continue;
            }

            // ---------- Capability validation per station ----------
            bool stationOk = true;
            foreach (var st in stationList)
            {
                if (!ValidateAgainstCaps(st, tier, inputList, outputList, out string msg))
                {
                    Debug.LogError($"[Row {i + 1}] '{recipeName}' invalid for station '{st}': {msg}");
                    stationOk = false;
                }
            }
            if (!stationOk) continue; // skip invalid recipe row

            // ---------- Create/Update RecipeSO ----------
            string assetPath = $"{OutDir}/{Sanitize(recipeName)}.asset";
            var recipe = AssetDatabase.LoadAssetAtPath<RecipeSO>(assetPath);
            bool isNew = recipe == null;
            if (isNew)
            {
                recipe = ScriptableObject.CreateInstance<RecipeSO>();
                AssetDatabase.CreateAsset(recipe, assetPath);
            }

            recipe.recipeName = recipeName;
            recipe.tier = Mathf.Clamp(tier, 1, 9);
            recipe.stations = stationList.ToArray();
            recipe.inputs = inputList.ToArray();
            recipe.outputs = outputList.ToArray();
            recipe.craftTimeSeconds = Mathf.Max(0, craftSeconds);
            recipe.unlockedByDefault = unlockedDefault;
            recipe.unlockTag = unlockTag;

            if (!string.IsNullOrEmpty(iconPath))
                recipe.icon = AssetDatabase.LoadAssetAtPath<Sprite>(iconPath);

            EditorUtility.SetDirty(recipe);
            if (isNew) created++; else updated++;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"Recipes import complete. Created: {created}, Updated: {updated}");
    }

    // ---------- Parsing ----------
    private static List<StationType> ParseStations(string s)
    {
        var list = new List<StationType>();
        foreach (var token in s.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
        {
            var t = token.Trim();
            if (Enum.TryParse(t, true, out StationType st)) list.Add(st);
            else Debug.LogWarning($"Unknown station '{t}'");
        }
        return list;
    }

    private static List<RecipeInput> ParseInputs(string s, Dictionary<string, StackableSO> lookup)
    {
        var list = new List<RecipeInput>();
        if (string.IsNullOrWhiteSpace(s)) return list;

        foreach (var token in s.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
        {
            var t = token.Trim();
            // format: "Nx Name" with optional "~L" for liquid
            // e.g., "2x Oil~L" or "1x Plastic"
            int xIdx = t.IndexOf('x');
            if (xIdx <= 0 || !int.TryParse(t[..xIdx].Trim(), out int amt))
            {
                Debug.LogWarning($"Bad input token '{t}', expected '2x Name' (optionally '~L').");
                continue;
            }

            string namePart = t[(xIdx + 1)..].Trim();
            bool isLiquid = false;
            if (namePart.EndsWith("~L", StringComparison.OrdinalIgnoreCase))
            {
                isLiquid = true;
                namePart = namePart[..^2].Trim(); // strip "~L"
            }

            if (!lookup.TryGetValue(namePart, out var so))
            {
                Debug.LogError($"Input '{namePart}' not found among StackableSOs.");
                continue;
            }

            list.Add(new RecipeInput { ingredient = so, amount = Mathf.Max(1, amt), isLiquid = isLiquid });
        }
        return list;
    }

    private static List<RecipeOutput> ParseOutputs(string s, Dictionary<string, StackableSO> lookup)
    {
        var list = new List<RecipeOutput>();
        if (string.IsNullOrWhiteSpace(s)) return list;

        foreach (var token in s.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
        {
            var t = token.Trim();
            int xIdx = t.IndexOf('x');
            if (xIdx <= 0 || !int.TryParse(t[..xIdx].Trim(), out int amt))
            {
                Debug.LogWarning($"Bad output token '{t}', expected '2x Name'.");
                continue;
            }

            string namePart = t[(xIdx + 1)..].Trim();
            if (!lookup.TryGetValue(namePart, out var so))
            {
                // If output asset does not exist, auto-create an ItemSO for it
                var newItem = ScriptableObject.CreateInstance<ItemSO>();
                newItem.displayName = namePart;
                string itemPath = $"{ItemsDir}/{Sanitize(namePart)}.asset";
                AssetDatabase.CreateAsset(newItem, itemPath);
                lookup[namePart] = newItem;
                so = newItem;
            }

            list.Add(new RecipeOutput { product = so, amount = Mathf.Max(1, amt) });
        }
        return list;
    }

    // ---------- Validation ----------
    private static bool ValidateAgainstCaps(StationType st, int recipeTier, List<RecipeInput> inputs, List<RecipeOutput> outputs, out string message)
    {
        var caps = CAPS[st];

        if (recipeTier < caps.minTier)
        {
            message = $"recipe tier {recipeTier} < station min tier {caps.minTier}";
            return false;
        }

        // input count
        if (inputs.Count > caps.maxInputs)
        {
            message = $"inputs={inputs.Count} exceed station max={caps.maxInputs}";
            return false;
        }

        // liquid allowance
        int liquidCount = inputs.Count(x => x.isLiquid);
        if (liquidCount > 0 && !caps.allowLiquidInput)
        {
            message = "liquid inputs not allowed at this station";
            return false;
        }
        if (liquidCount > 1)
        {
            // per your rule: at most ONE liquid slot on Automated/Mass/both refineries allow multiple liquid inputs; for refineries we allow >1
            bool isRefinery = st is StationType.BasicRefinery or StationType.IndustrialRefinery or StationType.MassRefinery;
            if (!isRefinery && liquidCount > 1)
            {
                message = "only one liquid input slot allowed (non-refinery)";
                return false;
            }
        }

        // output count
        if (outputs.Count > caps.maxOutputs)
        {
            message = $"outputs={outputs.Count} exceed station max={caps.maxOutputs}";
            return false;
        }

        message = null;
        return true;
    }

    // ---------- Utils ----------
    private static string Sanitize(string s)
    {
        foreach (var c in Path.GetInvalidFileNameChars()) s = s.Replace(c, '_');
        return s;
    }

    private static void EnsureFolder(string path)
    {
        var parts = path.Split('/');
        var cur = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            var next = $"{cur}/{parts[i]}";
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(cur, parts[i]);
            cur = next;
        }
    }

    private static int SafeInt(string s, int def, IFormatProvider ci) =>
        int.TryParse(s, System.Globalization.NumberStyles.Integer, ci, out var v) ? v : def;

    private static bool SafeBool(string s)
    {
        var t = s.Trim().ToLowerInvariant();
        if (t is "1" or "true" or "yes") return true;
        if (t is "0" or "false" or "no") return false;
        return bool.TryParse(s, out var b) && b;
    }
}
#endif

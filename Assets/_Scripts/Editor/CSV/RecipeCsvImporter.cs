// Assets/_Scripts/Editor/CSV/RecipeCsvImporter.cs
#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Imports RecipeSO from CSV.
///
/// Expected columns (header names not case sensitive):
/// RecipeName, Tier, Stations, Inputs, Outputs, Time, Unlocked
///
/// - RecipeName: unique recipe asset name
/// - Tier: 1..6
/// - Stations: semicolon-separated list of RecipeSO.Station names (e.g. "BasicAssembler; StoneSmelter")
/// - Inputs:  "Iron Ore x2; Coal x1" (names must match existing ResourceSO/ItemSO asset names)
/// - Outputs: "Iron Bar x1" (allowed: ResourceSO, ItemSO, StructureSO names)
/// - Time: seconds (float)
/// - Unlocked: true/false
///
/// Target folder:
///   Recipes -> Assets/_Data/Recipes
/// </summary>
public class RecipeCsvImporter : EditorWindow
{
    [SerializeField] private TextAsset csv;
    [SerializeField] private string recipesFolder = "Assets/_Data/Recipes";

    // cache lookups by name -> object
    private Dictionary<string, ResourceSO> resourceIndex;
    private Dictionary<string, ItemSO> itemIndex;
    private Dictionary<string, StructureSO> structureIndex;

    [MenuItem("Tools/CSV Import/Recipes")]
    public static void Open()
    {
        var w = GetWindow<RecipeCsvImporter>("Import Recipes");
        w.minSize = new Vector2(620, 220);
        w.Show();
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("CSV → RecipeSO", EditorStyles.boldLabel);
        csv = (TextAsset)EditorGUILayout.ObjectField("CSV File", csv, typeof(TextAsset), false);
        recipesFolder = EditorGUILayout.TextField("Recipes Folder", recipesFolder);

        EditorGUILayout.Space();
        if (GUILayout.Button("Import CSV"))
        {
            if (csv == null)
            {
                EditorUtility.DisplayDialog("CSV Import", "Please assign a CSV TextAsset.", "OK");
                return;
            }
            IndexAll();
            Import(csv.text);
        }

        EditorGUILayout.Space();
        EditorGUILayout.HelpBox(
            "Columns: RecipeName, Tier, Stations, Inputs, Outputs, Time, Unlocked\n" +
            "Stations: semicolon-separated names (e.g., BasicAssembler; StoneSmelter)\n" +
            "Inputs:   'Name xN; Other xM' (must match existing Resource/Item asset names)\n" +
            "Outputs:  'Name xN; ...' (Resource/Item/Structure names)", MessageType.Info);
    }

    private void IndexAll()
    {
        resourceIndex = new Dictionary<string, ResourceSO>(StringComparer.OrdinalIgnoreCase);
        itemIndex = new Dictionary<string, ItemSO>(StringComparer.OrdinalIgnoreCase);
        structureIndex = new Dictionary<string, StructureSO>(StringComparer.OrdinalIgnoreCase);

        void AddAll<T>(Dictionary<string, T> dict) where T : UnityEngine.Object
        {
            var guids = AssetDatabase.FindAssets($"t:{typeof(T).Name}");
            foreach (var g in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(g);
                var obj = AssetDatabase.LoadAssetAtPath<T>(path);
                if (obj != null && !dict.ContainsKey(obj.name))
                    dict.Add(obj.name, obj);
            }
        }

        AddAll(resourceIndex);
        AddAll(itemIndex);
        AddAll(structureIndex);
    }

    private void Import(string csvText)
    {
        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();

        try
        {
            var rows = CsvTools.Parse(csvText);
            if (rows.Count == 0) { Debug.LogWarning("CSV has no rows."); return; }

            // Header map
            var header = rows[0];
            var col = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < header.Length; i++)
                col[header[i]] = i;

            int Get(string name, out int idx) => col.TryGetValue(name, out idx) ? idx : -1;

            Get("RecipeName", out var iName);
            Get("Tier", out var iTier);
            Get("Stations", out var iStations);
            Get("Inputs", out var iInputs);
            Get("Outputs", out var iOutputs);
            Get("Time", out var iTime);
            Get("Unlocked", out var iUnlocked);

            int updated = 0, warnings = 0;

            for (int r = 1; r < rows.Count; r++)
            {
                var row = rows[r];
                if (row.Length == 0) continue;

                string name = iName >= 0 && iName < row.Length ? row[iName] : null;
                if (string.IsNullOrWhiteSpace(name))
                {
                    Debug.LogWarning($"Row {r + 1}: Missing RecipeName. Skipping.");
                    continue;
                }

                int tier = iTier >= 0 && iTier < row.Length ? CsvTools.ToInt(row[iTier], 1) : 1;
                float secs = iTime >= 0 && iTime < row.Length ? CsvTools.ToFloat(row[iTime], 3f) : 3f;
                bool unlocked = iUnlocked >= 0 && iUnlocked < row.Length && CsvTools.TryParseBool(row[iUnlocked], false);

                var recipe = CsvTools.CreateOrLoad<RecipeSO>(recipesFolder, name);
                Undo.RecordObject(recipe, "CSV Import Recipe");
                recipe.recipeName = name;
                recipe.tier = Mathf.Clamp(tier, 1, 6);
                recipe.craftTimeSeconds = Mathf.Max(0.1f, secs);
                recipe.unlockedByDefault = unlocked;

                // Stations
                recipe.stations.Clear();
                if (iStations >= 0 && iStations < row.Length && !string.IsNullOrWhiteSpace(row[iStations]))
                {
                    var stations = row[iStations].Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (var s in stations)
                    {
                        if (Enum.TryParse<RecipeSO.Station>(s.Trim(), ignoreCase: true, out var st))
                            recipe.stations.Add(st);
                        else
                        {
                            warnings++;
                            Debug.LogWarning($"Recipe '{name}': Unknown station '{s}'. Skipped.");
                        }
                    }
                }
                if (recipe.stations.Count == 0)
                    recipe.stations.Add(RecipeSO.Station.BasicAssembler);

                // Inputs
                recipe.inputs.Clear();
                if (iInputs >= 0 && iInputs < row.Length)
                {
                    foreach (var (inName, amount) in CsvTools.ParseNameAmountList(row[iInputs]))
                    {
                        if (string.IsNullOrWhiteSpace(inName)) continue;
                        StackableSO stack = null;

                        if (resourceIndex.TryGetValue(inName, out var res)) stack = res;
                        else if (itemIndex.TryGetValue(inName, out var itm)) stack = itm;

                        if (stack == null)
                        {
                            warnings++;
                            Debug.LogWarning($"Recipe '{name}': Input '{inName}' not found as ResourceSO/ItemSO. Skipped.");
                            continue;
                        }
                        recipe.inputs.Add(new RecipeSO.Input { item = stack, amount = Mathf.Max(1, amount) });
                    }
                }

                // Outputs
                recipe.outputs.Clear();
                if (iOutputs >= 0 && iOutputs < row.Length)
                {
                    foreach (var (outName, amount) in CsvTools.ParseNameAmountList(row[iOutputs]))
                    {
                        if (string.IsNullOrWhiteSpace(outName)) continue;
                        UnityEngine.Object target = null;

                        if (resourceIndex.TryGetValue(outName, out var res)) target = res;
                        else if (itemIndex.TryGetValue(outName, out var itm)) target = itm;
                        else if (structureIndex.TryGetValue(outName, out var st)) target = st;

                        if (target == null)
                        {
                            warnings++;
                            Debug.LogWarning($"Recipe '{name}': Output '{outName}' not found as ResourceSO/ItemSO/StructureSO. Skipped.");
                            continue;
                        }
                        recipe.outputs.Add(new RecipeSO.Output { target = target, amount = Mathf.Max(1, amount) });
                    }
                }

                EditorUtility.SetDirty(recipe);
                updated++;
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[RecipeCsvImporter] Done. Updated: {updated}. Warnings: {warnings}.");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[RecipeCsvImporter] Import failed: {ex.Message}\n{ex.StackTrace}");
        }
    }
}
#endif

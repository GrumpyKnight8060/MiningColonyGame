// Assets/_Scripts/Editor/CSV/ResourceCsvImporter.cs
#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Imports Resources and Items from CSV.
///
/// Expected columns (header names not case sensitive):
/// Type, Name, Tier, Refined, BaseValue, MaxStack, Category
///
/// - Type: "Resource" or "Item" (defaults to Resource if empty)
/// - Name: asset name (required)
/// - Tier: 1..6
/// - Refined: true/false (only used for ResourceSO; ignored for ItemSO)
/// - BaseValue: integer economy weight (only ResourceSO)
/// - MaxStack: max stack size (StackableSO)
/// - Category: free text for ItemSO (e.g., Armor, Optics, Component)
///
/// Folder targets:
///   Resources -> Assets/_Data/Resources
///   Items     -> Assets/_Data/Items
/// </summary>
public class ResourceCsvImporter : EditorWindow
{
    [SerializeField] private TextAsset csv;
    [SerializeField] private string resourcesFolder = "Assets/_Data/Resources";
    [SerializeField] private string itemsFolder = "Assets/_Data/Items";

    [MenuItem("Tools/CSV Import/Resources & Items")]
    public static void Open()
    {
        var w = GetWindow<ResourceCsvImporter>("Import Resources & Items");
        w.minSize = new Vector2(520, 180);
        w.Show();
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("CSV → ResourceSO / ItemSO", EditorStyles.boldLabel);
        csv = (TextAsset)EditorGUILayout.ObjectField("CSV File", csv, typeof(TextAsset), false);
        resourcesFolder = EditorGUILayout.TextField("Resources Folder", resourcesFolder);
        itemsFolder = EditorGUILayout.TextField("Items Folder", itemsFolder);

        EditorGUILayout.Space();
        if (GUILayout.Button("Import CSV"))
        {
            if (csv == null)
            {
                EditorUtility.DisplayDialog("CSV Import", "Please assign a CSV TextAsset.", "OK");
                return;
            }
            Import(csv.text);
        }

        EditorGUILayout.Space();
        EditorGUILayout.HelpBox(
            "Columns: Type, Name, Tier, Refined, BaseValue, MaxStack, Category\n" +
            "Type = Resource or Item (defaults to Resource).", MessageType.Info);
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

            Get("Type", out var iType);
            Get("Name", out var iName);
            Get("Tier", out var iTier);
            Get("Refined", out var iRefined);
            Get("BaseValue", out var iBaseValue);
            Get("MaxStack", out var iMaxStack);
            Get("Category", out var iCategory);

            int created = 0, updated = 0;

            for (int r = 1; r < rows.Count; r++)
            {
                var row = rows[r];
                if (row.Length == 0) continue;

                string type = iType >= 0 && iType < row.Length ? row[iType] : "Resource";
                string name = iName >= 0 && iName < row.Length ? row[iName] : null;
                if (string.IsNullOrWhiteSpace(name))
                {
                    Debug.LogWarning($"Row {r + 1}: Missing Name. Skipping.");
                    continue;
                }

                int tier = iTier >= 0 && iTier < row.Length ? CsvTools.ToInt(row[iTier], 1) : 1;
                int maxStack = iMaxStack >= 0 && iMaxStack < row.Length ? CsvTools.ToInt(row[iMaxStack], 100) : 100;

                bool isItem = type.Equals("item", StringComparison.OrdinalIgnoreCase);

                if (isItem)
                {
                    // Create/Update ItemSO
                    var asset = CsvTools.CreateOrLoad<ItemSO>(itemsFolder, name);
                    Undo.RecordObject(asset, "CSV Import ItemSO");
                    asset.displayName = name;
                    asset.tier = Mathf.Clamp(tier, 1, 6);
                    asset.maxStack = Mathf.Max(1, maxStack);
                    if (iCategory >= 0 && iCategory < row.Length) asset.category = row[iCategory];
                    EditorUtility.SetDirty(asset);
                    updated++;
                }
                else
                {
                    // Create/Update ResourceSO
                    var asset = CsvTools.CreateOrLoad<ResourceSO>(resourcesFolder, name);
                    Undo.RecordObject(asset, "CSV Import ResourceSO");
                    asset.displayName = name;
                    asset.tier = Mathf.Clamp(tier, 1, 6);
                    asset.maxStack = Mathf.Max(1, maxStack);
                    asset.refined = iRefined >= 0 && iRefined < row.Length && CsvTools.TryParseBool(row[iRefined], false);
                    asset.baseValue = iBaseValue >= 0 && iBaseValue < row.Length ? CsvTools.ToInt(row[iBaseValue], 1) : 1;
                    EditorUtility.SetDirty(asset);
                    updated++;
                }
            }

            AssetDatabase.SaveAssets();
            Undo.CollapseUndoOperations(group);
            Debug.Log($"[ResourceCsvImporter] Done. Created/Updated: {updated}");
        }
        catch (Exception ex)
        {
            Undo.RevertAllInCurrentGroup();
            Debug.LogError($"[ResourceCsvImporter] Import failed: {ex.Message}\n{ex.StackTrace}");
        }
    }
}
#endif

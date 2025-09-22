#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// CSV → BlueprintSO
/// Columns (case-insensitive):
/// BlueprintName, Tier, Unlocks
/// - Unlocks is the exact asset name of an ItemSO or StructureSO
/// Assets are created at: Assets/_Data/Blueprints
public class BlueprintCsvImporter : EditorWindow
{
    [SerializeField] private TextAsset csv;
    [SerializeField] private string blueprintsFolder = "Assets/_Data/Blueprints";

    [MenuItem("Tools/CSV Import/Blueprints")]
    public static void Open()
    {
        var w = GetWindow<BlueprintCsvImporter>("Import Blueprints");
        w.minSize = new Vector2(520, 160);
        w.Show();
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("CSV → BlueprintSO", EditorStyles.boldLabel);
        csv = (TextAsset)EditorGUILayout.ObjectField("CSV File", csv, typeof(TextAsset), false);
        blueprintsFolder = EditorGUILayout.TextField("Blueprints Folder", blueprintsFolder);

        EditorGUILayout.Space();
        if (GUILayout.Button("Import CSV"))
        {
            if (csv == null) { EditorUtility.DisplayDialog("CSV Import", "Please assign a CSV TextAsset.", "OK"); return; }
            Import(csv.text);
        }

        EditorGUILayout.Space();
        EditorGUILayout.HelpBox("Columns: BlueprintName, Tier, Unlocks (ItemSO or StructureSO name).", MessageType.Info);
    }

    private void Import(string csvText)
    {
        Undo.IncrementCurrentGroup();
        var group = Undo.GetCurrentGroup();
        try
        {
            var rows = CsvTools.Parse(csvText);
            if (rows.Count == 0) { Debug.LogWarning("CSV has no rows."); return; }

            // header map
            var header = rows[0];
            var col = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < header.Length; i++) col[header[i]] = i;

            int Get(string name, out int idx) => col.TryGetValue(name, out idx) ? idx : -1;
            Get("BlueprintName", out var iName);
            Get("Tier", out var iTier);
            Get("Unlocks", out var iUnlocks);

            int updated = 0, missing = 0;
            for (int r = 1; r < rows.Count; r++)
            {
                var row = rows[r];
                if (row.Length == 0) continue;

                string name = iName >= 0 && iName < row.Length ? row[iName] : null;
                if (string.IsNullOrWhiteSpace(name)) { Debug.LogWarning($"Row {r + 1}: Missing BlueprintName."); continue; }

                int tier = iTier >= 0 && iTier < row.Length ? CsvTools.ToInt(row[iTier], 1) : 1;
                string unlockName = iUnlocks >= 0 && iUnlocks < row.Length ? row[iUnlocks] : null;

                // find ItemSO or StructureSO by name
                UnityEngine.Object unlockTarget =
                    CsvTools.FindByName<ItemSO>(unlockName) as UnityEngine.Object ??
                    CsvTools.FindByName<StructureSO>(unlockName) as UnityEngine.Object;

                if (unlockTarget == null)
                {
                    missing++;
                    Debug.LogWarning($"Blueprint '{name}': Unlock target '{unlockName}' not found as ItemSO/StructureSO.");
                    continue;
                }

                // create/update BlueprintSO
                var bp = CsvTools.CreateOrLoad<BlueprintSO>(blueprintsFolder, name);
                Undo.RecordObject(bp, "CSV Import Blueprint");
                bp.blueprintName = name;
                bp.tier = Mathf.Clamp(tier, 1, 6);
                bp.unlocks = unlockTarget;
                EditorUtility.SetDirty(bp);
                updated++;
            }

            AssetDatabase.SaveAssets();
            Undo.CollapseUndoOperations(group);
            Debug.Log($"[BlueprintCsvImporter] Done. Updated: {updated}. Missing unlock targets: {missing}.");
        }
        catch (Exception ex)
        {
            Undo.RevertAllInCurrentGroup();
            Debug.LogError($"[BlueprintCsvImporter] Import failed: {ex.Message}\n{ex.StackTrace}");
        }
    }
}
#endif

#if UNITY_EDITOR
using System.Globalization;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class ResourcesImporter
{
    private const string CsvPath = "Assets/_Data/Import/resources.csv";
    private const string ResourcesDir = "Assets/_Data/Resources";

    [MenuItem("Tools/Data Import/Import Resources CSV")]
    public static void ImportResources()
    {
        if (!File.Exists(CsvPath))
        {
            Debug.LogError($"resources.csv not found at {CsvPath}");
            return;
        }

        // Ensure target folder exists
        if (!AssetDatabase.IsValidFolder(ResourcesDir))
        {
            var parent = "Assets/_Data";
            if (!AssetDatabase.IsValidFolder(parent)) AssetDatabase.CreateFolder("Assets", "_Data");
            AssetDatabase.CreateFolder(parent, "Resources");
        }

        var lines = File.ReadAllLines(CsvPath);
        if (lines.Length <= 1)
        {
            Debug.LogWarning("resources.csv has no data rows.");
            return;
        }

        var ci = CultureInfo.InvariantCulture;
        int created = 0, updated = 0;

        // CSV: name,category,tier,isRefined,baseValue,rarityWeight,maxStack,notes
        for (int i = 1; i < lines.Length; i++)
        {
            var line = lines[i].Trim();
            if (string.IsNullOrWhiteSpace(line)) continue;

            // Simple splitter (no quoted commas in our data)
            var cols = line.Split(',');
            if (cols.Length < 8)
            {
                Debug.LogWarning($"Row {i + 1}: expected 8 columns, got {cols.Length}. Skipping.");
                continue;
            }

            string name = cols[0].Trim();
            // string category = cols[1].Trim(); // not used by the SO, only for organization
            int tier = SafeInt(cols[2], 1);
            bool isRefined = SafeBool(cols[3]);
            int baseValue = SafeInt(cols[4], 1);
            float rarity = SafeFloat(cols[5], 0f, ci);
            int maxStack = SafeInt(cols[6], 9999);
            // string notes    = cols[7]; // not stored

            string assetPath = $"{ResourcesDir}/{Sanitize(name)}.asset";
            var res = AssetDatabase.LoadAssetAtPath<ResourceSO>(assetPath);
            bool isNew = res == null;
            if (isNew)
            {
                res = ScriptableObject.CreateInstance<ResourceSO>();
                AssetDatabase.CreateAsset(res, assetPath);
            }

            // Apply fields
            res.displayName = name;
            res.tier = Mathf.Clamp(tier, 1, 9);
            res.isRefined = isRefined;
            res.baseValue = baseValue;
            res.rarityWeight = isRefined ? 0f : Mathf.Clamp01(rarity); // refined items aren’t rolled at mines
            res.maxStack = Mathf.Max(1, maxStack);

            EditorUtility.SetDirty(res);
            if (isNew) created++; else updated++;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"Resources import complete. Created: {created}, Updated: {updated}");
    }

    private static string Sanitize(string s)
    {
        foreach (var c in Path.GetInvalidFileNameChars()) s = s.Replace(c, '_');
        return s;
    }

    private static int SafeInt(string s, int def) =>
        int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : def;

    private static float SafeFloat(string s, float def, CultureInfo ci) =>
        float.TryParse(s, NumberStyles.Float | NumberStyles.AllowThousands, ci, out var v) ? v : def;

    private static bool SafeBool(string s)
    {
        // accept true/false, True/False, 1/0, yes/no
        var t = s.Trim().ToLowerInvariant();
        if (t == "1" || t == "true" || t == "yes") return true;
        if (t == "0" || t == "false" || t == "no") return false;
        bool.TryParse(s, out var b);
        return b;
    }
}
#endif

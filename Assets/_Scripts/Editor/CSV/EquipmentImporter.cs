#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

public class EquipmentImporter : EditorWindow
{
    [SerializeField] private TextAsset csv;
    [SerializeField] private string itemsFolder = "Assets/_Data/Items";
    [SerializeField] private string blueprintsFolder = "Assets/_Data/Blueprints";
    [SerializeField] private bool autoCreateBlueprints = true;

    private static readonly string[] AllowedTypes = { "Weapon", "Armor", "Pack", "Optic", "Item", "Equipment" };
    private static readonly Dictionary<string, string> TypeToClass =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "Weapon",    "WeaponSO" },
            { "Armor",     "ArmorSO"  },
            { "Pack",      "PackSO"   },
            { "Optic",     "OpticSO"  },
            { "Item",      "ItemSO"   },
            { "Equipment", "ItemSO"   },
        };

    private const string COL_TYPE = "Type";
    private const string COL_NAME = "Item";
    private const string COL_TIER = "Tier";
    private const string COL_RAR = "Rarity";
    private const string COL_SLOT = "Equip Slot";
    private const string COL_PWR_T = "Power Type";
    private const string COL_BP = "Blueprint"; // optional

    private static readonly string[] ClassCols = { "Miner", "Soldier", "Transporter", "Technician", "Explorer" };

    [MenuItem("Tools/CSV Import/Equipment (Workbook Header)")]
    public static void Open()
    {
        var w = GetWindow<EquipmentImporter>("Equipment Import (Workbook)");
        w.minSize = new Vector2(820, 260);
        w.Show();
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Import Equipment from workbook CSV (exact header; single class block).", EditorStyles.wordWrappedLabel);
        csv = (TextAsset)EditorGUILayout.ObjectField("CSV File", csv, typeof(TextAsset), false);
        itemsFolder = EditorGUILayout.TextField("Items Folder", itemsFolder);
        blueprintsFolder = EditorGUILayout.TextField("Blueprints Folder", blueprintsFolder);
        autoCreateBlueprints = EditorGUILayout.Toggle("Auto-create Blueprints", autoCreateBlueprints);

        GUILayout.Space(8);
        if (GUILayout.Button("Import CSV"))
        {
            if (csv == null) { EditorUtility.DisplayDialog("Import", "Assign a CSV TextAsset.", "OK"); return; }
            EnsureFolder(itemsFolder);
            EnsureFolder(blueprintsFolder);
            Import(csv.text, itemsFolder, blueprintsFolder, autoCreateBlueprints);
        }

        GUILayout.Space(8);
        EditorGUILayout.HelpBox(
            "Required: Type, Item, Tier.\n" +
            "Whitespace in headers is normalized (so \" Durability \" works).\n" +
            "Equip Slot is also added as a tag. Miner..Explorer flags populate `usableBy`.\n" +
            "Unknown Type values fall back to ItemSO with a warning.", MessageType.Info);
    }

    private static void Import(string text, string itemsFolder, string blueprintsFolder, bool createBps)
    {
        var rows = ParseCsv(text);
        if (rows.Count == 0) { Debug.LogWarning("CSV appears empty."); return; }

        var header = rows[0];
        var map = BuildHeaderMap(header);

        if (!map.ContainsKey(COL_TYPE)) { Debug.LogError("CSV missing required header: Type"); return; }
        if (!map.ContainsKey(COL_NAME)) { Debug.LogError("CSV missing required header: Item"); return; }
        if (!map.ContainsKey(COL_TIER)) { Debug.LogError("CSV missing required header: Tier"); return; }

        var classIdx = new int[ClassCols.Length];
        for (int i = 0; i < ClassCols.Length; i++)
            classIdx[i] = map.TryGetValue(ClassCols[i], out var idx) ? idx : -1;

        int created = 0, updated = 0, bpMade = 0, errors = 0;

        for (int r = 1; r < rows.Count; r++)
        {
            var row = rows[r];
            if (row.Count == 0) continue;

            // local accessor that ALWAYS uses the header map (the previous bug)
            string get(string key) => Get(row, map, key);

            string type = get(COL_TYPE);
            string name = get(COL_NAME);
            if (string.IsNullOrWhiteSpace(type) || string.IsNullOrWhiteSpace(name))
            {
                Debug.LogWarning($"Row {r + 1}: missing Type or Item."); continue;
            }

            string className;
            if (!AllowedTypes.Contains(type, StringComparer.OrdinalIgnoreCase))
            {
                Debug.LogWarning($"Row {r + 1} ({name}): unknown Type '{type}'. Falling back to ItemSO.");
                className = "ItemSO";
            }
            else
            {
                className = TypeToClass.TryGetValue(type, out var cn) ? cn : "ItemSO";
            }

            try
            {
                var assetType = FindTypeOrDefault(className, typeof(ScriptableObject));
                if (assetType == null || assetType == typeof(ScriptableObject))
                    assetType = FindTypeOrDefault("ItemSO", typeof(ScriptableObject));

                string path = $"{itemsFolder}/{Sanitize(name)}.asset";
                var existing = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path);
                bool existed = existing != null;
                var asset = existed ? existing : CreateSO(assetType, path);

                // Basic fields
                TrySet(asset, new[] { "itemName", "displayName", "resourceName" }, name);
                TrySetInt(asset, "tier", ToInt(get(COL_TIER), 1));

                // Rarity
                var rarity = get(COL_RAR);
                if (!string.IsNullOrWhiteSpace(rarity))
                {
                    if (!TrySetEnum(asset, "rarity", rarity)) TrySet(asset, "rarity", rarity);
                }

                // Slot (field + tag)
                var slotRaw = get(COL_SLOT).Trim();
                if (!string.IsNullOrWhiteSpace(slotRaw))
                {
                    if (!TrySetEnum(asset, "slot", slotRaw)) TrySet(asset, "slot", slotRaw);
                }

                // Power Type
                var pwrType = get(COL_PWR_T);
                if (!string.IsNullOrWhiteSpace(pwrType))
                {
                    if (!TrySetEnum(asset, "powerType", pwrType)) TrySet(asset, "powerType", pwrType);
                }

                // Stats (NOW all use the mapped getter)
                TrySetFloat(asset, "oreProductionPerHour", ToFloat(get("Ore Production Per Hour"), 0));
                TrySetInt(asset, "itemCarryCapacityIncrease", ToInt(get("Item Carry Capacity Increase"), 0));
                TrySetInt(asset, "fluidCarryCapacityIncrease", ToInt(get("Fluid Carry Capacity Increase"), 0));

                TrySetInt(asset, "defense", ToInt(get("Defense"), 0));
                TrySetFloat(asset, "defenseMultiplier", ToFloat(get("Defense Multiplier"), 0));
                TrySetFloat(asset, "movementSpeedMultiplier", ToFloat(get("Movement Speed Multiplier"), 1f));
                TrySetFloat(asset, "fatigueMultiplier", ToFloat(get("Fatigue Multiplier"), 1f));

                TrySetFloat(asset, "powerCostPerHour", ToFloat(get("Power Cost Per Hour"), 0));
                TrySetFloat(asset, "powerCostPerUse", ToFloat(get("Power Cost Per Use"), 0));
                TrySetFloat(asset, "powerCostPerTile", ToFloat(get("Power Cost Per Tile"), 0));

                TrySetFloat(asset, "poiDiscoveryMultiplier", ToFloat(get("POI Discovery Multiplier"), 0));

                TrySetFloat(asset, "attackPower", ToFloat(get("Atk Pwr"), 0));
                TrySetFloat(asset, "attacksPerSec", ToFloat(get("Atk / Sec"), 0));
                TrySetFloat(asset, "range", ToFloat(get("Range"), 0));
                TrySetFloat(asset, "accuracy", ToFloat(get("Accuracy"), 0));
                TrySetFloat(asset, "critChance", ToFloat(get("Crit Chance"), 0));
                TrySetFloat(asset, "critDamage", ToFloat(get("Crit Damage"), 0));
                TrySetFloat(asset, "aoe", ToFloat(get("AOE"), 0));

                TrySetFloat(asset, "repairPerHour", ToFloat(get("Repair Per Hour"), 0));
                TrySetFloat(asset, "repairPerHourMultiplier", ToFloat(get("Repair Per Hour Multiplier"), 0));
                TrySetFloat(asset, "structureDecayReductionMultiplier", ToFloat(get("Structure Decay Reduction Multiplier"), 0));
                TrySetFloat(asset, "structureEfficiencyMultiplier", ToFloat(get("Structure Efficiency Multiplier"), 0));

                TrySetInt(asset, "durability", ToInt(get("Durability"), 0)); // header whitespace normalized
                TrySetFloat(asset, "decayPerHour", ToFloat(get("Decay Per Hour"), 0));
                TrySetFloat(asset, "decayPerUse", ToFloat(get("Decay Per Use"), 0));

                // UsableBy
                var usableList = new List<string>();
                for (int i = 0; i < ClassCols.Length; i++)
                {
                    int idx = classIdx[i];
                    if (idx < 0) continue;
                    string cell = GetByIndexRaw(row, idx);
                    if (IsTrue(cell)) usableList.Add(ClassCols[i]);
                }
                if (usableList.Count > 0)
                {
                    if (!TrySetEnumFlagsOrList(asset, "usableBy", usableList))
                    {
                        if (!TrySetStringArray(asset, "usableBy", usableList))
                            if (!TrySetStringList(asset, "usableBy", usableList))
                                TrySet(asset, "usableBy", string.Join(";", usableList));
                    }
                }

                // Ensure slot as tag
                if (!string.IsNullOrWhiteSpace(slotRaw))
                {
                    var oneTag = new List<string> { slotRaw };
                    if (!TrySetStringArrayAppend(asset, "tags", oneTag))
                        if (!TrySetStringListAppend(asset, "tags", oneTag))
                            TryAppendCsvField(asset, "tagsCsv", slotRaw);
                }

                EditorUtility.SetDirty(asset);
                if (!existed) created++; else updated++;

                // Optional blueprint creation
                if (map.ContainsKey(COL_BP))
                {
                    bool bpFlag = IsTrue(get(COL_BP));
                    if (bpFlag && createBps)
                    {
                        var bpName = $"BP: {name}";
                        var bpPath = $"{blueprintsFolder}/{Sanitize(bpName)}.asset";
                        var bp = AssetDatabase.LoadAssetAtPath<BlueprintSO>(bpPath);
                        if (bp == null)
                        {
                            bp = ScriptableObject.CreateInstance<BlueprintSO>();
                            AssetDatabase.CreateAsset(bp, bpPath);
                        }
                        TrySet(bp, "blueprintName", bpName);
                        TrySetInt(bp, "tier", ToInt(get(COL_TIER), 1));
                        TrySetUnity(bp, "unlocks", asset);
                        EditorUtility.SetDirty(bp);
                        bpMade++;
                    }
                }
            }
            catch (Exception ex)
            {
                errors++;
                Debug.LogError($"Row {r + 1} ({name}): {ex.Message}\n{ex.StackTrace}");
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[EquipmentImporter] Created: {created}, Updated: {updated}, Blueprints: {bpMade}, Errors: {errors}");
    }

    // ------------- CSV helpers -------------
    private static List<List<string>> ParseCsv(string text)
    {
        var lines = text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
        var rows = new List<List<string>>();
        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            rows.Add(Split(line));
        }
        return rows;
    }

    private static List<string> Split(string line)
    {
        var list = new List<string>();
        bool inQ = false;
        var cur = new System.Text.StringBuilder();
        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];
            if (c == '"')
            {
                if (inQ && i + 1 < line.Length && line[i + 1] == '"') { cur.Append('"'); i++; }
                else inQ = !inQ;
            }
            else if (!inQ && c == ',')
            {
                list.Add(cur.ToString().Trim());
                cur.Length = 0;
            }
            else cur.Append(c);
        }
        list.Add(cur.ToString().Trim());
        return list;
    }

    private static Dictionary<string, int> BuildHeaderMap(List<string> header)
    {
        var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < header.Count; i++)
        {
            var raw = header[i] ?? "";
            var key = Regex.Replace(raw, @"\s+", " ").Trim();
            if (string.IsNullOrEmpty(key)) continue;

            if (key.Equals("Durability", StringComparison.OrdinalIgnoreCase))
                key = "Durability";

            if (!map.ContainsKey(key)) map[key] = i;
        }
        return map;
    }

    private static string Get(List<string> row, Dictionary<string, int> map, string key)
        => map.TryGetValue(key, out var idx) ? GetByIndexRaw(row, idx) : "";

    private static string GetByIndexRaw(List<string> row, int idx)
        => (idx >= 0 && idx < row.Count) ? (row[idx] ?? "").Trim() : "";

    // ------------- reflection setters -------------
    private static void TrySet(UnityEngine.Object obj, string field, object value)
    {
        if (obj == null) return;
        var f = obj.GetType().GetField(field, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        if (f != null && (value == null || f.FieldType.IsInstanceOfType(value))) f.SetValue(obj, value);
    }
    private static void TrySet(UnityEngine.Object obj, string[] fields, object value)
    {
        foreach (var name in fields)
        {
            var f = obj.GetType().GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (f != null && f.FieldType == typeof(string)) { f.SetValue(obj, value?.ToString()); return; }
        }
    }
    private static void TrySetInt(UnityEngine.Object obj, string field, int value)
    {
        var f = obj.GetType().GetField(field, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        if (f != null && f.FieldType == typeof(int)) f.SetValue(obj, value);
    }
    private static void TrySetFloat(UnityEngine.Object obj, string field, float value)
    {
        var f = obj.GetType().GetField(field, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        if (f != null && f.FieldType == typeof(float)) f.SetValue(obj, value);
    }
    private static void TrySetUnity(UnityEngine.Object obj, string field, UnityEngine.Object value)
    {
        var f = obj.GetType().GetField(field, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        if (f != null && typeof(UnityEngine.Object).IsAssignableFrom(f.FieldType)) f.SetValue(obj, value);
    }
    private static bool TrySetEnum(UnityEngine.Object obj, string field, string value)
    {
        var f = obj.GetType().GetField(field, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        if (f == null || !f.FieldType.IsEnum) return false;
        try
        {
            var names = Enum.GetNames(f.FieldType);
            var match = names.FirstOrDefault(n => n.Equals(value, StringComparison.OrdinalIgnoreCase));
            if (match != null) { f.SetValue(obj, Enum.Parse(f.FieldType, match)); return true; }
        }
        catch { }
        return false;
    }
    private static bool TrySetEnumFlagsOrList(UnityEngine.Object obj, string field, List<string> tokens)
    {
        var f = obj.GetType().GetField(field, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        if (f == null) return false;
        var ft = f.FieldType;

        if (ft.IsEnum && Attribute.IsDefined(ft, typeof(FlagsAttribute)))
        {
            long acc = 0;
            foreach (var tok in tokens)
            {
                var name = Enum.GetNames(ft).FirstOrDefault(n => n.Equals(tok, StringComparison.OrdinalIgnoreCase));
                if (name != null) acc |= Convert.ToInt64(Enum.Parse(ft, name));
            }
            f.SetValue(obj, Enum.ToObject(ft, acc)); return true;
        }
        if (ft.IsArray && ft.GetElementType().IsEnum)
        {
            var et = ft.GetElementType();
            var vals = tokens.Select(t => Enum.GetNames(et).FirstOrDefault(n => n.Equals(t, StringComparison.OrdinalIgnoreCase)))
                             .Where(n => n != null).Select(n => Enum.Parse(et, n)).ToArray();
            var arr = Array.CreateInstance(et, vals.Length);
            for (int i = 0; i < vals.Length; i++) arr.SetValue(vals[i], i);
            f.SetValue(obj, arr); return true;
        }
        if (ft.IsGenericType && ft.GetGenericTypeDefinition() == typeof(List<>) && ft.GetGenericArguments()[0].IsEnum)
        {
            var et = ft.GetGenericArguments()[0];
            var list = (System.Collections.IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(et));
            foreach (var t in tokens)
            {
                var name = Enum.GetNames(et).FirstOrDefault(n => n.Equals(t, StringComparison.OrdinalIgnoreCase));
                if (name != null) list.Add(Enum.Parse(et, name));
            }
            f.SetValue(obj, list); return true;
        }
        return false;
    }
    private static bool TrySetStringArray(UnityEngine.Object obj, string field, List<string> tokens)
    {
        var f = obj.GetType().GetField(field, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        if (f == null) return false;
        if (f.FieldType == typeof(string[])) { f.SetValue(obj, tokens.ToArray()); return true; }
        return false;
    }
    private static bool TrySetStringList(UnityEngine.Object obj, string field, List<string> tokens)
    {
        var f = obj.GetType().GetField(field, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        if (f == null) return false;
        if (f.FieldType.IsGenericType && f.FieldType.GetGenericTypeDefinition() == typeof(List<>) &&
            f.FieldType.GetGenericArguments()[0] == typeof(string))
        {
            var list = (System.Collections.IList)Activator.CreateInstance(f.FieldType);
            foreach (var s in tokens) list.Add(s);
            f.SetValue(obj, list); return true;
        }
        return false;
    }
    private static bool TrySetStringArrayAppend(UnityEngine.Object obj, string field, List<string> toAdd)
    {
        var f = obj.GetType().GetField(field, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        if (f == null || f.FieldType != typeof(string[])) return false;
        var cur = (string[])f.GetValue(obj) ?? Array.Empty<string>();
        var set = new HashSet<string>(cur, StringComparer.OrdinalIgnoreCase);
        foreach (var t in toAdd) set.Add(t);
        f.SetValue(obj, set.ToArray());
        return true;
    }
    private static bool TrySetStringListAppend(UnityEngine.Object obj, string field, List<string> toAdd)
    {
        var f = obj.GetType().GetField(field, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        if (f == null) return false;

        if (f.FieldType.IsGenericType &&
            f.FieldType.GetGenericTypeDefinition() == typeof(List<>) &&
            f.FieldType.GetGenericArguments()[0] == typeof(string))
        {
            var list = (System.Collections.IList)f.GetValue(obj);
            if (list == null)
            {
                list = (System.Collections.IList)Activator.CreateInstance(f.FieldType);
                f.SetValue(obj, list);
            }

            var existing = new HashSet<string>(
                list.Cast<object>().Select(o => (string)o),
                StringComparer.OrdinalIgnoreCase
            );

            foreach (var s in toAdd)
                if (!existing.Contains(s)) list.Add(s);

            return true;
        }

        return false;
    }
    private static void TryAppendCsvField(UnityEngine.Object obj, string field, string append)
    {
        var f = obj.GetType().GetField(field, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        if (f == null || f.FieldType != typeof(string)) return;
        var cur = (string)f.GetValue(obj) ?? "";
        var parts = new List<string>(cur.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()));
        if (!parts.Any(p => p.Equals(append, StringComparison.OrdinalIgnoreCase))) parts.Add(append);
        f.SetValue(obj, string.Join(";", parts));
    }

    private static void EnsureFolder(string path)
    {
        var parts = path.Split('/');
        if (parts.Length == 0 || parts[0] != "Assets") return;
        string cur = "Assets";
        for (int i = 1; i < parts.Length; i++)
        {
            if (!AssetDatabase.IsValidFolder($"{cur}/{parts[i]}"))
                AssetDatabase.CreateFolder(cur, parts[i]);
            cur = $"{cur}/{parts[i]}";
        }
    }
    private static Type FindTypeOrDefault(string typeName, Type defaultType)
    {
        var t = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType(typeName, false)).FirstOrDefault(x => x != null);
        return t ?? defaultType;
    }
    private static UnityEngine.Object CreateSO(Type t, string path)
    {
        var so = ScriptableObject.CreateInstance(t) as ScriptableObject;
        AssetDatabase.CreateAsset(so, path);
        return so;
    }
    private static string Sanitize(string name)
    {
        foreach (var ch in Path.GetInvalidFileNameChars()) name = name.Replace(ch, '_');
        return name;
    }
    private static int ToInt(string s, int defVal) => int.TryParse(s, out var v) ? v : defVal;
    private static float ToFloat(string s, float defVal) => float.TryParse(s, out var v) ? v : defVal;
    private static bool IsTrue(string s)
    {
        s = (s ?? "").Trim().ToLowerInvariant();
        return s == "true" || s == "yes" || s == "y" || s == "1" || s == "x";
    }
}
#endif

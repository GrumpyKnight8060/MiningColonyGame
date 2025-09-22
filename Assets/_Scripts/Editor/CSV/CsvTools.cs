// Assets/_Scripts/Editor/CSV/CsvTools.cs
#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

internal static class CsvTools
{
    // -------- Basic CSV parsing (no external deps) --------
    // Supports quoted fields, commas in quotes, and \n line breaks.
    public static List<string[]> Parse(string csvText)
    {
        var rows = new List<string[]>();
        if (string.IsNullOrEmpty(csvText)) return rows;

        using (var reader = new StringReader(csvText))
        {
            string line;
            var current = new List<string>();
            bool inQuotes = false;
            var cell = new System.Text.StringBuilder();

            void PushCell()
            {
                current.Add(cell.ToString());
                cell.Length = 0;
            }
            void PushRow()
            {
                rows.Add(current.ToArray());
                current.Clear();
            }

            while (true)
            {
                line = reader.ReadLine();
                if (line == null)
                {
                    if (inQuotes) { cell.Append('\n'); continue; } // ended while in quotes -> continue reading (shouldn't happen)
                    if (cell.Length > 0 || current.Count > 0) { PushCell(); PushRow(); }
                    break;
                }

                for (int i = 0; i < line.Length; i++)
                {
                    char c = line[i];
                    if (inQuotes)
                    {
                        if (c == '"')
                        {
                            bool nextIsQuote = (i + 1 < line.Length && line[i + 1] == '"');
                            if (nextIsQuote) { cell.Append('"'); i++; }
                            else { inQuotes = false; }
                        }
                        else cell.Append(c);
                    }
                    else
                    {
                        if (c == ',') { PushCell(); }
                        else if (c == '"') { inQuotes = true; }
                        else cell.Append(c);
                    }
                }

                if (inQuotes)
                {
                    cell.Append('\n'); // multi-line cell
                }
                else
                {
                    PushCell();
                    PushRow();
                }
            }
        }

        // Trim whitespace in all fields
        for (int r = 0; r < rows.Count; r++)
            for (int c = 0; c < rows[r].Length; c++)
                rows[r][c] = rows[r][c].Trim();
        return rows;
    }

    // -------- Asset helpers --------
    public static void EnsureFolder(string path)
    {
        // Example path: "Assets/_Data/Resources"
        var parts = path.Split('/');
        if (parts.Length < 2 || parts[0] != "Assets") return;

        string cur = "Assets";
        for (int i = 1; i < parts.Length; i++)
        {
            string next = parts[i];
            if (!AssetDatabase.IsValidFolder($"{cur}/{next}"))
                AssetDatabase.CreateFolder(cur, next);
            cur = $"{cur}/{next}";
        }
    }

    public static T FindByName<T>(string name) where T : UnityEngine.Object
    {
        if (string.IsNullOrEmpty(name)) return null;
        // Exact name match first (without extension)
        var guids = AssetDatabase.FindAssets($"{Path.GetFileNameWithoutExtension(name)} t:{typeof(T).Name}");
        foreach (var g in guids)
        {
            var path = AssetDatabase.GUIDToAssetPath(g);
            var obj = AssetDatabase.LoadAssetAtPath<T>(path);
            if (obj != null && string.Equals(obj.name, name, StringComparison.OrdinalIgnoreCase))
                return obj;
        }
        // Fallback: first type match
        foreach (var g in guids)
        {
            var path = AssetDatabase.GUIDToAssetPath(g);
            var obj = AssetDatabase.LoadAssetAtPath<T>(path);
            if (obj != null) return obj;
        }
        return null;
    }

    public static T CreateOrLoad<T>(string folder, string assetName) where T : ScriptableObject
    {
        EnsureFolder(folder);
        string assetPath = $"{folder}/{SanitizeFileName(assetName)}.asset";
        var existing = AssetDatabase.LoadAssetAtPath<T>(assetPath);
        if (existing != null) return existing;

        var inst = ScriptableObject.CreateInstance<T>();
        inst.name = assetName;
        AssetDatabase.CreateAsset(inst, assetPath);
        return inst;
    }

    public static string SanitizeFileName(string raw)
    {
        if (string.IsNullOrEmpty(raw)) return "NewAsset";
        var invalid = Path.GetInvalidFileNameChars();
        var safe = new string(raw.Select(ch => invalid.Contains(ch) ? '_' : ch).ToArray());
        return safe.Trim();
    }

    // Converts "A x2; B x3" -> (name,amount) list
    public static List<(string name, int amount)> ParseNameAmountList(string field)
    {
        var list = new List<(string name, int amount)>();
        if (string.IsNullOrWhiteSpace(field)) return list;

        var parts = field.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
        foreach (var p in parts)
        {
            var s = p.Trim();
            // Accept "Name xN" or "Nx Name" or just "Name"
            int amount = 1;
            string name = s;
            // Try suffix " xN"
            var idx = s.LastIndexOf('x');
            if (idx > 0)
            {
                var maybe = s.Substring(idx + 1).Trim();
                if (int.TryParse(maybe, out var n))
                {
                    amount = n;
                    name = s.Substring(0, idx).Trim().TrimEnd();
                }
            }
            list.Add((name, Mathf.Max(1, amount)));
        }
        return list;
    }

    public static bool TryParseBool(string s, bool defaultVal) =>
        string.IsNullOrWhiteSpace(s) ? defaultVal :
        s.Equals("true", StringComparison.OrdinalIgnoreCase) ||
        s.Equals("yes", StringComparison.OrdinalIgnoreCase) ||
        s.Equals("1");

    public static int ToInt(string s, int def = 0) =>
        int.TryParse(s, out var v) ? v : def;

    public static float ToFloat(string s, float def = 0f) =>
        float.TryParse(s, out var v) ? v : def;
}
#endif

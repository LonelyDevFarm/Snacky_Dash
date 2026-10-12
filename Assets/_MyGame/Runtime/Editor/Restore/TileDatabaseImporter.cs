#if UNITY_EDITOR
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using SnackDash.LevelSystem;
using UnityEditor;
using UnityEngine;

namespace SnackDash.Editor.Restore
{
    /// <summary>
    /// Converts the original TileDefinitionDatabaseVO.asset (YAML) into our TileDefinitionDatabase,
    /// resolving each Resources cover path to the matching prefab imported under Prefabs/Original.
    /// </summary>
    public static class TileDatabaseImporter
    {
        private const string SourcePath = "G:/Game_2D/APK/ExportedProject/Assets/MonoBehaviour/TileDefinitionDatabaseVO.asset";
        private const string PrefabRoot = "Assets/_MyGame/Prefabs/InGame";
        private const string OutputPath = "Assets/_MyGame/Data/Levels/TileDefinitionDatabase.asset";

        [MenuItem("SnackDash/Restore/Import Tile Database")]
        public static void Run()
        {
            var prefabsByResourcePath = AssetDatabase.FindAssets("t:Prefab", new[] { PrefabRoot })
                .Select(AssetDatabase.GUIDToAssetPath)
                .ToDictionary(p => p.Substring(PrefabRoot.Length + 1, p.Length - PrefabRoot.Length - 1 - ".prefab".Length).ToLowerInvariant(),
                              p => AssetDatabase.LoadAssetAtPath<GameObject>(p));

            var entries = new List<TileDefinition>();
            var unresolved = new List<string>();
            TileDefinition current = null;

            foreach (var rawLine in File.ReadLines(SourcePath))
            {
                var line = rawLine.Trim();
                if (line.StartsWith("- name:"))
                {
                    current = new TileDefinition { name = Value(line.Substring(2)).Trim('\'').Trim() };
                    entries.Add(current);
                    continue;
                }
                if (current == null) continue;

                var key = line.Split(':')[0];
                var value = Value(line);
                switch (key)
                {
                    case "tiledId": current.tiledId = Int(value); break;
                    case "tileType": current.tileType = (TileType)Int(value); break;
                    case "category": current.category = (TileCategory)Int(value); break;
                    case "coverZRotation": current.coverZRotation = float.Parse(value, CultureInfo.InvariantCulture); break;
                    case "spawnOverlayTunnelBoosterTile": current.spawnOverlayTunnelBoosterTile = Int(value) != 0; break;
                    case "deliverPointIndex": current.deliverPointIndex = Int(value); break;
                    case "tunnelDirection": current.tunnelDirection = Int(value); break;
                    case "tunnelIndex": current.tunnelIndex = Int(value); break;
                    case "arrowIsFixed": current.arrowIsFixed = Int(value) != 0; break;
                    case "arrowIsUnique": current.arrowIsUnique = Int(value) != 0; break;
                    case "arrowGroupId": current.arrowGroupId = Int(value); break;
                    case "arrowDirection": current.arrowDirection = Int(value); break;
                    case "coverPrefabResourcePath":
                        if (value.Length == 0) break;
                        if (prefabsByResourcePath.TryGetValue(value.ToLowerInvariant(), out var prefab)) current.coverPrefab = prefab;
                        else unresolved.Add($"{current.tiledId} {current.name}: {value}");
                        break;
                }
            }

            var database = AssetDatabase.LoadAssetAtPath<TileDefinitionDatabase>(OutputPath);
            if (database == null)
            {
                database = ScriptableObject.CreateInstance<TileDefinitionDatabase>();
                AssetDatabase.CreateAsset(database, OutputPath);
            }
            database.SetEntries(entries);
            EditorUtility.SetDirty(database);
            AssetDatabase.SaveAssets();

            Debug.Log($"[Restore] Tile database: {entries.Count} entries, {entries.Count(e => e.coverPrefab != null)} with cover prefab, " +
                      $"{unresolved.Count} unresolved cover paths.\n{string.Join("\n", unresolved)}", database);
        }

        private static string Value(string line)
        {
            int colon = line.IndexOf(':');
            return colon < 0 ? string.Empty : line.Substring(colon + 1).Trim();
        }

        private static int Int(string value) => int.Parse(value, CultureInfo.InvariantCulture);
    }
}
#endif

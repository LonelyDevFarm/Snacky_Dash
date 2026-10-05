#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace SnackDash.Editor.Restore
{
    /// <summary>
    /// Copies original prefabs (and the assets they need) from the AssetRipper export into
    /// _MyGame while keeping their GUIDs, so mesh/material/texture references resolve on their own.
    /// Script references to the TextMeshPro / uGUI DLLs are remapped to the package scripts;
    /// every other original script is stripped after import.
    /// </summary>
    public static class OriginalAssetImporter
    {
        private const string SourceRoot = "G:/Game_2D/APK/ExportedProject/Assets";
        private const string ManifestPath = "Tools/Restore/original_import_manifest.txt";
        private const string PrefabRoot = "Assets/_MyGame/Prefabs/Original";

        // Dummy copies of package shaders produced by AssetRipper -> real package shader GUIDs.
        private static readonly Dictionary<string, string> ShaderGuidMap = new Dictionary<string, string>
        {
            { "068e8682928eff24c84a4c5b975218d0", "933532a4fcc9baf4fa0491de14d08ed7" }, // URP/Lit
            { "2028b3c69e5f2dc4c8947a23875c587b", "650dd9526735d5b46b79224bc6e94025" }, // URP/Unlit
            { "0c3d8d8d72d850f46b38cb349ecf28e6", "0406db5a14f94604a8c57ccfbc9f3b46" }, // URP/Particles/Unlit
            { "166fe22b449de8940b76caa77c9a5882", "13c02b14c4d048fa9653293d54f6e0e1" }, // URP/2D/Sprite-Unlit-Default
            { "b1aa41e8a51c8bb438e3cbcf51f08a80", "e6e9a19c3678ded42a3bc431ebef7dbd" }, // URP/FallbackError
            { "50949d89f90cde84aa1150c1a931e851", "da07703fcc09f8d4799221050659bd55" }, // Shader Graph/FallbackError
            { "0fec7bde647e40649bc9063126ac367f", "68e6db2ebdc24f95958faec2be5558d6" }, // TextMeshPro/Distance Field
            { "7400a87e3581a8046b7d1f974f78f7c4", "fe393ace9b354375a9cb14cdbbc28be4" }, // TextMeshPro/Mobile/Distance Field
            { "8565ccd08a7383b4fbfcae50ec32bed3", "cf81c85f95fe47e1a27f6ae460cf182c" }, // TextMeshPro/Sprite
        };

        // DLL GUIDs in the export whose classes live in packages in this project.
        private static readonly Dictionary<string, string> DllGuidToAssembly = new Dictionary<string, string>
        {
            { "67dfb1fdfb2b407222eda8e23ac8b724", "Unity.TextMeshPro" },
            { "d3e719b59ab71ba3f6b398058c866280", "UnityEngine.UI" },
        };

        private static readonly Regex ScriptRef = new Regex(@"m_Script: \{fileID: (-?\d+), guid: ([0-9a-f]{32}), type: 3\}");
        private static readonly Regex AnyGuid = new Regex(@"guid: ([0-9a-f]{32})");

        [MenuItem("SnackDash/Restore/Import Original Prefabs")]
        public static void Run()
        {
            if (!File.Exists(ManifestPath))
            {
                Debug.LogError($"[Restore] Manifest not found: {ManifestPath}");
                return;
            }

            var scriptMap = BuildDllScriptMap();
            var entries = File.ReadAllLines(ManifestPath).Select(l => l.Trim().Trim('﻿').Replace('\\', '/')).Where(l => l.Length > 0).ToList();
            var copiedPrefabs = new List<string>();
            var report = new StringBuilder();
            int remappedScripts = 0, unmappedDllScripts = 0, strippedComponents = 0;

            // A destination that already exists with a different GUID is either an identical copy
            // (alias the original GUID to it) or a locally edited version (import the original beside it).
            var guidAliases = new Dictionary<string, string>(ShaderGuidMap);
            var plan = new List<(string relative, string destination)>();
            foreach (var relative in entries)
            {
                var source = Path.Combine(SourceRoot, relative);
                if (!File.Exists(source) || !File.Exists(source + ".meta"))
                {
                    report.AppendLine($"  missing source: {relative}");
                    continue;
                }

                var destination = DestinationFor(relative);
                if (File.Exists(destination) && ReadGuid(destination + ".meta") != ReadGuid(source + ".meta"))
                {
                    if (File.ReadAllBytes(destination).SequenceEqual(File.ReadAllBytes(source)))
                    {
                        guidAliases[ReadGuid(source + ".meta")] = ReadGuid(destination + ".meta");
                        report.AppendLine($"  reused identical: {destination}");
                        continue;
                    }
                    destination = Path.Combine(Path.GetDirectoryName(destination), "Original", Path.GetFileName(destination)).Replace('\\', '/');
                    report.AppendLine($"  kept local edit, original imported to: {destination}");
                }
                plan.Add((relative, destination));
            }

            foreach (var (relative, destination) in plan)
            {
                var source = Path.Combine(SourceRoot, relative);
                Directory.CreateDirectory(Path.GetDirectoryName(destination));

                if (IsYaml(relative))
                {
                    var text = File.ReadAllText(source);
                    text = AnyGuid.Replace(text, m => guidAliases.TryGetValue(m.Groups[1].Value, out var g) ? "guid: " + g : m.Value);
                    text = ScriptRef.Replace(text, m =>
                    {
                        if (!DllGuidToAssembly.ContainsKey(m.Groups[2].Value)) return m.Value;
                        var key = m.Groups[2].Value + ":" + m.Groups[1].Value;
                        if (scriptMap.TryGetValue(key, out var guid)) { remappedScripts++; return $"m_Script: {{fileID: 11500000, guid: {guid}, type: 3}}"; }
                        unmappedDllScripts++;
                        return m.Value;
                    });
                    text = StripUnknownScripts(text, ref strippedComponents);
                    File.WriteAllText(destination, text);
                }
                else
                {
                    File.Copy(source, destination, true);
                }
                File.Copy(source + ".meta", destination + ".meta", true);

                if (relative.EndsWith(".prefab")) copiedPrefabs.Add(destination.Replace('\\', '/'));
            }

            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            Debug.Log($"[Restore] Copied {entries.Count} entries ({copiedPrefabs.Count} prefabs). " +
                      $"Remapped {remappedScripts} TMP/uGUI script refs, {unmappedDllScripts} unmapped, " +
                      $"stripped {strippedComponents} original components.\n{report}");
        }

        public const string RebuiltFontFolder = "Assets/_MyGame/Art/Fonts/Rebuilt/";
        public const string RebuiltFontName = "LuckiestGuy SDF";

        // Original TMP material presets used by the popups (LoseInBoardPopup / LoseScreenFailedPopup).
        public static readonly string[] FontPresets =
        {
            "LuckiestGuy-Regular Combo - BG", "LuckiestGuy-Regular Combo - Base", "LuckiestGuy-Regular SDF Green Outline",
            "LuckiestGuy-Regular SDF Dark Red", "LuckiestGuy-Regular SDF Blue Underlay", "LuckiestGuy-Regular SDF Green Cartoon",
            "LuckiestGuy-Regular Atlas Material", "LuckiestGuy-Regular SDF Red Cartoon", "LuckiestGuy-Regular SDF Red Cartoon Play btn",
            "LuckiestGuy-Regular Brown Level Number", "LuckiestGuy-Regular HomeScreen Levels", "LuckiestGuy-Regular Progress Bar",
        };

        /// <summary>
        /// The exported "LuckiestGuy-Regular SDF" font asset comes from Unity 6's TextMeshPro and
        /// crashes this editor (TMP 3.0.7) when its text is drawn. Builds a fresh dynamic font asset from
        /// the TTF and recreates each original material preset on it, copying colours, outline,
        /// underlay values and keywords. Running it again only adds the presets that are missing, so
        /// references to the font and existing presets stay valid.
        /// </summary>
        [MenuItem("SnackDash/Restore/Rebuild TMP Font")]
        public static void RebuildFont()
        {
            var ttf = AssetDatabase.LoadAssetAtPath<Font>("Assets/_MyGame/Art/Fonts/LuckiestGuy-Regular.ttf");
            if (ttf == null) { Debug.LogError("[Restore] LuckiestGuy-Regular.ttf not found"); return; }
            Directory.CreateDirectory(RebuiltFontFolder);

            var fontPath = RebuiltFontFolder + RebuiltFontName + ".asset";
            var font = AssetDatabase.LoadAssetAtPath<TMPro.TMP_FontAsset>(fontPath);
            if (font == null)
            {
                font = TMPro.TMP_FontAsset.CreateFontAsset(ttf, 90, 9, UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA, 1024, 1024,
                    TMPro.AtlasPopulationMode.Dynamic, true);
                font.name = RebuiltFontName;
                AssetDatabase.CreateAsset(font, fontPath);
                font.atlasTextures[0].name = RebuiltFontName + " Atlas";
                AssetDatabase.AddObjectToAsset(font.atlasTextures[0], font);
                font.material.name = RebuiltFontName + " Material";
                AssetDatabase.AddObjectToAsset(font.material, font);
            }

            int made = 0;
            foreach (var preset in FontPresets)
            {
                var presetPath = RebuiltFontFolder + RebuiltFontName + " (" + preset + ").mat";
                if (AssetDatabase.LoadAssetAtPath<Material>(presetPath) != null) continue;
                var original = FindByName<Material>(preset);
                var material = new Material(original != null ? original.shader : font.material.shader);
                material.CopyPropertiesFromMaterial(font.material);
                if (original != null)
                {
                    var shader = original.shader;
                    for (int i = 0; i < shader.GetPropertyCount(); i++)
                    {
                        var id = shader.GetPropertyName(i);
                        if (id == "_MainTex" || id == "_TextureWidth" || id == "_TextureHeight" || id == "_GradientScale") continue;
                        switch (shader.GetPropertyType(i))
                        {
                            case UnityEngine.Rendering.ShaderPropertyType.Color: material.SetColor(id, original.GetColor(id)); break;
                            case UnityEngine.Rendering.ShaderPropertyType.Vector: material.SetVector(id, original.GetVector(id)); break;
                            case UnityEngine.Rendering.ShaderPropertyType.Float:
                            case UnityEngine.Rendering.ShaderPropertyType.Range: material.SetFloat(id, original.GetFloat(id)); break;
                        }
                    }
                    material.shaderKeywords = original.shaderKeywords;
                }
                material.SetTexture("_MainTex", font.atlasTextures[0]);
                material.name = RebuiltFontName + " (" + preset + ")";
                AssetDatabase.CreateAsset(material, presetPath);
                made++;
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"[Restore] Font {fontPath}: {made} new material presets in {RebuiltFontFolder}");
        }

        private static T FindByName<T>(string assetName) where T : UnityEngine.Object
        {
            foreach (var guid in AssetDatabase.FindAssets(assetName + " t:" + typeof(T).Name, new[] { "Assets/_MyGame" }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.StartsWith(RebuiltFontFolder)) continue;
                foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(path))
                    if (asset is T typed && asset.name == assetName) return typed;
            }
            return null;
        }

        private const string ReferenceScenePath = "Assets/_MyGame/Scenes/Reference/Game_Original.unity";

        /// <summary>
        /// Copies the original Game.unity as a read-only reference scene, with package shader and
        /// TextMeshPro/uGUI script references remapped so its UI shows up. References to original data
        /// assets that are not imported simply stay empty.
        /// </summary>
        [MenuItem("SnackDash/Restore/Import Original Game Scene (reference)")]
        public static void ImportReferenceScene()
        {
            var scriptMap = BuildDllScriptMap();
            var text = File.ReadAllText(Path.Combine(SourceRoot, "Scenes/Game.unity"));
            text = AnyGuid.Replace(text, m => ShaderGuidMap.TryGetValue(m.Groups[1].Value, out var g) ? "guid: " + g : m.Value);
            text = ScriptRef.Replace(text, m =>
                DllGuidToAssembly.ContainsKey(m.Groups[2].Value) && scriptMap.TryGetValue(m.Groups[2].Value + ":" + m.Groups[1].Value, out var guid)
                    ? $"m_Script: {{fileID: 11500000, guid: {guid}, type: 3}}"
                    : m.Value);
            int stripped = 0;
            text = StripUnknownScripts(text, ref stripped);
            Directory.CreateDirectory(Path.GetDirectoryName(ReferenceScenePath));
            File.WriteAllText(ReferenceScenePath, text);
            AssetDatabase.ImportAsset(ReferenceScenePath, ImportAssetOptions.ForceUpdate);
            Debug.Log($"[Restore] Reference scene written to {ReferenceScenePath}");
        }

        private static readonly Regex DocumentHeader = new Regex(@"^--- !u!(\d+) &(-?\d+)");
        private static readonly Regex ComponentLine = new Regex(@"^\s*- component: \{fileID: (-?\d+)\}");

        /// <summary>
        /// Drops MonoBehaviour documents whose script does not exist in this project, plus their
        /// entries in GameObject component lists, directly in the YAML. Letting Unity remove them
        /// instead (GameObjectUtility.RemoveMonoBehavioursWithMissingScript) crashes the editor on
        /// the original Feel feedbacks, which hold [SerializeReference] data.
        /// </summary>
        private static string StripUnknownScripts(string yaml, ref int stripped)
        {
            var lines = yaml.Replace("\r\n", "\n").Split('\n');
            var dropped = new HashSet<string>();

            // Pass 1: find MonoBehaviour documents with an unknown script.
            for (int i = 0; i < lines.Length; i++)
            {
                var header = DocumentHeader.Match(lines[i]);
                if (!header.Success || header.Groups[1].Value != "114") continue;
                for (int j = i + 1; j < lines.Length && !lines[j].StartsWith("--- "); j++)
                {
                    var script = ScriptRef.Match(lines[j]);
                    if (!script.Success) continue;
                    if (!IsKnownScript(script.Groups[2].Value)) dropped.Add(header.Groups[2].Value);
                    break;
                }
            }
            if (dropped.Count == 0) return yaml;
            stripped += dropped.Count;

            // Pass 2: copy everything except the dropped documents and component entries pointing at them.
            var result = new StringBuilder(yaml.Length);
            bool skipping = false;
            foreach (var line in lines)
            {
                var header = DocumentHeader.Match(line);
                if (header.Success) skipping = dropped.Contains(header.Groups[2].Value);
                if (skipping) continue;
                var component = ComponentLine.Match(line);
                if (component.Success && dropped.Contains(component.Groups[1].Value)) continue;
                result.Append(line).Append('\n');
            }
            return result.ToString();
        }

        private static bool IsKnownScript(string guid)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            return !string.IsNullOrEmpty(path) && AssetDatabase.LoadAssetAtPath<MonoScript>(path) != null;
        }

        private static string ReadGuid(string metaPath)
        {
            var line = File.ReadLines(metaPath).FirstOrDefault(l => l.StartsWith("guid: "));
            return line != null ? line.Substring(6).Trim() : string.Empty;
        }

        private static bool IsYaml(string path)
        {
            var ext = Path.GetExtension(path);
            return ext == ".prefab" || ext == ".mat" || ext == ".asset" || ext == ".controller" || ext == ".anim";
        }

        private static string DestinationFor(string relative)
        {
            var parts = relative.Split('/');
            switch (parts[0])
            {
                case "Resources": return PrefabRoot + "/" + string.Join("/", parts.Skip(1));
                case "GameObject": return PrefabRoot + "/GameObject/" + string.Join("/", parts.Skip(1));
                case "AnimationClip":
                case "AnimatorController": return "Assets/_MyGame/Art/Animations/" + parts.Last();
                case "Font": return "Assets/_MyGame/Art/Fonts/" + parts.Last();
                case "MonoBehaviour": return "Assets/_MyGame/Art/Fonts/" + parts.Last();
                default: return "Assets/_MyGame/Art/" + relative;
            }
        }

        /// <summary>Key "dllGuid:fileID" -> package MonoScript GUID, using Unity's DLL script fileID hash.</summary>
        private static Dictionary<string, string> BuildDllScriptMap()
        {
            var map = new Dictionary<string, string>();
            foreach (var guid in AssetDatabase.FindAssets("t:MonoScript", new[] { "Packages/com.unity.textmeshpro", "Packages/com.unity.ugui" }))
            {
                var script = AssetDatabase.LoadAssetAtPath<MonoScript>(AssetDatabase.GUIDToAssetPath(guid));
                var type = script != null ? script.GetClass() : null;
                if (type == null) continue;
                var assembly = type.Assembly.GetName().Name;
                foreach (var dll in DllGuidToAssembly.Where(d => d.Value == assembly))
                    map[dll.Key + ":" + DllScriptFileId(type.Namespace, type.Name)] = guid;
            }
            return map;
        }

        public static int DllScriptFileId(string ns, string name)
        {
            var bytes = Encoding.UTF8.GetBytes("s\0\0\0" + (ns ?? string.Empty) + name);
            return BitConverter.ToInt32(Md4.Hash(bytes), 0);
        }

        private static class Md4
        {
            public static byte[] Hash(byte[] input)
            {
                uint a = 0x67452301, b = 0xefcdab89, c = 0x98badcfe, d = 0x10325476;
                long bitLength = (long)input.Length * 8;
                int padded = ((input.Length + 8) / 64 + 1) * 64;
                var msg = new byte[padded];
                Array.Copy(input, msg, input.Length);
                msg[input.Length] = 0x80;
                BitConverter.GetBytes(bitLength).CopyTo(msg, padded - 8);

                var x = new uint[16];
                for (int block = 0; block < padded; block += 64)
                {
                    for (int i = 0; i < 16; i++) x[i] = BitConverter.ToUInt32(msg, block + i * 4);
                    uint aa = a, bb = b, cc = c, dd = d;
                    int[] s1 = { 3, 7, 11, 19 }, s2 = { 3, 5, 9, 13 }, s3 = { 3, 9, 11, 15 };
                    for (int i = 0; i < 16; i++)
                    {
                        int k = i;
                        uint f = (b & c) | (~b & d);
                        uint t = a + f + x[k];
                        a = d; d = c; c = b; b = Rotl(t, s1[i % 4]);
                    }
                    for (int i = 0; i < 16; i++)
                    {
                        int k = (i % 4) * 4 + i / 4;
                        uint g = (b & c) | (b & d) | (c & d);
                        uint t = a + g + x[k] + 0x5a827999;
                        a = d; d = c; c = b; b = Rotl(t, s2[i % 4]);
                    }
                    int[] order = { 0, 8, 4, 12, 2, 10, 6, 14, 1, 9, 5, 13, 3, 11, 7, 15 };
                    for (int i = 0; i < 16; i++)
                    {
                        uint h = b ^ c ^ d;
                        uint t = a + h + x[order[i]] + 0x6ed9eba1;
                        a = d; d = c; c = b; b = Rotl(t, s3[i % 4]);
                    }
                    a += aa; b += bb; c += cc; d += dd;
                }

                var result = new byte[16];
                BitConverter.GetBytes(a).CopyTo(result, 0);
                BitConverter.GetBytes(b).CopyTo(result, 4);
                BitConverter.GetBytes(c).CopyTo(result, 8);
                BitConverter.GetBytes(d).CopyTo(result, 12);
                return result;
            }

            private static uint Rotl(uint v, int s) => (v << s) | (v >> (32 - s));
        }
    }
}
#endif

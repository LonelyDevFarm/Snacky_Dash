using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace SnackDash.Editor.Audit
{
    /// <summary>
    /// So từng phần tử của prefab mình với prefab gốc [user 22/10: "kiểm tra tỉ mỉ", lần trước chỉ so dáng nút nên sót chữ / icon].
    /// Prefab gốc (bản xuất AssetRipper) tham chiếu script Image / TextMeshProUGUI qua dll gốc nên Unity không đọc được; bản chép tạm
    /// được đổi các tham chiếu đó sang script của dự án, rồi cả hai dựng trên cùng khung Canvas. Ghép phần tử theo đường dẫn trong cây
    /// (bỏ gốc); không trùng đường dẫn thì theo tên + tên cha. Báo mọi khác biệt nhìn thấy được: vị trí, cỡ, sprite, màu, kiểu ảnh, hệ số
    /// ppu, chữ (font, vật liệu viền, cỡ thật, màu, căn, khung chữ thật), bật / tắt, phần tử thiếu / thừa.
    /// </summary>
    public static class OriginalCompare
    {
        public const string Original114 = "G:/Game_2D/APK/ExportedProject/Assets/";
        public const string Original115 = "G:/Game_2D/APK/Export_1.15_ShadersYaml/ExportedProject/Assets/";
        private const string TempFolder = "Assets/_RefTmp";

        // dll UnityEngine.UI / Unity.TextMeshPro của bản gốc (AssetRipper): script = {fileID: băm MD4 tên lớp, guid của dll}.
        private const string UiDllGuid = "d3e719b59ab71ba3f6b398058c866280", TmpDllGuid = "67dfb1fdfb2b407222eda8e23ac8b724";

        /// <summary>Lớp giao diện cần giữ khi dựng bản gốc: hình, chữ, layout (vị trí đúng), hiệu ứng viền / bóng, mask.</summary>
        private static readonly System.Type[] KeptTypes =
        {
            typeof(Image), typeof(RawImage), typeof(Mask), typeof(RectMask2D), typeof(HorizontalLayoutGroup), typeof(VerticalLayoutGroup),
            typeof(GridLayoutGroup), typeof(ContentSizeFitter), typeof(LayoutElement), typeof(AspectRatioFitter), typeof(Shadow), typeof(Outline),
            typeof(TextMeshProUGUI),
        };

        private static (string from, string to)[] _scriptMap;

        /// <summary>Script dll gốc -> script dự án (tính một lần).</summary>
        private static (string from, string to)[] ScriptMap
        {
            get
            {
                if (_scriptMap != null) return _scriptMap;
                var list = new List<(string, string)>();
                var scripts = Resources.FindObjectsOfTypeAll<MonoScript>();
                foreach (var type in KeptTypes)
                {
                    var script = scripts.FirstOrDefault(s => s.GetClass() == type);
                    if (script == null || !AssetDatabase.TryGetGUIDAndLocalFileIdentifier(script, out string guid, out long _)) continue;
                    string dll = type == typeof(TextMeshProUGUI) ? TmpDllGuid : UiDllGuid;
                    list.Add(($"{{fileID: {DllScriptFileId(type.Namespace, type.Name)}, guid: {dll}, type: 3}}", $"{{fileID: 11500000, guid: {guid}, type: 3}}"));
                }
                return _scriptMap = list.ToArray();
            }
        }

        /// <summary>fileID của lớp trong dll theo cách Unity tính: 4 byte đầu MD4("s\0\0\0" + namespace + tên lớp).</summary>
        public static int DllScriptFileId(string ns, string name)
        {
            var bytes = new List<byte> { 115, 0, 0, 0 };
            bytes.AddRange(Encoding.UTF8.GetBytes(ns + name));
            var hash = Md4(bytes.ToArray());
            return System.BitConverter.ToInt32(hash, 0);
        }

        private static byte[] Md4(byte[] input)
        {
            uint a = 0x67452301, b = 0xefcdab89, c = 0x98badcfe, d = 0x10325476;
            long bitLength = (long)input.Length * 8;
            var msg = new List<byte>(input) { 0x80 };
            while (msg.Count % 64 != 56) msg.Add(0);
            msg.AddRange(System.BitConverter.GetBytes(bitLength));
            uint F(uint x, uint y, uint z) => (x & y) | (~x & z);
            uint G(uint x, uint y, uint z) => (x & y) | (x & z) | (y & z);
            uint H(uint x, uint y, uint z) => x ^ y ^ z;
            uint Rot(uint x, int s) => (x << s) | (x >> (32 - s));
            var m = msg.ToArray();
            for (int off = 0; off < m.Length; off += 64)
            {
                var X = new uint[16];
                for (int i = 0; i < 16; i++) X[i] = System.BitConverter.ToUInt32(m, off + i * 4);
                uint aa = a, bb = b, cc = c, dd = d;
                int[] s1 = { 3, 7, 11, 19 }, s2 = { 3, 5, 9, 13 }, s3 = { 3, 9, 11, 15 };
                for (int i = 0; i < 16; i++)
                {
                    uint t = a + F(b, c, d) + X[i]; t = Rot(t, s1[i % 4]); a = d; d = c; c = b; b = t;
                }
                int[] o2 = { 0, 4, 8, 12, 1, 5, 9, 13, 2, 6, 10, 14, 3, 7, 11, 15 };
                for (int i = 0; i < 16; i++)
                {
                    uint t = a + G(b, c, d) + X[o2[i]] + 0x5a827999; t = Rot(t, s2[i % 4]); a = d; d = c; c = b; b = t;
                }
                int[] o3 = { 0, 8, 4, 12, 2, 10, 6, 14, 1, 9, 5, 13, 3, 11, 7, 15 };
                for (int i = 0; i < 16; i++)
                {
                    uint t = a + H(b, c, d) + X[o3[i]] + 0x6ed9eba1; t = Rot(t, s3[i % 4]); a = d; d = c; c = b; b = t;
                }
                a += aa; b += bb; c += cc; d += dd;
            }
            var result = new List<byte>();
            foreach (var v in new[] { a, b, c, d }) result.AddRange(System.BitConverter.GetBytes(v));
            return result.ToArray();
        }

        private sealed class Node
        {
            public Transform T;
            public string Path, Name, Parent;
            public Rect Rect;
            public bool Active;
        }

        /// <param name="originalPath">Đường dẫn đầy đủ tới .prefab gốc (vd. Original114 + "Resources/popups/VictoryPopup.prefab").</param>
        /// <param name="originalRoot">Tên node con của prefab gốc làm gốc so (rỗng = gốc prefab).</param>
        /// <param name="ourRoot">Tên node con của prefab mình làm gốc so (rỗng = gốc prefab).</param>
        public static string Compare(string originalPath, string ourPath, string originalRoot = "", string ourRoot = "", float width = 886f, float height = 1920f)
        {
            Directory.CreateDirectory(TempFolder);
            string temp = TempFolder + "/O_" + Path.GetFileName(originalPath);
            var yaml = File.ReadAllText(originalPath);
            foreach (var (from, to) in ScriptMap) yaml = yaml.Replace(from, to);
            yaml = UiOnly(yaml);
            yaml = Remap(yaml, originalPath, out var missing);
            File.WriteAllText(temp, yaml);
            AssetDatabase.ImportAsset(temp, ImportAssetOptions.ForceSynchronousImport);
            var scene = EditorSceneManager.NewPreviewScene();
            try
            {
                var size = new Vector2(width, height);
                var original = Build(scene, AssetDatabase.LoadAssetAtPath<GameObject>(temp), size, originalRoot);
                var ours = Build(scene, AssetDatabase.LoadAssetAtPath<GameObject>(ourPath), size, ourRoot);
                return Report(original, ours, Path.GetFileName(originalPath), ourPath)
                     + (missing.Count > 0 ? "  (tài sản gốc không có trong dự án: " + string.Join(", ", missing) + ")\n" : "");
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(scene);
                AssetDatabase.DeleteAsset(temp);
            }
        }

        /// <summary>
        /// Chỉ giữ GameObject, Transform / RectTransform, CanvasRenderer và các script đã đổi sang của dự án (Image, chữ). Hệ hạt Unity 6
        /// trong prefab gốc làm Unity 2022 sập khi dựng (22/10 sập thật với VictoryPopup) nên bỏ hết phần còn lại, kể cả tham chiếu tới
        /// chúng trong danh sách component của GameObject.
        /// </summary>
        private static string UiOnly(string yaml)
        {
            var keepScripts = ScriptMap.Select(p => p.to).ToArray();
            var docs = System.Text.RegularExpressions.Regex.Split(yaml, @"(?=^--- !u!)", System.Text.RegularExpressions.RegexOptions.Multiline);
            var removed = new HashSet<string>();
            var kept = new List<string>();
            foreach (var doc in docs)
            {
                var head = System.Text.RegularExpressions.Regex.Match(doc, @"^--- !u!(\d+) &(-?\d+)");
                if (!head.Success) { kept.Add(doc); continue; }
                string cls = head.Groups[1].Value, id = head.Groups[2].Value;
                // Bỏ cả prefab lồng (1001): có thể kéo theo hệ hạt; phần tử lồng vắng thì báo THIẾU, xem tay.
                bool keep = cls == "1" || cls == "4" || cls == "224" || cls == "222" || cls == "225" // 225 CanvasGroup (độ mờ)
                         || (cls == "114" && keepScripts.Any(doc.Contains));
                if (keep) kept.Add(doc); else removed.Add(id);
            }
            var result = new StringBuilder();
            foreach (var doc in kept)
                foreach (var line in doc.Split('\n'))
                {
                    var c = System.Text.RegularExpressions.Regex.Match(line, @"^\s*- component: \{fileID: (-?\d+)\}");
                    if (c.Success && removed.Contains(c.Groups[1].Value)) continue;
                    result.Append(line).Append('\n');
                }
            return result.ToString().TrimEnd('\n') + "\n";
        }

        /// <summary>
        /// Số đo từng phần tử của nhánh <paramref name="rootName"/> trong prefab gốc (tính từ tâm nhánh): "đường dẫn | tâm x,y | cỡ w,h |
        /// xoay | bật" mỗi dòng. Dùng để đặt prefab mình đúng bằng số đo, không ước lượng.
        /// </summary>
        public static string Measure(string originalPath, string rootName)
        {
            Directory.CreateDirectory(TempFolder);
            string temp = TempFolder + "/O_" + Path.GetFileName(originalPath);
            var yaml = File.ReadAllText(originalPath);
            foreach (var (from, to) in ScriptMap) yaml = yaml.Replace(from, to);
            File.WriteAllText(temp, Remap(UiOnly(yaml), originalPath, out _));
            AssetDatabase.ImportAsset(temp, ImportAssetOptions.ForceSynchronousImport);
            var scene = EditorSceneManager.NewPreviewScene();
            try
            {
                var sb = new StringBuilder();
                foreach (var n in Build(scene, AssetDatabase.LoadAssetAtPath<GameObject>(temp), new Vector2(886f, 1920f), rootName))
                    sb.AppendLine($"{n.Path} | {n.Rect.center.x:0.##},{n.Rect.center.y:0.##} | {n.Rect.width:0.##},{n.Rect.height:0.##} | {n.T.localEulerAngles.z:0.##} | {n.T.gameObject.activeSelf} | {Describe(n.T)}");
                return sb.ToString();
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(scene);
                AssetDatabase.DeleteAsset(temp);
            }
        }

        /// <summary>
        /// Chép kiểu chữ gốc (vật liệu, giãn chữ / dòng / từ, cỡ + tự co) sang prefab mình, chỉ với cặp chắc chắn cùng một chữ: cùng tên node
        /// và cùng nội dung (bỏ thẻ, không phân biệt hoa thường). Không đổi khung / vị trí / căn lề. <paramref name="apply"/> = false: chỉ liệt kê.
        /// </summary>
        public static string ApplyTextStyles(string originalPath, string ourPath, bool apply)
        {
            Directory.CreateDirectory(TempFolder);
            string temp = TempFolder + "/O_" + Path.GetFileName(originalPath);
            var yaml = File.ReadAllText(originalPath);
            foreach (var (from, to) in ScriptMap) yaml = yaml.Replace(from, to);
            File.WriteAllText(temp, Remap(UiOnly(yaml), originalPath, out _));
            AssetDatabase.ImportAsset(temp, ImportAssetOptions.ForceSynchronousImport);
            bool isScene = temp.EndsWith(".unity");
            UnityEngine.SceneManagement.Scene preview = default;
            List<TMP_Text> originalTexts;
            if (isScene)
            {
                // OpenPreviewScene không mở được scene đã lược; mở cộng thêm rồi đóng lại (chỉ ở Editor, không Play).
                preview = EditorSceneManager.OpenScene(temp, OpenSceneMode.Additive);
                originalTexts = preview.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<TMP_Text>(true)).ToList();
            }
            else originalTexts = AssetDatabase.LoadAssetAtPath<GameObject>(temp).GetComponentsInChildren<TMP_Text>(true).ToList();
            var ours = PrefabUtility.LoadPrefabContents(ourPath);
            var sb = new StringBuilder($"=== {Path.GetFileName(originalPath)} -> {ourPath}\n");
            try
            {
                var sizeNotes = new StringBuilder();
                int changed = 0;
                foreach (var mine in ours.GetComponentsInChildren<TMP_Text>(true))
                {
                    string key = Norm(mine.text);
                    if (key.Length == 0) continue;
                    var o = originalTexts.Where(t => t.name == mine.name && Norm(t.text) == key)
                                         .OrderBy(t => t.transform.parent != null && mine.transform.parent != null && t.transform.parent.name == mine.transform.parent.name ? 0 : 1)
                                         .FirstOrDefault();
                    if (o == null) continue;
                    var d = new StringBuilder();
                    // Vật liệu gắn với atlas của font: font gốc khác thì đổi font theo (vd. số tim Home dùng LilitaOne).
                    if (o.font != null && o.font != mine.font && o.font.glyphTable.Count > 0)
                    {
                        d.Append($" font {Name(mine.font)}→{o.font.name}");
                        if (apply) mine.font = o.font;
                    }
                    var targetFont = o.font != null && o.font.glyphTable.Count > 0 ? o.font : mine.font;
                    var mat = OurMaterial(o.fontSharedMaterial, targetFont);
                    if (mat != null && mat != mine.fontSharedMaterial) { d.Append($" vật liệu {Name(mine.fontSharedMaterial)}→{mat.name}"); if (apply) mine.fontSharedMaterial = mat; }
                    if (Mathf.Abs(o.characterSpacing - mine.characterSpacing) > 0.01f) { d.Append($" giãn chữ {mine.characterSpacing}→{o.characterSpacing}"); if (apply) mine.characterSpacing = o.characterSpacing; }
                    if (Mathf.Abs(o.wordSpacing - mine.wordSpacing) > 0.01f) { d.Append($" giãn từ {mine.wordSpacing}→{o.wordSpacing}"); if (apply) mine.wordSpacing = o.wordSpacing; }
                    if (Mathf.Abs(o.lineSpacing - mine.lineSpacing) > 0.01f) { d.Append($" giãn dòng {mine.lineSpacing}→{o.lineSpacing}"); if (apply) mine.lineSpacing = o.lineSpacing; }
                    if (o.enableAutoSizing != mine.enableAutoSizing || Mathf.Abs(o.fontSizeMax - mine.fontSizeMax) > 0.01f || Mathf.Abs(o.fontSizeMin - mine.fontSizeMin) > 0.01f
                        || (!o.enableAutoSizing && Mathf.Abs(o.fontSize - mine.fontSize) > 0.01f))
                    {
                        // Cỡ chữ chỉ báo, không tự chép: nhiều cỡ đã chỉnh theo video và được duyệt.
                        if (Mathf.Abs(o.fontSizeMax - mine.fontSize) > 0.5f || !o.enableAutoSizing)
                            sizeNotes.AppendLine($"    (chỉ báo) {mine.name} '{Short(mine.text)}' cỡ {(mine.enableAutoSizing ? $"tự {mine.fontSizeMin}-{mine.fontSizeMax}" : mine.fontSize.ToString("0.#"))} — gốc {(o.enableAutoSizing ? $"tự {o.fontSizeMin}-{o.fontSizeMax}" : o.fontSize.ToString("0.#"))}");
                    }
                    if (d.Length == 0) continue;
                    changed++;
                    sb.AppendLine($"  {AnimationUtility.CalculateTransformPath(mine.transform, ours.transform)} '{Short(mine.text)}':{d}");
                }
                if (apply && changed > 0) PrefabUtility.SaveAsPrefabAsset(ours, ourPath);
                sb.AppendLine($"  ({changed} chữ {(apply ? "đã chép" : "sẽ chép")})");
                sb.Append(sizeNotes);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(ours);
                if (isScene) EditorSceneManager.CloseScene(preview, true);
                AssetDatabase.DeleteAsset(temp);
            }
            return sb.ToString();
        }

        private static string Norm(string s) => System.Text.RegularExpressions.Regex.Replace(s ?? "", "<[^>]*>|\\s+", " ").Trim().ToLowerInvariant();

        /// <summary>Vật liệu dự án ứng với vật liệu gốc: vật liệu mặc định của font gốc -> mặc định của font mình; còn lại theo tên "(tên gốc)".</summary>
        private static Material OurMaterial(Material original, TMP_FontAsset ourFont)
        {
            if (original == null) return null;
            if (original.name.EndsWith(" SDF Material") || original.name.EndsWith("Atlas Material"))
            {
                var atlas = Directory.GetFiles(TextMaterialSync.RebuiltFolder, ourFont.name + " (*Atlas Material).mat").FirstOrDefault();
                return atlas != null ? AssetDatabase.LoadAssetAtPath<Material>(atlas.Replace('\\', '/')) : ourFont.material;
            }
            return original; // Remap đã đổi sang vật liệu dự án cùng tên
        }

        private static readonly Dictionary<string, Dictionary<string, string>> OriginalNames = new Dictionary<string, Dictionary<string, string>>();
        private static List<string> _ourAssets;

        /// <summary>
        /// GUID gốc không có trong dự án (font / vật liệu chữ dựng lại, sprite đổi tên) hoặc trỏ tới font gốc rỗng: tra tên trong bản gốc rồi
        /// đổi sang tài sản cùng tên của dự án — vật liệu dựng lại tên "Font SDF (tên gốc)", font dựng lại bỏ "-Regular".
        /// </summary>
        private static string Remap(string yaml, string originalPath, out List<string> missing)
        {
            missing = new List<string>();
            string root = originalPath.Substring(0, originalPath.IndexOf("/Assets/") + "/Assets/".Length);
            if (!OriginalNames.TryGetValue(root, out var names))
            {
                // Bảng guid -> tên dựng sẵn ngoài Unity (quét hàng nghìn .meta trong Unity quá thời gian chờ của MCP):
                // grep -r -m1 --include=*.meta "^guid:" Assets > ../guid_names.tsv (xem Docs/KE_HOACH_RA_SOAT_HINH_ANH.md).
                names = new Dictionary<string, string>();
                string table = Path.Combine(Path.GetDirectoryName(root.TrimEnd('/')), "guid_names.tsv");
                foreach (var line in File.ReadLines(table))
                {
                    int tab = line.IndexOf('\t');
                    if (tab > 0) names[line.Substring(0, tab)] = line.Substring(tab + 1);
                }
                OriginalNames[root] = names;
            }
            if (_ourAssets == null) _ourAssets = AssetDatabase.FindAssets("t:Material t:Sprite t:TMP_FontAsset t:Texture2D", new[] { "Assets/_MyGame" }).Select(AssetDatabase.GUIDToAssetPath).Distinct().ToList();
            var all = _ourAssets;
            var done = new Dictionary<string, string>();
            foreach (System.Text.RegularExpressions.Match match in System.Text.RegularExpressions.Regex.Matches(yaml, @"guid: ([0-9a-f]{32})"))
            {
                string guid = match.Groups[1].Value;
                if (done.ContainsKey(guid)) continue;
                string ours = AssetDatabase.GUIDToAssetPath(guid);
                bool emptyFont = ours.Length > 0 && AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(ours) is TMP_FontAsset f && f.glyphTable.Count == 0;
                if (ours.Length > 0 && !emptyFont) { done[guid] = guid; continue; }
                if (!names.TryGetValue(guid, out var name)) { done[guid] = guid; continue; }
                string alt = name.Replace("-Regular", "");
                string found = all.FirstOrDefault(p => Path.GetFileNameWithoutExtension(p) == name && AssetDatabase.AssetPathToGUID(p) != guid)
                            ?? all.FirstOrDefault(p => Path.GetFileNameWithoutExtension(p).EndsWith("(" + name + ")"))
                            ?? all.FirstOrDefault(p => p.EndsWith(".asset") && Path.GetFileNameWithoutExtension(p) == alt && AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(p) != null);
                if (found == null) { if (!name.StartsWith("Unity") && ours.Length == 0) missing.Add(name); done[guid] = guid; continue; }
                done[guid] = AssetDatabase.AssetPathToGUID(found);
            }
            foreach (var pair in done.Where(p => p.Key != p.Value)) yaml = yaml.Replace("guid: " + pair.Key, "guid: " + pair.Value);
            return yaml;
        }

        private static List<Node> Build(UnityEngine.SceneManagement.Scene scene, GameObject prefab, Vector2 size, string rootName)
        {
            var canvasObject = new GameObject("CompareCanvas", typeof(RectTransform), typeof(Canvas));
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(canvasObject, scene);
            canvasObject.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
            ((RectTransform)canvasObject.transform).sizeDelta = size;
            var instance = Object.Instantiate(prefab, canvasObject.transform, false);
            instance.SetActive(true);
            if (instance.transform is RectTransform r && r.anchorMin == r.anchorMax && r.sizeDelta == Vector2.zero)
            {
                r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = r.offsetMax = Vector2.zero;
            }
            Transform root = instance.transform;
            if (!string.IsNullOrEmpty(rootName))
                root = instance.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == rootName) ?? root;
            for (var p = root; p != null; p = p.parent) p.gameObject.SetActive(true); // nhánh đang so có thể tắt sẵn trong prefab (bật lúc chạy)
            for (int pass = 0; pass < 3; pass++)
            {
                foreach (var rect in instance.GetComponentsInChildren<RectTransform>(true).Reverse()) LayoutRebuilder.ForceRebuildLayoutImmediate(rect);
                Canvas.ForceUpdateCanvases();
            }
            var nodes = new List<Node>();
            var corners = new Vector3[4];
            // Toạ độ tính từ tâm nhánh đang so: hai prefab đặt nhánh ở chỗ khác nhau thì vẫn so được bên trong.
            Vector3 origin = Vector3.zero;
            if (!string.IsNullOrEmpty(rootName) && root is RectTransform rr) { rr.GetWorldCorners(corners); origin = (corners[0] + corners[2]) / 2f; }
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                if (!(t is RectTransform rt)) continue;
                rt.GetWorldCorners(corners);
                for (int i = 0; i < 4; i++) corners[i] -= origin;
                // Khung thật (trước khi xoay): tâm = giữa hai góc chéo, cỡ = rect x tỉ lệ. Góc chéo của hình đã xoay không phải cỡ thật.
                var mid = (corners[0] + corners[2]) / 2f;
                var real = Vector2.Scale(rt.rect.size, new Vector2(rt.lossyScale.x, rt.lossyScale.y));
                corners[0] = new Vector3(mid.x - real.x / 2f, mid.y - real.y / 2f); corners[2] = new Vector3(mid.x + real.x / 2f, mid.y + real.y / 2f);
                var parts = new List<string>();
                for (var p = t; p != null && p != root; p = p.parent) parts.Add(p.name);
                parts.Reverse();
                nodes.Add(new Node
                {
                    T = t, Name = t.name, Parent = t.parent != null ? t.parent.name : "", Path = string.Join("/", parts),
                    Rect = Rect.MinMaxRect(corners[0].x, corners[0].y, corners[2].x, corners[2].y),
                    Active = t.gameObject.activeSelf,
                });
            }
            return nodes;
        }

        private static string Report(List<Node> original, List<Node> ours, string originalName, string ourPath)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"=== {originalName} (gốc) <-> {ourPath}");
            var used = new HashSet<Node>();
            var byPath = ours.GroupBy(n => n.Path).ToDictionary(g => g.Key, g => g.First());
            int same = 0;
            foreach (var o in original)
            {
                if (!Shows(o.T)) continue;
                if (!byPath.TryGetValue(o.Path, out var m) || used.Contains(m))
                    m = ours.Where(n => !used.Contains(n) && n.Name == o.Name)
                            .OrderBy(n => n.Parent == o.Parent ? 0 : 1).ThenBy(n => (n.Rect.center - o.Rect.center).sqrMagnitude).FirstOrDefault();
                if (m == null) { sb.AppendLine($"  THIẾU {o.Path} {Describe(o.T)}"); continue; }
                used.Add(m);
                string diff = Diff(o, m);
                if (diff.Length == 0) { same++; continue; }
                sb.AppendLine($"  KHÁC  {o.Path}{(m.Path != o.Path ? "  (mình: " + m.Path + ")" : "")}{diff}");
            }
            foreach (var m in ours.Where(n => !used.Contains(n) && Shows(n.T)))
                sb.AppendLine($"  THÊM  {m.Path} {Describe(m.T)}");
            sb.AppendLine($"  ({same} phần tử khớp hoàn toàn)");
            return sb.ToString();
        }

        /// <summary>Phần tử vẽ ra thứ gì đó (ảnh có sprite / chữ có nội dung) và đang bật trong cây.</summary>
        private static bool Shows(Transform t)
        {
            if (!t.gameObject.activeInHierarchy) return false;
            if (t.TryGetComponent<TMP_Text>(out var text)) return text.enabled && !string.IsNullOrWhiteSpace(text.text) && text.color.a > 0.01f;
            if (t.TryGetComponent<Image>(out var image))
            {
                if (!image.enabled || image.color.a <= 0.01f) return false;
                if (t.TryGetComponent<Mask>(out var mask) && !mask.showMaskGraphic) return false;
                return image.sprite != null;
            }
            return false;
        }

        private static string Describe(Transform t)
        {
            if (t.TryGetComponent<TMP_Text>(out var text)) return $"chữ '{text.text}' {Name(text.fontSharedMaterial)}";
            if (t.TryGetComponent<Image>(out var image)) return $"ảnh {Name(image.sprite)} màu {Hex(image.color)}";
            return "";
        }

        private static string Diff(Node o, Node m)
        {
            var d = new StringBuilder();
            var dc = m.Rect.center - o.Rect.center;
            if (dc.magnitude > 1.5f) d.Append($"\n      lệch ({dc.x:+0.#;-0.#;0}, {dc.y:+0.#;-0.#;0})");
            if (Mathf.Abs(m.Rect.width - o.Rect.width) > 1.5f || Mathf.Abs(m.Rect.height - o.Rect.height) > 1.5f)
                d.Append($"\n      cỡ {o.Rect.width:0.#}x{o.Rect.height:0.#} -> {m.Rect.width:0.#}x{m.Rect.height:0.#}");
            if (Mathf.Abs(Mathf.DeltaAngle(o.T.eulerAngles.z, m.T.eulerAngles.z)) > 0.5f) d.Append($"\n      xoay {o.T.eulerAngles.z:0.#} -> {m.T.eulerAngles.z:0.#}");
            if (o.T.TryGetComponent<Image>(out var ia) && m.T.TryGetComponent<Image>(out var ib))
            {
                if (Name(ia.sprite) != Name(ib.sprite)) d.Append($"\n      sprite {Name(ia.sprite)} -> {Name(ib.sprite)}");
                if (!Same(ia.color, ib.color)) d.Append($"\n      màu {Hex(ia.color)} -> {Hex(ib.color)}");
                if (ia.type != ib.type) d.Append($"\n      kiểu {ia.type} -> {ib.type}");
                if (ia.type != Image.Type.Simple && Mathf.Abs(ia.pixelsPerUnitMultiplier - ib.pixelsPerUnitMultiplier) > 0.01f)
                    d.Append($"\n      ppu x{ia.pixelsPerUnitMultiplier} -> x{ib.pixelsPerUnitMultiplier}");
                if (ia.preserveAspect != ib.preserveAspect) d.Append($"\n      giữ tỉ lệ {ia.preserveAspect} -> {ib.preserveAspect}");
                if (Name(ia.material) != Name(ib.material) && !(ia.material == ia.defaultMaterial && ib.material == ib.defaultMaterial))
                    d.Append($"\n      vật liệu {Name(ia.material)} -> {Name(ib.material)}");
            }
            if (o.T.TryGetComponent<TMP_Text>(out var ta) && m.T.TryGetComponent<TMP_Text>(out var tb))
            {
                if (ta.font == null || ta.fontSharedMaterial == null) { d.Append($"\n      (chữ gốc thiếu font / vật liệu, chỉ so số liệu)"); }
                else { try { ta.ForceMeshUpdate(); } catch { } }
                try { tb.ForceMeshUpdate(); } catch { }
                if (ta.text != tb.text) d.Append($"\n      chữ '{Short(ta.text)}' -> '{Short(tb.text)}'");
                if (Name(ta.font) != Name(tb.font)) d.Append($"\n      font {Name(ta.font)} -> {Name(tb.font)}");
                if (Name(ta.fontSharedMaterial) != Name(tb.fontSharedMaterial)) d.Append($"\n      vật liệu chữ {Name(ta.fontSharedMaterial)} -> {Name(tb.fontSharedMaterial)}");
                if (Mathf.Abs(ta.fontSize - tb.fontSize) > 0.5f) d.Append($"\n      cỡ chữ {ta.fontSize:0.#} -> {tb.fontSize:0.#}");
                if (!Same(ta.color, tb.color)) d.Append($"\n      màu chữ {Hex(ta.color)} -> {Hex(tb.color)}");
                if (ta.alignment != tb.alignment) d.Append($"\n      căn {ta.alignment} -> {tb.alignment}");
                if (Mathf.Abs(ta.characterSpacing - tb.characterSpacing) > 0.1f) d.Append($"\n      giãn chữ {ta.characterSpacing} -> {tb.characterSpacing}");
                if (ta.fontStyle != tb.fontStyle) d.Append($"\n      kiểu chữ {ta.fontStyle} -> {tb.fontStyle}");
                if (ta.enableVertexGradient != tb.enableVertexGradient) d.Append($"\n      gradient {ta.enableVertexGradient} -> {tb.enableVertexGradient}");
                var ba = TextRect(ta); var bb = TextRect(tb);
                var dt = bb.center - ba.center;
                if (dt.magnitude > 2f) d.Append($"\n      chữ thật lệch ({dt.x:+0.#;-0.#;0}, {dt.y:+0.#;-0.#;0})");
                if (Mathf.Abs(ba.width - bb.width) > 3f || Mathf.Abs(ba.height - bb.height) > 3f) d.Append($"\n      chữ thật {ba.width:0}x{ba.height:0} -> {bb.width:0}x{bb.height:0}");
                MaterialDiff(ta.fontSharedMaterial, tb.fontSharedMaterial, d);
            }
            return d.ToString();
        }

        /// <summary>Khung chữ thật (theo mesh) trên Canvas.</summary>
        private static Rect TextRect(TMP_Text t)
        {
            // Tương đối với khung của chính nó (khung đã so riêng): chỉ đo chữ nằm ở đâu trong khung.
            var b = t.textBounds;
            return Rect.MinMaxRect(b.min.x, b.min.y, b.max.x, b.max.y);
        }

        /// <summary>Viền / bóng của vật liệu chữ khác tên: so thẳng giá trị (vật liệu mình dựng lại có thể khác tên mà cùng dáng).</summary>
        private static void MaterialDiff(Material a, Material b, StringBuilder d)
        {
            if (a == null || b == null || a == b) return;
            foreach (var p in new[] { "_OutlineWidth", "_OutlineColor", "_UnderlayColor", "_UnderlayOffsetX", "_UnderlayOffsetY", "_UnderlaySoftness", "_FaceDilate", "_FaceColor" })
            {
                if (!a.HasProperty(p) || !b.HasProperty(p)) continue;
                bool isColor = p.EndsWith("Color");
                string va = isColor ? Hex(a.GetColor(p)) : a.GetFloat(p).ToString("0.###");
                string vb = isColor ? Hex(b.GetColor(p)) : b.GetFloat(p).ToString("0.###");
                if (va != vb) d.Append($"\n      {p} {va} -> {vb}");
            }
            bool ua = a.IsKeywordEnabled("UNDERLAY_ON") || a.IsKeywordEnabled("UNDERLAY_INNER"), ub = b.IsKeywordEnabled("UNDERLAY_ON") || b.IsKeywordEnabled("UNDERLAY_INNER");
            if (ua != ub) d.Append($"\n      bóng chữ (underlay) {ua} -> {ub}");
            bool oa = a.IsKeywordEnabled("OUTLINE_ON"), ob = b.IsKeywordEnabled("OUTLINE_ON");
            if (oa != ob) d.Append($"\n      OUTLINE_ON {oa} -> {ob}");
        }

        private static bool Same(Color a, Color b) => Mathf.Abs(a.r - b.r) < 0.02f && Mathf.Abs(a.g - b.g) < 0.02f && Mathf.Abs(a.b - b.b) < 0.02f && Mathf.Abs(a.a - b.a) < 0.02f;
        private static string Hex(Color c) => "#" + ColorUtility.ToHtmlStringRGBA(c);
        private static string Name(Object o) => o != null ? o.name : "null";
        private static string Short(string s) => s.Length > 20 ? s.Substring(0, 20) + "…" : s;
    }
}

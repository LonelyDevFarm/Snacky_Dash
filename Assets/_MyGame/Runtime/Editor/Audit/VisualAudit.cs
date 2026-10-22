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
    /// Rà phần nhìn trước khi kết thúc dự án [user 22/10] (chỉ dùng trong Editor, gọi từ execute_code). Không so với bản gốc, chỉ tìm
    /// lỗi thật, mỗi lỗi kèm số đo:
    /// 1. <see cref="AuditPrefab"/>: dựng prefab trên khung Canvas cỡ máy thật rồi tìm: lệch tâm vài px, hàng không đều / lệch một bên,
    ///    ảnh 9-slice đặt nhỏ hơn tổng viền (góc bị bóp), ảnh méo, chữ tràn khung, phần tử bị mask cắt.
    /// 2. <see cref="AuditTextures"/>: cài đặt nhập ảnh gây viền / mờ (wrap Repeat, mipmap, alpha, bị thu nhỏ khi nhập) và viền trắng / đen
    ///    vẽ dính sẵn ở mép ảnh (như lần đống xu có viền trắng).
    /// 3. <see cref="ScanScreen"/>: quét màn đang chạy: chữ tràn, phần tử ra ngoài màn / vùng an toàn, vật liệu viền chữ đang dùng.
    /// </summary>
    public static class VisualAudit
    {
        /// <summary>Khung Canvas máy 19.5:9 (CanvasScaler 1080x1920 khớp chiều cao).</summary>
        public static readonly Vector2 Phone = new Vector2(886f, 1920f);
        /// <summary>Khung Canvas máy tính bảng 4:3.</summary>
        public static readonly Vector2 Tablet = new Vector2(1440f, 1920f);

        private const float CenterSlack = 1f, CenterLimit = 12f;

        private sealed class Node
        {
            public RectTransform T;
            public Rect Rect; // toạ độ khung Canvas, gốc ở góc dưới trái
            public bool Visible;
            public string Path;
        }

        // ------------------------------------------------------------------ 1. prefab

        /// <summary>Rà một prefab UI trên khung <paramref name="canvasSize"/> (đơn vị Canvas).</summary>
        public static string AuditPrefab(string prefabPath, Vector2 canvasSize)
        {
            var scene = EditorSceneManager.NewPreviewScene();
            try
            {
                var nodes = Build(scene, AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath), canvasSize);
                var byT = nodes.ToDictionary(n => n.T);
                var sb = new StringBuilder();
                foreach (var n in nodes.Where(n => n.Visible))
                {
                    CheckCenter(n, byT, sb);
                    CheckImage(n, sb);
                    CheckText(n, sb);
                    CheckMask(n, byT, sb);
                }
                foreach (var parent in nodes) CheckRow(parent, nodes, sb);
                return $"== {prefabPath} ({canvasSize.x}x{canvasSize.y}) ==\n" + (sb.Length > 0 ? sb.ToString() : "  không thấy lỗi\n");
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        private static List<Node> Build(UnityEngine.SceneManagement.Scene scene, GameObject prefab, Vector2 size)
        {
            var canvasObject = new GameObject("AuditCanvas", typeof(RectTransform), typeof(Canvas));
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(canvasObject, scene);
            canvasObject.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
            var canvasRect = (RectTransform)canvasObject.transform;
            canvasRect.sizeDelta = size;
            canvasRect.position = Vector3.zero;
            var instance = Object.Instantiate(prefab, canvasObject.transform, false);
            instance.SetActive(true);
            if (instance.transform is RectTransform root && root.anchorMin == root.anchorMax && root.sizeDelta == Vector2.zero)
            {
                root.anchorMin = Vector2.zero; root.anchorMax = Vector2.one; root.offsetMin = root.offsetMax = Vector2.zero;
            }
            for (int pass = 0; pass < 3; pass++)
            {
                foreach (var rect in instance.GetComponentsInChildren<RectTransform>(true).Reverse()) LayoutRebuilder.ForceRebuildLayoutImmediate(rect);
                Canvas.ForceUpdateCanvases();
            }
            var nodes = new List<Node>();
            var corners = new Vector3[4];
            foreach (var rt in instance.GetComponentsInChildren<RectTransform>(true))
            {
                rt.GetWorldCorners(corners);
                var graphic = rt.GetComponent<Graphic>();
                nodes.Add(new Node
                {
                    T = rt,
                    Rect = Rect.MinMaxRect(corners[0].x + size.x / 2, corners[0].y + size.y / 2, corners[2].x + size.x / 2, corners[2].y + size.y / 2),
                    Visible = rt.gameObject.activeInHierarchy && graphic != null && graphic.enabled && graphic.color.a > 0.01f && HasContent(graphic),
                    Path = RelPath(rt, instance.transform),
                });
            }
            return nodes;
        }

        private static bool HasContent(Graphic g)
        {
            if (g is TMP_Text text) return !string.IsNullOrEmpty(text.text);
            if (g is Image image)
            {
                var mask = image.GetComponent<Mask>();
                return image.sprite != null && (mask == null || mask.showMaskGraphic);
            }
            return true;
        }

        /// <summary>Lệch tâm ngang so với cha 1-12 px: thường là kéo tay lỡ, không phải ý đồ.</summary>
        private static void CheckCenter(Node n, Dictionary<RectTransform, Node> byT, StringBuilder sb)
        {
            if (!(n.T.parent is RectTransform parentT) || !byT.TryGetValue(parentT, out var parent)) return;
            if (n.T.localEulerAngles.z > 0.1f && n.T.localEulerAngles.z < 359.9f) return;
            float dx = n.Rect.center.x - parent.Rect.center.x;
            if (Mathf.Abs(dx) > CenterSlack && Mathf.Abs(dx) <= CenterLimit && n.Rect.width < parent.Rect.width * 1.05f)
                sb.AppendLine($"  LỆCH-TÂM   {n.Path}: tâm lệch {dx:+0.0;-0.0} px so với cha ({parent.Rect.width:0} rộng)");
        }

        private static void CheckImage(Node n, StringBuilder sb)
        {
            var image = n.T.GetComponent<Image>();
            if (image == null || image.sprite == null) return;
            var sprite = image.sprite;
            if ((image.type == Image.Type.Sliced || image.type == Image.Type.Tiled) && sprite.border != Vector4.zero)
            {
                // Cỡ viền trên Canvas = px viền / (ppu sprite * hệ số / ppu tham chiếu 100). Khung nhỏ hơn tổng viền thì Unity bóp
                // viền theo từng chiều riêng: hai chiều bóp khác nhau = góc bo thành elip (méo viền).
                float scale = 100f / (sprite.pixelsPerUnit * image.pixelsPerUnitMultiplier);
                float needW = (sprite.border.x + sprite.border.z) * scale, needH = (sprite.border.y + sprite.border.w) * scale;
                var r = n.T.rect;
                float fx = needW > 0 ? Mathf.Min(1f, r.width / needW) : 1f, fy = needH > 0 ? Mathf.Min(1f, r.height / needH) : 1f;
                float skew = Mathf.Max(fx, fy) / Mathf.Max(0.0001f, Mathf.Min(fx, fy));
                if (skew > 1.1f)
                    sb.AppendLine($"  VIỀN-MÉO   {n.Path}: góc 9-slice bị bóp {fx:P0} ngang / {fy:P0} dọc (méo x{skew:0.00}) — khung {r.width:0.#}x{r.height:0.#}, viền cần {needW:0.#}x{needH:0.#} ({sprite.name}, ppu x{image.pixelsPerUnitMultiplier})");
            }
            else if (image.type == Image.Type.Simple && !image.preserveAspect && sprite.border == Vector4.zero)
            {
                var r = n.T.rect;
                if (r.width < 4 || r.height < 4) return;
                float ratio = (sprite.rect.width / sprite.rect.height) / (r.width / r.height);
                float off = Mathf.Abs(ratio - 1f);
                if (off > 0.05f)
                    sb.AppendLine($"  MÉO{(LooksLikeGlow(n.T.name, sprite.name) ? "(sáng/nền?)" : "")} {n.Path}: {off:P0} — ảnh {sprite.rect.width:0}x{sprite.rect.height:0}, khung {r.width:0.#}x{r.height:0.#} ({sprite.name})");
            }
        }

        private static bool LooksLikeGlow(string node, string sprite)
        {
            string s = (node + " " + sprite).ToLowerInvariant();
            return new[] { "glow", "gradi", "shadow", "light", "ray", "fx", "flare", "shine", "dim", "overlay", "edge", "bg", "background", "mask", "pattern" }.Any(s.Contains);
        }

        private static void CheckText(Node n, StringBuilder sb)
        {
            var text = n.T.GetComponent<TMP_Text>();
            if (text == null) return;
            text.ForceMeshUpdate();
            var b = text.textBounds; var r = n.T.rect;
            if (text.isTextOverflowing || b.size.x > r.width + 2 || b.size.y > r.height + 2)
                sb.AppendLine($"  TRÀN-CHỮ   {n.Path}: '{Short(text.text)}' chữ {b.size.x:0}x{b.size.y:0}, khung {r.width:0}x{r.height:0}");
            if (text.enableAutoSizing && text.fontSize <= text.fontSizeMin + 0.01f)
                sb.AppendLine($"  CO-CHỮ     {n.Path}: '{Short(text.text)}' co tới cỡ nhỏ nhất {text.fontSize}");
        }

        /// <summary>Phần tử thò ra ngoài vùng mask của cha (bị cắt một phần).</summary>
        private static void CheckMask(Node n, Dictionary<RectTransform, Node> byT, StringBuilder sb)
        {
            for (var p = n.T.parent; p != null; p = p.parent)
            {
                bool masks = (p.TryGetComponent<Mask>(out var m) && m.enabled) || (p.TryGetComponent<RectMask2D>(out var m2) && m2.enabled);
                if (!masks) continue;
                if (!byT.TryGetValue((RectTransform)p, out var mask)) return;
                var r = n.Rect; var k = mask.Rect;
                float outside = Mathf.Max(k.xMin - r.xMin, r.xMax - k.xMax, k.yMin - r.yMin, r.yMax - k.yMax);
                bool fullyOut = r.xMax <= k.xMin || r.xMin >= k.xMax || r.yMax <= k.yMin || r.yMin >= k.yMax;
                if (outside > 4f && !fullyOut && !LooksLikeGlow(n.T.name, ""))
                    sb.AppendLine($"  BỊ-CẮT     {n.Path}: thò {outside:0} px ra ngoài mask {mask.Path}");
                return;
            }
        }

        /// <summary>
        /// Con trực tiếp cùng cỡ, cùng hàng (≥ 3, hoặc 2 khi cha có layout group): khoảng cách phải đều, cả cụm cân giữa cha nếu lề hai bên gần bằng nhau.
        /// </summary>
        private static void CheckRow(Node parent, List<Node> nodes, StringBuilder sb)
        {
            var kids = nodes.Where(k => k.T.parent == parent.T && k.T.gameObject.activeInHierarchy && k.Rect.width > 4).ToList();
            foreach (var axis in new[] { 0, 1 })
            {
                var groups = kids.GroupBy(k => Mathf.Round((axis == 0 ? k.Rect.center.y : k.Rect.center.x) / 3f)).Where(g => g.Count() >= 3);
                foreach (var g in groups)
                {
                    var row = g.OrderBy(k => axis == 0 ? k.Rect.center.x : k.Rect.center.y).ToList();
                    float w0 = axis == 0 ? row[0].Rect.width : row[0].Rect.height;
                    if (row.Any(k => Mathf.Abs((axis == 0 ? k.Rect.width : k.Rect.height) - w0) > w0 * 0.15f)) continue;
                    var steps = new List<float>();
                    for (int i = 1; i < row.Count; i++) steps.Add(axis == 0 ? row[i].Rect.center.x - row[i - 1].Rect.center.x : row[i].Rect.center.y - row[i - 1].Rect.center.y);
                    if (steps.Min() < w0 * 0.5f) continue; // các lớp chồng nhau (bóng / viền / màu), không phải một hàng
                    if (steps.Max() - steps.Min() > 3f)
                        sb.AppendLine($"  HÀNG-LỆCH  {parent.Path}: {(axis == 0 ? "hàng ngang" : "cột")} {string.Join(", ", row.Select(k => k.T.name))} bước {string.Join(" / ", steps.Select(s => s.ToString("0.#")))}");
                    float lead = axis == 0 ? row[0].Rect.xMin - parent.Rect.xMin : row[0].Rect.yMin - parent.Rect.yMin;
                    float trail = axis == 0 ? parent.Rect.xMax - row[row.Count - 1].Rect.xMax : parent.Rect.yMax - row[row.Count - 1].Rect.yMax;
                    if (axis == 0 && Mathf.Abs(lead - trail) > 3f && Mathf.Abs(lead - trail) < 40f)
                        sb.AppendLine($"  CỤM-LỆCH   {parent.Path}: lề trái {lead:0.#} / phải {trail:0.#} ({row.Count} phần tử)");
                }
            }
        }

        // ------------------------------------------------------------------ 2. ảnh

        /// <summary>
        /// Ảnh dùng bởi các prefab trong <paramref name="folders"/>: cài đặt nhập gây viền / mờ, và viền trắng / đen vẽ dính ở mép.
        /// </summary>
        public static string AuditTextures(params string[] folders)
        {
            var prefabs = AssetDatabase.FindAssets("t:Prefab", folders).Select(AssetDatabase.GUIDToAssetPath).ToArray();
            var textures = AssetDatabase.GetDependencies(prefabs, true)
                .Where(p => AssetImporter.GetAtPath(p) is TextureImporter ti && ti.textureType == TextureImporterType.Sprite).Distinct().OrderBy(p => p).ToList();
            var sb = new StringBuilder();
            sb.AppendLine($"== {textures.Count} ảnh sprite dùng bởi {prefabs.Length} prefab ==");
            foreach (var path in textures)
            {
                var ti = (TextureImporter)AssetImporter.GetAtPath(path);
                var issues = new List<string>();
                var source = new Texture2D(2, 2);
                bool loaded = (path.EndsWith(".png") || path.EndsWith(".jpg")) && source.LoadImage(File.ReadAllBytes(path));
                if (ti.wrapMode != TextureWrapMode.Clamp && (ti.spriteImportMode == SpriteImportMode.Single)) issues.Add($"wrap {ti.wrapMode} (mép có thể lem vạch)");
                if (ti.mipmapEnabled) issues.Add("bật mipmap (UI / sprite dễ mờ, viền lem)");
                if (ti.filterMode == FilterMode.Point) issues.Add("lọc Point (răng cưa)");
                if (loaded && ti.DoesSourceTextureHaveAlpha() && !ti.alphaIsTransparency) issues.Add("chưa bật Alpha Is Transparency (viền đen/trắng khi lọc)");
                if (loaded)
                {
                    int native = Mathf.Max(source.width, source.height);
                    var android = ti.GetPlatformTextureSettings("Android");
                    int max = android.overridden ? android.maxTextureSize : ti.maxTextureSize;
                    if (max < native) issues.Add($"bị thu nhỏ khi nhập {native} -> {max} (mờ)");
                    if (ti.crunchedCompression || (android.overridden && android.crunchedCompression)) issues.Add("nén crunch (vệt bẩn quanh viền)");
                    string halo = Halo(source);
                    if (halo != null) issues.Add(halo);
                }
                Object.DestroyImmediate(source);
                if (issues.Count > 0) sb.AppendLine($"  {path}: {string.Join("; ", issues)}");
            }
            return sb.ToString();
        }

        /// <summary>
        /// Viền vẽ dính: điểm mép (alpha 0.15-0.85, sát điểm trong suốt) sáng / tối hơn hẳn phần đặc kế bên. Trả null khi không thấy.
        /// </summary>
        private static string Halo(Texture2D tex)
        {
            var px = tex.GetPixels32();
            int w = tex.width, h = tex.height, step = Mathf.Max(1, Mathf.Max(w, h) / 512);
            int edges = 0, bright = 0, dark = 0;
            for (int y = 1; y < h - 1; y += step)
                for (int x = 1; x < w - 1; x += step)
                {
                    var c = px[y * w + x];
                    if (c.a < 38 || c.a > 217) continue;
                    // cần một điểm gần như trong suốt và một điểm đặc ở hai phía
                    Color32 solid = default; bool hasClear = false, hasSolid = false;
                    for (int k = 0; k < 4; k++)
                    {
                        int nx = x + (k == 0 ? 2 : k == 1 ? -2 : 0), ny = y + (k == 2 ? 2 : k == 3 ? -2 : 0);
                        if (nx < 0 || ny < 0 || nx >= w || ny >= h) continue;
                        var n = px[ny * w + nx];
                        if (n.a < 10) hasClear = true;
                        if (n.a > 245) { hasSolid = true; solid = n; }
                    }
                    if (!hasClear || !hasSolid) continue;
                    edges++;
                    float le = (c.r + c.g + c.b) / 765f, ls = (solid.r + solid.g + solid.b) / 765f;
                    if (le > 0.92f && ls < 0.75f) bright++;
                    else if (le < 0.06f && ls > 0.25f) dark++;
                }
            if (edges < 40) return null;
            float fb = bright / (float)edges, fd = dark / (float)edges;
            if (fb > 0.35f) return $"viền TRẮNG dính ở mép ({fb:P0} điểm mép)";
            if (fd > 0.35f) return $"viền ĐEN dính ở mép ({fd:P0} điểm mép)";
            return null;
        }

        // ------------------------------------------------------------------ 3. màn đang chạy

        /// <summary>Quét màn đang chạy (Play): chữ tràn, phần tử ra ngoài màn / vùng an toàn, vật liệu viền chữ đang hiện.</summary>
        public static string ScanScreen()
        {
            var sb = new StringBuilder();
            var materials = new Dictionary<string, int>();
            var safe = Screen.safeArea;
            foreach (var t in Object.FindObjectsOfType<TMP_Text>())
            {
                if (!t.isActiveAndEnabled || string.IsNullOrEmpty(t.text) || !Shown(t)) continue;
                t.ForceMeshUpdate();
                string key = Name(t.font) + " | " + Name(t.fontSharedMaterial);
                materials[key] = materials.TryGetValue(key, out int n) ? n + 1 : 1;
                var b = t.textBounds; var r = t.rectTransform.rect;
                if (t.isTextOverflowing || b.size.x > r.width + 2 || b.size.y > r.height + 2)
                    sb.AppendLine($"TRÀN-CHỮ {Path4(t.transform)} '{Short(t.text)}' chữ {b.size.x:0}x{b.size.y:0} khung {r.width:0}x{r.height:0}");
                if (t.enableAutoSizing && t.fontSize <= t.fontSizeMin + 0.01f) sb.AppendLine($"CO-CHỮ {Path4(t.transform)} '{Short(t.text)}' cỡ {t.fontSize}");
            }
            var corners = new Vector3[4];
            foreach (var g in Object.FindObjectsOfType<Graphic>())
            {
                if (!g.isActiveAndEnabled || g.canvas == null || g.canvas.renderMode != RenderMode.ScreenSpaceOverlay || !Shown(g)) continue;
                g.rectTransform.GetWorldCorners(corners);
                var r = Rect.MinMaxRect(corners[0].x, corners[0].y, corners[2].x, corners[2].y);
                if (r.width >= Screen.width * 0.9f || r.height >= Screen.height * 0.9f) continue;
                // Nằm hẳn ngoài màn (tab / popup đang ẩn bằng cách đẩy ra) hay trong vùng cuộn có mask: không phải lỗi.
                if (r.xMax <= 0 || r.xMin >= Screen.width || r.yMax <= 0 || r.yMin >= Screen.height || Masked(g.transform)) continue;
                if (r.xMin < -2 || r.xMax > Screen.width + 2 || r.yMin < -2 || r.yMax > Screen.height + 2)
                    sb.AppendLine($"RA-NGOÀI-MÀN {Path4(g.transform)} [{r.xMin:0},{r.yMin:0} {r.width:0}x{r.height:0}]");
                else if (g.raycastTarget && (r.yMax > safe.yMax + 2 || r.yMin < safe.yMin - 2 || r.xMin < safe.xMin - 2 || r.xMax > safe.xMax + 2))
                    sb.AppendLine($"NGOÀI-VÙNG-AN-TOÀN {Path4(g.transform)} [{r.xMin:0},{r.yMin:0} {r.width:0}x{r.height:0}]");
            }
            sb.AppendLine($"--- màn {Screen.width}x{Screen.height}, vùng an toàn {safe}; font | vật liệu đang hiện:");
            foreach (var kv in materials.OrderBy(k => k.Key)) sb.AppendLine($"  {kv.Value}x {kv.Key}");
            return sb.ToString();
        }

        private static bool Masked(Transform t)
        {
            for (var p = t.parent; p != null; p = p.parent)
                if ((p.TryGetComponent<Mask>(out var m) && m.enabled) || (p.TryGetComponent<RectMask2D>(out var m2) && m2.enabled)) return true;
            return false;
        }

        private static bool Shown(Graphic g)
        {
            if (g.color.a < 0.05f || g.canvasRenderer.GetAlpha() < 0.05f) return false;
            for (var p = g.transform; p != null; p = p.parent)
                if (p.TryGetComponent<CanvasGroup>(out var group) && group.alpha < 0.05f) return false;
            return true;
        }

        private static string RelPath(Transform t, Transform root)
        {
            var parts = new List<string>();
            for (var p = t; p != null && p != root; p = p.parent) parts.Add(p.name);
            parts.Reverse();
            return parts.Count == 0 ? root.name : string.Join("/", parts);
        }

        private static string Path4(Transform t)
        {
            var parts = new List<string>();
            for (var p = t; p != null && parts.Count < 4; p = p.parent) parts.Add(p.name);
            parts.Reverse();
            return string.Join("/", parts);
        }

        private static string Short(string s) => s.Length > 24 ? s.Substring(0, 24) + "…" : s.Replace("\n", " ");
        private static string Name(Object o) => o != null ? o.name : "null";
    }
}

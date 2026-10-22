using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace SnackDash.Editor.Audit
{
    /// <summary>
    /// Vật liệu chữ (TMP) theo đúng giá trị bản gốc [user 22/10: chữ CONTINUE / X2 khác gốc]. Vật liệu dựng lại trong
    /// Source/Art/Fonts/Rebuilt tên "Font SDF (tên gốc)" trước đây chép tay viền / bóng nhưng đặt _GradientScale = 10 (theo atlas
    /// dựng lại) trong khi vật liệu gốc giữ 7 (cùng atlas cỡ 90, đệm 9) nên viền / bóng hiện dày hơn gốc ~43 %. Công cụ đọc .mat gốc
    /// (bản xuất AssetRipper) và chép mọi số / màu / từ khoá shader, trừ ảnh atlas (giữ của font dựng lại).
    /// </summary>
    public static class TextMaterialSync
    {
        public const string RebuiltFolder = "Assets/_MyGame/Source/Art/Fonts/Rebuilt/";
        private static readonly string[] KeepOurs = { "_TextureWidth", "_TextureHeight" };

        /// <summary>So từng vật liệu dựng lại với .mat gốc cùng tên; <paramref name="apply"/> thì chép giá trị gốc vào.</summary>
        public static string SyncAll(string originalMaterialFolder, bool apply)
        {
            var sb = new StringBuilder();
            foreach (var path in Directory.GetFiles(RebuiltFolder, "*.mat"))
            {
                string file = Path.GetFileNameWithoutExtension(path);
                var m = Regex.Match(file, @"^[^(]*\((.*)\)$");
                if (!m.Success) continue;
                string original = Path.Combine(originalMaterialFolder, m.Groups[1].Value + ".mat");
                if (!File.Exists(original)) { sb.AppendLine($"{file}: không có .mat gốc"); continue; }
                var mat = AssetDatabase.LoadAssetAtPath<Material>(path.Replace('\\', '/'));
                string diff = Apply(mat, original, apply);
                sb.AppendLine($"{m.Groups[1].Value}: {(diff.Length == 0 ? "khớp" : diff)}");
            }
            if (apply) AssetDatabase.SaveAssets();
            return sb.ToString();
        }

        /// <summary>Tạo vật liệu "font (tên gốc)" từ .mat gốc khi dự án chưa có; trả đường dẫn.</summary>
        public static string Create(TMP_FontAsset font, string originalMatPath)
        {
            string name = Path.GetFileNameWithoutExtension(originalMatPath);
            string path = RebuiltFolder + font.name + " (" + name + ").mat";
            // Gốc dùng shader TextMeshPro/Distance Field (không phải Mobile như vật liệu mặc định của font: 22/10 dựng theo font.material
            // thì chữ thành khối màu). Khởi từ một vật liệu dựng lại đang hiển thị đúng của cùng font, rồi chép giá trị gốc.
            var template = Directory.GetFiles(RebuiltFolder, font.name + " (*.mat")
                .Select(p => AssetDatabase.LoadAssetAtPath<Material>(p.Replace('\\', '/')))
                .FirstOrDefault(m => m != null && m.shader != null && m.shader.name == "TextMeshPro/Distance Field" && m.mainTexture == font.atlasTexture && !m.name.EndsWith("(" + name + ")"));
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(template != null ? template : font.material) { name = font.name + " (" + name + ")" };
                AssetDatabase.CreateAsset(mat, path);
            }
            else if (template != null && mat.shader != template.shader)
            {
                mat.shader = template.shader;
                mat.CopyPropertiesFromMaterial(template);
                mat.shaderKeywords = template.shaderKeywords;
            }
            Apply(mat, originalMatPath, true);
            AssetDatabase.SaveAssets();
            return path;
        }

        private static string Apply(Material mat, string originalPath, bool apply)
        {
            var text = File.ReadAllText(originalPath);
            var d = new StringBuilder();
            string section = "";
            foreach (var raw in text.Split('\n'))
            {
                var line = raw.TrimEnd('\r');
                var head = Regex.Match(line, @"^\s{4}(m_TexEnvs|m_Ints|m_Floats|m_Colors):");
                if (head.Success) { section = head.Groups[1].Value; continue; }
                if (section != "m_Floats" && section != "m_Colors") continue;
                var kv = Regex.Match(line, @"^\s+(?:- )?(_\w+): (.+)$");
                if (!kv.Success) continue;
                string prop = kv.Groups[1].Value, value = kv.Groups[2].Value.Trim();
                if (KeepOurs.Contains(prop) || !mat.HasProperty(prop)) continue;
                if (section == "m_Floats" && float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float f))
                {
                    float ours = mat.GetFloat(prop);
                    if (Mathf.Abs(ours - f) > 0.0005f) { d.Append($" {prop} {ours:0.###}→{f:0.###}"); if (apply) mat.SetFloat(prop, f); }
                }
                else if (section == "m_Colors")
                {
                    var c = Regex.Match(value, @"r: ([-\d.eE]+), g: ([-\d.eE]+), b: ([-\d.eE]+), a: ([-\d.eE]+)");
                    if (!c.Success) continue;
                    var col = new Color(P(c.Groups[1].Value), P(c.Groups[2].Value), P(c.Groups[3].Value), P(c.Groups[4].Value));
                    var ours = mat.GetColor(prop);
                    if (!Same(ours, col)) { d.Append($" {prop} #{ColorUtility.ToHtmlStringRGBA(ours)}→#{ColorUtility.ToHtmlStringRGBA(col)}"); if (apply) mat.SetColor(prop, col); }
                }
            }
            // Từ khoá shader (viền / bóng / vát) theo gốc.
            var keywords = new HashSet<string>();
            var kw = Regex.Match(text, @"m_ValidKeywords:\s*\n((?:\s*- .+\n)*)");
            if (kw.Success) foreach (Match k in Regex.Matches(kw.Groups[1].Value, @"- (\S+)")) keywords.Add(k.Groups[1].Value);
            foreach (var k in new[] { "OUTLINE_ON", "UNDERLAY_ON", "UNDERLAY_INNER", "BEVEL_ON", "GLOW_ON" })
            {
                bool want = keywords.Contains(k), have = mat.IsKeywordEnabled(k);
                if (want == have) continue;
                d.Append($" {k} {have}→{want}");
                if (apply) { if (want) mat.EnableKeyword(k); else mat.DisableKeyword(k); }
            }
            if (apply && d.Length > 0) EditorUtility.SetDirty(mat);
            return d.ToString().Trim();
        }

        private static float P(string s) => float.Parse(s, NumberStyles.Float, CultureInfo.InvariantCulture);
        private static bool Same(Color a, Color b) => Mathf.Abs(a.r - b.r) < 0.003f && Mathf.Abs(a.g - b.g) < 0.003f && Mathf.Abs(a.b - b.b) < 0.003f && Mathf.Abs(a.a - b.a) < 0.003f;
    }
}

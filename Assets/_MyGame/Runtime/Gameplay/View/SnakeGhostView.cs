using System.Collections.Generic;
using SnackDash.Gameplay;
using UnityEngine;

namespace SnackDash.Presentation
{
    /// <summary>
    /// Hình của booster Rắn bóng ma (phần sáng tạo, bản gốc không có): cả rắn (thân, trái trên thân, mắt) trong suốt thật rồi đậm lại,
    /// nhấp nháy như hồn ma; hào quang xanh bung ở đầu lúc bật và mỗi lần xuyên thân; hết thì đặc hẳn như cũ [user 20/10: làm mờ chấm,
    /// phủ màu, xám đều không phải "mờ"]. Shader gốc là loại đặc nên lúc bóng ma đổi tạm material sang bản trong suốt cùng cách tô
    /// (<c>SH_FakeUnlit_Texture_Ghost</c>, tạo một lần mỗi material rồi dùng lại), viền tắt trong lúc đó; hết thì trả material gốc. Gắn cạnh SnakeView.
    /// </summary>
    public class SnakeGhostView : MonoBehaviour
    {
        [Tooltip("Prefabs/InGame/Boosters/FX_Snake_Ghost (biến thể xanh ngọc của FX_Snake_Magnetic, chỉ chùm sáng lúc bung).")]
        [SerializeField] private GameObject auraPrefab;
        [Tooltip("Source/Effect/Shaders/SH_FakeUnlit_Texture_Ghost: bản trong suốt của shader thân rắn.")]
        [SerializeField] private Shader ghostShader;
        [Tooltip("Độ đặc lúc mờ nhất / đậm nhất trong một nhịp (1 = đặc).")]
        [SerializeField, Range(0f, 1f)] private float alphaMin = 0.35f, alphaMax = 1f;
        [Tooltip("Giây một nhịp mờ → đậm.")]
        [SerializeField] private float pulsePeriod = 0.8f;
        [Tooltip("Ánh sáng tự phát cộng vào thân (đường tối nhìn xuyên qua không làm rắn sẫm theo).")]
        [SerializeField] private Color glowColor = new Color(0.6f, 0.9f, 1f, 1f);
        [SerializeField, Range(0f, 1f)] private float glow = 0.3f;
        [Tooltip("Mép sáng như kính (rim): màu, độ sáng và độ mảnh (càng lớn càng sát mép).")]
        [SerializeField] private Color rimColor = Color.white;
        [SerializeField, Range(0f, 2f)] private float rim = 0.9f;
        [SerializeField, Range(0.5f, 8f)] private float rimPower = 2.5f;

        private const string BodyShader = "Shader Graphs/SH_FakeUnlit_Texture"; // thân, đầu, mắt, trái
        private const string OutlineShader = "Universal Render Pipeline/Unlit";  // viền đầu và đốt
        private static readonly int Alpha = Shader.PropertyToID("_Alpha");
        private static readonly int GlowColor = Shader.PropertyToID("_GlowColor");
        private static readonly int Glow = Shader.PropertyToID("_Glow");
        private static readonly int RimColor = Shader.PropertyToID("_RimColor"); // material gốc còn lưu _RimColor cam không dùng: luôn đặt lại
        private static readonly int Rim = Shader.PropertyToID("_Rim");
        private static readonly int RimPower = Shader.PropertyToID("_RimPower");

        private readonly List<Renderer> _renderers = new List<Renderer>();
        private readonly Dictionary<Material, Material> _ghostOf = new Dictionary<Material, Material>(); // material gốc → bản trong suốt
        private readonly Dictionary<Material, int> _kind = new Dictionary<Material, int>();               // 1 thân, 2 viền, 0 khác (đọc tên shader cấp phát)
        private readonly Dictionary<Renderer, Material> _original = new Dictionary<Renderer, Material>();  // để trả lại
        private readonly HashSet<Renderer> _hiddenOutlines = new HashSet<Renderer>(); // viền bật / tắt theo nhịp
        private SnakeView _snake;
        private GameObject _aura;
        private bool _on;
        private float _time;

        public void Bind(GameSession session, SnakeView snake)
        {
            _snake = snake;
            session.GhostChanged += OnGhostChanged;
            // Mỗi lần xuyên qua thân: hào quang bung lại một lần.
            session.GhostPassed += () => { if (_aura != null) foreach (var particles in _aura.GetComponentsInChildren<ParticleSystem>()) particles.Play(true); };
        }

        private void OnGhostChanged(bool on)
        {
            _on = on;
            _time = 0f;
            if (on)
            {
                if (auraPrefab != null && _snake != null && _snake.Head != null && _aura == null)
                {
                    _aura = Instantiate(auraPrefab, _snake.Head);
                    _aura.transform.localPosition = Vector3.zero;
                    _aura.transform.localRotation = Quaternion.identity;
                    foreach (var particles in _aura.GetComponentsInChildren<ParticleSystem>()) particles.Play(true);
                }
                return;
            }
            if (_aura != null)
            {
                Destroy(_aura, 1f); // chùm sáng tan hết
                _aura = null;
            }
            Restore();
        }

        private int Kind(Material material)
        {
            if (!_kind.TryGetValue(material, out int kind))
            {
                string shader = material.shader.name;
                _kind[material] = kind = shader == BodyShader ? 1 : shader == OutlineShader ? 2 : 0;
            }
            return kind;
        }

        private Material GhostOf(Material original)
        {
            if (!_ghostOf.TryGetValue(original, out var ghost))
            {
                ghost = new Material(ghostShader) { name = original.name + " (Ghost)" };
                ghost.CopyPropertiesFromMaterial(original);
                _ghostOf[original] = ghost;
            }
            return ghost;
        }

        private void LateUpdate()
        {
            if (!_on || ghostShader == null) return;
            _time += Time.deltaTime;
            float pulse = 0.5f - 0.5f * Mathf.Cos(_time * 2f * Mathf.PI / Mathf.Max(0.05f, pulsePeriod));
            float alpha = Mathf.Lerp(alphaMin, alphaMax, 1f - pulse); // bật lên là mờ dần trước

            // Đốt mới mọc thêm khi đang bóng ma cũng phải đổi: lấy lại danh sách mỗi frame (List dùng lại, không cấp phát).
            GetComponentsInChildren(_renderers);
            foreach (var renderer in _renderers)
            {
                if (renderer is ParticleSystemRenderer || _original.ContainsKey(renderer) || _hiddenOutlines.Contains(renderer)) continue;
                var material = renderer.sharedMaterial;
                if (material == null) continue;
                int kind = Kind(material);
                if (kind == 1)
                {
                    _original[renderer] = material;
                    renderer.sharedMaterial = GhostOf(material);
                }
                else if (kind == 2) _hiddenOutlines.Add(renderer);
            }
            // Ánh sáng và mép bóng đi theo nhịp: mờ nhất thì sáng nhất, đặc hẳn thì tắt, rắn đúng màu gốc [user 20/10].
            float faint = alphaMax > alphaMin ? Mathf.Clamp01((alphaMax - alpha) / (alphaMax - alphaMin)) : 0f;
            foreach (var ghost in _ghostOf.Values)
            {
                ghost.SetFloat(Alpha, alpha);
                ghost.SetColor(GlowColor, glowColor);
                ghost.SetFloat(Glow, glow * faint);
                ghost.SetColor(RimColor, rimColor);
                ghost.SetFloat(Rim, rim * faint);
                ghost.SetFloat(RimPower, rimPower);
            }
            // Viền đặc lộ qua thân trong suốt: chỉ hiện lúc gần đặc hẳn.
            bool solid = alpha >= 0.97f;
            foreach (var outline in _hiddenOutlines)
                if (outline != null && outline.enabled != solid) outline.enabled = solid;
        }

        /// <summary>Trả material gốc và viền.</summary>
        private void Restore()
        {
            foreach (var pair in _original)
                if (pair.Key != null) pair.Key.sharedMaterial = pair.Value;
            _original.Clear();
            foreach (var outline in _hiddenOutlines)
                if (outline != null) outline.enabled = true;
            _hiddenOutlines.Clear();
        }

        private void OnDestroy()
        {
            foreach (var ghost in _ghostOf.Values) if (ghost != null) Destroy(ghost);
        }
    }
}

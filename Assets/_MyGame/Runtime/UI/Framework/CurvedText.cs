using TMPro;
using UnityEngine;

namespace SnackDash.UI
{
    /// <summary>
    /// Uốn chữ TMP theo cung (gốc TMP_CurveText_ConstantRadius trên VICTORY!, UNLOCKED!, NEW ITEM…) [user 22/10: "victory của người ta
    /// cong cong, của mình thẳng"]. Mỗi ký tự nâng lên <see cref="height"/> x đường cong theo vị trí ngang (0 = mép trái, 1 = mép phải)
    /// và nghiêng theo tiếp tuyến x <see cref="rotationMultiplier"/>. Bản gốc mất thân hàm khi giải nén; chiều cao lấy theo maxHeight
    /// gốc, khớp số đo trên video lv61 (chữ giữa cao hơn hai đầu ~20 đơn vị).
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(TMP_Text))]
    public class CurvedText : MonoBehaviour
    {
        [Tooltip("Hình cung (gốc: 0 -> 1 ở giữa -> 0).")]
        public AnimationCurve curve = new AnimationCurve(
            new Keyframe(0f, 0f, 5.9098005f, 5.9098005f),
            new Keyframe(0.49762f, 0.9970002f, 0.017222892f, 0.017222892f),
            new Keyframe(0.9970139f, 0.011447847f, -5.1889095f, -5.1889095f));
        [Tooltip("Chữ giữa cao hơn hai đầu bấy nhiêu đơn vị (âm = cong xuống). Gốc maxHeight: VICTORY! 24, NEW ITEM thanh -6.")]
        public float height = 24f;
        [Tooltip("1 = nghiêng đúng theo tiếp tuyến; > 1 nghiêng thêm cho vui mắt (gốc 1.1).")]
        public float rotationMultiplier = 1.1f;

        private TMP_Text _text;
        private bool _warping;

        private void OnEnable()
        {
            _text = GetComponent<TMP_Text>();
            TMPro_EventManager.TEXT_CHANGED_EVENT.Add(OnTextChanged);
            Warp();
        }

        private void OnDisable()
        {
            TMPro_EventManager.TEXT_CHANGED_EVENT.Remove(OnTextChanged);
            if (_text != null) _text.ForceMeshUpdate(); // trả chữ thẳng
        }

        private void OnValidate()
        {
            if (isActiveAndEnabled) Warp();
        }

        private void OnTextChanged(Object changed)
        {
            if (changed == _text && !_warping) Warp();
        }

        /// <summary>Dựng lại mesh chữ rồi uốn từng ký tự (gọi tự động mỗi khi TMP dựng lại chữ).</summary>
        public void Warp()
        {
            if (_text == null || _warping) return;
            _warping = true;
            try
            {
                _text.ForceMeshUpdate();
                var info = _text.textInfo;
                if (info.characterCount == 0) return;
                float minX = float.MaxValue, maxX = float.MinValue;
                for (int i = 0; i < info.characterCount; i++)
                {
                    var c = info.characterInfo[i];
                    if (!c.isVisible) continue;
                    minX = Mathf.Min(minX, c.bottomLeft.x);
                    maxX = Mathf.Max(maxX, c.topRight.x);
                }
                float width = maxX - minX;
                if (width <= 0f) return;
                const float Step = 0.01f;
                for (int i = 0; i < info.characterCount; i++)
                {
                    var c = info.characterInfo[i];
                    if (!c.isVisible) continue;
                    float mid = (c.bottomLeft.x + c.topRight.x) * 0.5f;
                    float t = (mid - minX) / width;
                    float lift = curve.Evaluate(t) * height;
                    float slope = (curve.Evaluate(t + Step) - curve.Evaluate(t - Step)) / (2f * Step) * height / width;
                    var pivot = new Vector3(mid, c.baseLine, 0f);
                    var matrix = Matrix4x4.TRS(pivot + new Vector3(0f, lift, 0f), Quaternion.Euler(0f, 0f, Mathf.Atan(slope) * Mathf.Rad2Deg * rotationMultiplier), Vector3.one)
                               * Matrix4x4.Translate(-pivot);
                    var vertices = info.meshInfo[c.materialReferenceIndex].vertices;
                    for (int v = 0; v < 4; v++) vertices[c.vertexIndex + v] = matrix.MultiplyPoint3x4(vertices[c.vertexIndex + v]);
                }
                _text.UpdateVertexData(TMP_VertexDataUpdateFlags.Vertices);
            }
            finally
            {
                _warping = false;
            }
        }
    }
}

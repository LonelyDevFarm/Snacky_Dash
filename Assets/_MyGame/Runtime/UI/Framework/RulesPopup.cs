using SnackDash.Presentation;
using UnityEngine;
using UnityEngine.UI;

namespace SnackDash.UI
{
    /// <summary>
    /// Màn luật chơi nền đen dùng chung (Jungle Heist, Weekly, Snacky Pass): các phần hiện lần lượt (<see cref="RevealSequence"/>),
    /// tiêu đề giãn ngang lúc mở, mũi tên nhún theo hướng của nó, vầng sáng "thở", "Tap to continue!". Chạm sớm thì hiện hết, chạm
    /// lần nữa thì đóng. Lớp con chỉ khai báo khoá "đã xem" (<see cref="SeenKey"/>); ô nào để trống thì bỏ qua phần đó.
    /// </summary>
    public abstract class RulesPopup : BasePopup
    {
        [Tooltip("Vùng chạm (nền tối): chạm sớm hiện hết, chạm lần nữa đóng.")]
        [SerializeField] private Button tapArea;
        [SerializeField] private RevealSequence reveal;
        [Tooltip("Tiêu đề giãn ngang lúc mở (0.1-0.25 s). Để trống nếu không có.")]
        [SerializeField] private RectTransform header;
        [Tooltip("Vầng sáng \"thở\". Để trống nếu không có.")]
        [SerializeField] private RectTransform glow;
        [SerializeField] private float glowAmount = 0.05f;
        [Tooltip("Mũi tên giữa các bước (nhún dọc theo hướng của nó).")]
        [SerializeField] private RectTransform[] arrows = new RectTransform[0];
        [SerializeField] private float arrowDistance = 8f;
        [SerializeField] private float arrowSpeed = 5f;

        private Vector2[] _arrowHome, _arrowWay;
        private Vector3 _headerScale;

        /// <summary>Khoá PlayerPrefs "đã xem" (đặt lúc hiện).</summary>
        protected abstract string SeenKey { get; }

        protected static bool SeenFor(string key) => PlayerPrefs.GetInt(key, 0) != 0;

        protected override void OnInit()
        {
            tapArea.onClick.AddListener(() => { if (!reveal.Ready) reveal.Skip(); else Close(); });
            _arrowHome = new Vector2[arrows.Length];
            _arrowWay = new Vector2[arrows.Length];
            for (int i = 0; i < arrows.Length; i++)
            {
                _arrowHome[i] = arrows[i].anchoredPosition;
                _arrowWay[i] = arrows[i].localRotation * Vector3.right; // hình mũi tên chỉ sang phải trước khi xoay
            }
            if (header != null)
            {
                _headerScale = header.localScale;
                reveal.Tick += Widen;
            }
        }

        protected override void OnShow()
        {
            PlayerPrefs.SetInt(SeenKey, 1);
            PlayerPrefs.Save();
            if (header != null) Widen(0f);
            StartCoroutine(reveal.Play());
        }

        /// <summary>Tiêu đề: cao đủ sau 0.1 s, ngang bật ra (OutBack) 0.1-0.25 s.</summary>
        private void Widen(float t)
        {
            float h = Mathf.Clamp01((t - 0.1f) / 0.15f);
            header.localScale = new Vector3(_headerScale.x * Tween.OutBack(1.7f)(h), _headerScale.y * Mathf.Min(1f, t / 0.1f), 1f);
        }

        private void Update()
        {
            if (_arrowHome == null) return;
            float time = Time.unscaledTime;
            if (reveal.Ready)
                for (int i = 0; i < arrows.Length; i++)
                    arrows[i].anchoredPosition = _arrowHome[i] + _arrowWay[i] * (arrowDistance * Mathf.Sin(time * arrowSpeed + i));
            if (glow != null) glow.localScale = Vector3.one * (1f + glowAmount * Mathf.Sin(time * 3f));
        }
    }
}

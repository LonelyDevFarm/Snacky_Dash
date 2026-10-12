using System;
using System.Collections;
using SnackDash.Presentation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SnackDash.UI
{
    /// <summary>
    /// Một widget sự kiện ở Home (prefab gốc Prefabs/UI/Screens/Home/EventWidget, 3 bản: Weekly, Snacky Pass, Jungle Heist). Mỗi sự kiện mở
    /// từ một level; trước đó widget hiện ổ khoá vàng với "LEVEL N", bấm vào thì ổ khoá lắc và một dòng chữ nổi lên (Hint của
    /// Main.unity) [user]. Mở rồi thì thanh CLAIM xanh, chấm đỏ đếm phần thưởng, ánh sáng lướt qua icon và thanh (UIShiny).
    /// Lớp con chỉ thêm phần riêng của sự kiện.
    /// </summary>
    public abstract class EventWidget : MonoBehaviour
    {
        [Tooltip("Vùng bấm phủ cả widget (Hit).")]
        [SerializeField] private Button button;
        [Tooltip("Thanh tím: ổ khoá + LEVEL N (Jungle Heist: đồng hồ + giờ còn lại).")]
        [SerializeField] protected GameObject lockedBar;
        [SerializeField] protected Image lockedIcon;
        [SerializeField] protected TMP_Text lockedText;
        [SerializeField] protected GameObject claimBar;
        [Tooltip("Chấm đỏ đếm phần thưởng chờ nhận.")]
        [SerializeField] protected GameObject dot;
        [SerializeField] private TMP_Text dotText;
        [Tooltip("Ánh sáng lướt qua icon và thanh CLAIM, chỉ bật khi sự kiện đã mở.")]
        [SerializeField] private UiShiny[] shines;
        [Tooltip("Dòng chữ nổi lên khi bấm lúc còn khoá; {0} là level mở.")]
        [SerializeField] private string lockedWords = "Reach Lv. {0} to unlock!";
        [Tooltip("Prefab Prefabs/UI/Screens/Home/WidgetHint.")]
        [SerializeField] private RectTransform hintPrefab;
        [Tooltip("Hint: hiện ở (0, -339), bay lên 437 trong 1.5 s.")]
        [SerializeField] private float hintY = -339f, hintRise = 437f, hintTime = 1.5f;
        [Tooltip("Lời giới thiệu lần đầu sự kiện mở (Prefabs/UI/Popups/<sự kiện>/*BadgeFtue).")]
        [SerializeField] protected BadgeFtue ftuePrefab;

        private static RectTransform _hint; // bấm lần mới thì thay dòng chữ còn đang bay

        protected HomeView Home { get; private set; }
        protected int Level { get; private set; }
        protected abstract int UnlockLevel { get; }
        public bool IsOpen => Level >= UnlockLevel;

        protected static Core.UIManager UI => Core.Services.Get<Core.UIManager>();

        public void Setup(HomeView home, int level)
        {
            Home = home;
            Level = level;
            bool open = IsOpen;
            lockedBar.SetActive(!open);
            claimBar.SetActive(open);
            lockedText.text = "LEVEL " + UnlockLevel;
            dot.SetActive(open);
            foreach (var shine in shines) if (shine != null) shine.enabled = open;
            button.onClick.AddListener(() => { if (IsOpen) Clicked(); else LockedHint(); });
            if (open) Opened();
        }

        /// <summary>Sự kiện đã mở: bắt đầu theo dõi dữ liệu của nó.</summary>
        protected abstract void Opened();

        /// <summary>Bấm widget đã mở.</summary>
        protected abstract void Clicked();

        protected void Badge(int count)
        {
            dot.SetActive(count > 0);
            dotText.text = count.ToString();
        }

        /// <summary>Lời giới thiệu "… unlocked!" khi Home đã hiện và <paramref name="wait"/> hết đúng; bấm nó thì <see cref="Clicked"/>.</summary>
        protected IEnumerator ShowFtue(Func<bool> wait)
        {
            yield return null;
            while (Home.Starting || wait()) yield return null;
            var ftue = UI.Show(ftuePrefab);
            if (ftue != null) ftue.Setup((RectTransform)transform, Clicked);
        }

        /// <summary>
        /// Widget còn khoá bị bấm: chỉ ổ khoá vàng lắc [user], và Hint gốc chạy ở (0, -339): chữ trắng viền nâu nảy lên 1.25 trong
        /// 0.2 s, hiện dần trong 0.1 s, bay lên 437 trong 1.5 s, mờ đi ở cuối.
        /// </summary>
        private void LockedHint()
        {
            var padlock = lockedIcon.transform;
            var rest = padlock.localRotation;
            StartCoroutine(Tween.Run(0.4f, Tween.Linear, t => padlock.localRotation = rest * Quaternion.Euler(0f, 0f, Mathf.Sin(t * Mathf.PI * 6f) * 15f * (1f - t)), true));
            Audio.Haptics.Light();

            if (_hint != null) Destroy(_hint.gameObject);
            var hint = Instantiate(hintPrefab, GetComponentInParent<Canvas>().rootCanvas.transform);
            _hint = hint;
            hint.GetComponentInChildren<TMP_Text>().text = string.Format(lockedWords, UnlockLevel);
            var group = hint.GetComponent<CanvasGroup>();
            StartCoroutine(Tween.Run(hintTime, Tween.Linear, t =>
            {
                if (hint == null) return;
                float s = t * hintTime;
                hint.localScale = Vector3.one * (s < 0.2f ? 1f + 0.25f * Mathf.Sin(s / 0.2f * Mathf.PI) : 1f);
                hint.anchoredPosition = new Vector2(0f, hintY + hintRise * Tween.OutQuad(t));
                group.alpha = s < 0.1f ? s / 0.1f : s > hintTime - 0.3f ? (hintTime - s) / 0.3f : 1f;
                if (t >= 1f) Destroy(hint.gameObject);
            }, true));
        }
    }
}

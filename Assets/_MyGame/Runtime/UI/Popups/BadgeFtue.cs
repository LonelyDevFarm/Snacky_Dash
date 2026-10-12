using System;
using UnityEngine;
using UnityEngine.UI;

namespace SnackDash.UI
{
    /// <summary>
    /// Lời giới thiệu sự kiện mới mở ở Home (gốc: WeeklyMissionBadgeFtue / SeasonPassBadgeFtue / LavaQuestBadgeFtue, video sk1.3
    /// 0-10 s): Home tối đi, bản sao widget sự kiện nổi lên, bóng nói bên cạnh và bàn tay chỉ vào; chỉ chạm được widget, chạm thì
    /// mở sự kiện. Một script cho 3 prefab <c>Prefabs/UI/Popups/WeeklyMissions/WeeklyBadgeFtue</c>, <c>SnackyPassBadgeFtue</c>, <c>JungleHeistBadgeFtue</c>
    /// (khác chữ, vị trí bóng nói / bàn tay và khoá "đã xem"). Mở bằng <c>UIManager.Show(prefab)</c> rồi <see cref="Setup"/>.
    /// </summary>
    public class BadgeFtue : BasePopup
    {
        public const string WeeklySeenKey = "wm_badge_ftue_seen";
        public const string SnackyPassSeenKey = "sp_badge_ftue_seen";
        public const string JungleHeistSeenKey = "jh_badge_ftue_seen";

        public static bool SeenFor(string key) => PlayerPrefs.GetInt(key, 0) != 0;

        [Tooltip("Khoá PlayerPrefs \"đã xem\" của sự kiện này.")]
        [SerializeField] private string seenKey = WeeklySeenKey;
        [Tooltip("Nền tối hiện dần, bóng nói và bàn tay bật ra.")]
        [SerializeField] private RevealSequence reveal;
        [Tooltip("Nhóm bóng nói + bàn tay, được đặt lên đúng chỗ widget.")]
        [SerializeField] private RectTransform content;
        [Tooltip("Chỗ đặt bản sao widget.")]
        [SerializeField] private RectTransform slot;
        [Tooltip("Bàn tay (nhấn vào widget rồi lùi ra).")]
        [SerializeField] private RectTransform hand;
        [SerializeField] private float handDistance = 18f;
        [Tooltip("Số lần nhấn mỗi giây.")]
        [SerializeField] private float handSpeed = 0.9f;

        private Vector2 _handRest, _handWay;
        private Vector3 _handScale;

        protected override void OnInit()
        {
            _handRest = hand.anchoredPosition;
            _handScale = hand.localScale;
            _handWay = ((RectTransform)hand.parent).anchoredPosition.normalized; // từ widget ra phía bàn tay
        }

        protected override void OnShow()
        {
            PlayerPrefs.SetInt(seenKey, 1);
            PlayerPrefs.Save();
            StartCoroutine(reveal.Play());
        }

        /// <summary>Đặt lên widget của Home và chép widget vào; chạm bản sao thì đóng và gọi <paramref name="open"/>.</summary>
        public void Setup(RectTransform widget, Action open)
        {
            content.position = widget.position;
            var copy = (RectTransform)Instantiate(widget, slot);
            copy.name = "Widget";
            copy.anchorMin = copy.anchorMax = new Vector2(0.5f, 0.5f);
            copy.anchoredPosition = Vector2.zero;
            copy.localScale = widget.lossyScale.x / slot.lossyScale.x * Vector3.one;
            foreach (var button in copy.GetComponentsInChildren<Button>(true)) Destroy(button);
            var hit = UiBuilder.Image(UiBuilder.Stretch(copy, "Tap Area"), null, Color.clear, raycast: true);
            hit.gameObject.AddComponent<Button>().onClick.AddListener(() => Close(open));
            UiBuilder.Pressable(copy);
        }

        private void Update()
        {
            if (_handScale == Vector3.zero) return;
            float push = Mathf.Abs(Mathf.Sin(Time.unscaledTime * Mathf.PI * handSpeed));
            hand.anchoredPosition = _handRest - _handWay * handDistance * push;
            hand.localScale = _handScale * (1f - 0.06f * push);
        }
    }
}

using System.Collections;
using SnackDash.Economy;
using SnackDash.Presentation;
using UnityEngine;
using UnityEngine.UI;

namespace SnackDash.UI
{
    /// <summary>
    /// LUCKY LADYBUG ở Home (prefab Prefabs/UI/Screens/Home/LuckyLadybugWidget, dưới Snacky Pass): thanh tím hiện đồng hồ của vòng, CLAIM khi
    /// rương chờ nhận. Mở sự kiện lần đầu: lời giới thiệu (BadgeFtue) rồi luật chơi. Về Home sau khi thắng màn có bắt bọ: một con bọ bay
    /// từ nút Play vào widget, widget nảy. Đủ bọ: popup chính tự mở và nhận rương. Báo nút Play gắn huy hiệu khi màn kế có bọ.
    /// </summary>
    public class LuckyLadybugWidget : EventWidget
    {
        [Tooltip("S_Clock_Icon_Widget_0: thay ổ khoá khi sự kiện đã mở.")]
        [SerializeField] private Sprite clockIcon;
        [Tooltip("Con bọ bay từ nút Play vào widget (Image, tắt sẵn).")]
        [SerializeField] private RectTransform flyingBug;
        [SerializeField] private float flyTime = 0.7f;
        [SerializeField] private RectTransform icon;

        private bool _listening;
        private float _tick;

        protected override int UnlockLevel => LuckyLadybug.UnlockLevel;

        protected override void Opened()
        {
            var ladybug = LuckyLadybug.Shared;
            ladybug.StartIfUnlocked(Level);
            lockedIcon.sprite = clockIcon;
            ladybug.Changed += Label;
            _listening = true;
            Label();
            if (!BadgeFtue.SeenFor(BadgeFtue.LuckyLadybugSeenKey))
                StartCoroutine(ShowFtue(() => Blocked()));
            else StartCoroutine(Entry());
        }

        private void OnDestroy()
        {
            if (_listening) LuckyLadybug.Shared.Changed -= Label;
        }

        private void Update()
        {
            if (!_listening || (_tick -= Time.unscaledDeltaTime) > 0f) return;
            _tick = 0.25f;
            var ladybug = LuckyLadybug.Shared;
            if (!ladybug.Complete && ladybug.Left <= System.TimeSpan.Zero) ladybug.StartIfUnlocked(Level); // hết giờ: vòng mới
            Label();
        }

        /// <summary>Thanh: CLAIM khi rương chờ nhận, còn lại là đồng hồ; nút Play gắn huy hiệu khi màn kế có bọ.</summary>
        private void Label()
        {
            var ladybug = LuckyLadybug.Shared;
            bool claim = ladybug.Complete && !ladybug.Claimed;
            lockedBar.SetActive(!claim);
            claimBar.SetActive(claim);
            dot.SetActive(claim);
            lockedText.text = ladybug.Complete ? "Done!" : TimerBanner.Format(ladybug.Left);
            if (Home != null && Home.PlayButton != null) Home.PlayButton.ShowLadybug(ladybug.LadybugThisLevel);
        }

        /// <summary>Popup của sự kiện khác đang mở (không đè lên chúng).</summary>
        private bool Blocked() =>
            UI.IsOpen<BadgeFtue>() || UI.IsOpen<WeeklyMissionsPopup>() || UI.IsOpen<SnackyPassView>() || UI.IsOpen<JungleHeistMain>() || UI.IsOpen<JungleHeistJoin>();

        /// <summary>Đợi Home sạch, rồi: bọ mới bay vào widget; đủ bọ thì popup chính mở và nhận rương.</summary>
        private IEnumerator Entry()
        {
            yield return null;
            while (Home.Busy || Blocked()) yield return null;
            var ladybug = LuckyLadybug.Shared;
            if (ladybug.HasNewLadybug)
            {
                yield return FlyFromPlay();
                ladybug.SeenCount = ladybug.Count;
            }
            if (ladybug.Complete && !ladybug.Claimed)
            {
                var popup = UI.Show<LuckyLadybugPopup>();
                if (popup != null) popup.ClaimNow();
            }
        }

        /// <summary>Con bọ bay theo đường cong từ nút Play vào icon widget; widget nảy khi tới.</summary>
        private IEnumerator FlyFromPlay()
        {
            if (flyingBug == null || Home.PlayButton == null) yield break;
            var parent = (RectTransform)flyingBug.parent;
            Vector2 Local(Vector3 world) { RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, RectTransformUtility.WorldToScreenPoint(null, world), null, out var p); return p; }
            Vector2 start = Local(Home.PlayButton.transform.position), end = Local(icon.position);
            Vector2 control = (start + end) * 0.5f + new Vector2(-250f, 150f);
            flyingBug.gameObject.SetActive(true);
            yield return Tween.Run(flyTime, Tween.InOutSine, t =>
            {
                float u = 1f - t;
                flyingBug.anchoredPosition = u * u * start + 2f * u * t * control + t * t * end;
                flyingBug.localScale = Vector3.one * Mathf.Lerp(1.2f, 0.7f, t);
            }, true);
            flyingBug.gameObject.SetActive(false);
            SnackDash.Audio.SoundPlayer.Play("CollectCoin");
            SnackDash.Audio.Haptics.Light();
            yield return new WaitForSeconds(ClipPlayer.Play(icon)); // icon nảy: clip LB_Pop trên S_Icon
        }

        protected override void Clicked()
        {
            LuckyLadybug.Shared.StartIfUnlocked(Level);
            UI.Show<LuckyLadybugPopup>();
            if (!LuckyLadybugRules.Seen) UI.Show<LuckyLadybugRules>();
        }
    }
}

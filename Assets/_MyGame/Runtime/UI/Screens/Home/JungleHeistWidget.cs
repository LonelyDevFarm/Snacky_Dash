using System.Collections;
using SnackDash.Economy;
using UnityEngine;

namespace SnackDash.UI
{
    /// <summary>
    /// JUNGLE HEIST (LavaQuestWidget, Widget_Unlocked): thanh tím hiện đồng hồ và giờ còn lại của vòng; "Start" khi tham gia được;
    /// CLAIM khi thắng chưa nhận (LavaQuestWidgetState). Bấm: popup tham gia (rồi tìm người chơi, luật chơi lần đầu, màn sông), hoặc
    /// màn sông khi đang chơi / đã xong. Về Home có tin mới thì màn sông tự mở (LavaQuestHomeEntryGate) [user: "có"].
    /// </summary>
    public class JungleHeistWidget : EventWidget
    {
        [Tooltip("S_Clock_Icon_Widget_0: thay ổ khoá khi sự kiện đã mở.")]
        [SerializeField] private Sprite clockIcon;
        [Tooltip("Lề trái chữ \"Start\" (không có đồng hồ bên cạnh).")]
        [SerializeField] private float startTextLeft = 10f;

        private bool _listening;
        private float _textLeft, _tick;

        protected override int UnlockLevel => JungleHeist.UnlockLevel;

        protected override void Opened()
        {
            var heist = JungleHeist.Shared;
            heist.StartIfUnlocked(Level);
            lockedIcon.sprite = clockIcon;
            _textLeft = lockedText.rectTransform.offsetMin.x;
            heist.Changed += Label;
            _listening = true;
            Label();
            if (!BadgeFtue.SeenFor(BadgeFtue.JungleHeistSeenKey))
                StartCoroutine(ShowFtue(() => UI.IsOpen<BadgeFtue>() || UI.IsOpen<WeeklyMissionsPopup>() || UI.IsOpen<SnackyPassView>()));
            else StartCoroutine(Entry());
        }

        private void OnDestroy()
        {
            if (_listening) JungleHeist.Shared.Changed -= Label;
        }

        private void Update()
        {
            if (!_listening || (_tick -= Time.unscaledDeltaTime) > 0f) return;
            _tick = 0.25f;
            if (JungleHeist.Shared.Left <= System.TimeSpan.Zero) JungleHeist.Shared.StartIfUnlocked(Level); // vòng sau
            Label();
        }

        /// <summary>Thanh theo trạng thái: "Start" để tham gia, CLAIM khi có giải, còn lại là đồng hồ.</summary>
        private void Label()
        {
            var heist = JungleHeist.Shared;
            bool claim = heist.Current == JungleHeist.State.Won && !heist.PrizeClaimed;
            lockedBar.SetActive(!claim);
            claimBar.SetActive(claim);
            dot.SetActive(claim);
            bool start = heist.CanJoin;
            lockedIcon.gameObject.SetActive(!start);
            lockedText.text = start ? "Start" : JungleHeistUi.Clock(heist.Left); // LocData jungle_heist_widget_start_hint
            var rect = lockedText.rectTransform;
            rect.offsetMin = new Vector2(start ? startTextLeft : _textLeft, rect.offsetMin.y);
        }

        /// <summary>Đợi Home sạch (màn chờ, xu thắng, các popup khác) rồi mở màn sông nếu có gì mới trong lượt chơi.</summary>
        private IEnumerator Entry()
        {
            yield return null;
            while (Home.Busy || UI.IsOpen<BadgeFtue>() || UI.IsOpen<WeeklyMissionsPopup>() || UI.IsOpen<SnackyPassView>()) yield return null;
            if (JungleHeist.Shared.HasNews) Clicked();
        }

        protected override void Clicked()
        {
            var heist = JungleHeist.Shared;
            heist.StartIfUnlocked(Level);
            if (!heist.CanJoin)
            {
                UI.Show<JungleHeistMain>();
                return;
            }
            var join = UI.Show<JungleHeistJoin>();
            if (join == null) return;
            join.Joined += () =>
            {
                heist.Join();
                // "Finding players on your level" trước (Popup_Lava_Quest_PurchaseOverlay), rồi màn sông.
                var match = UI.Show<JungleHeistMatch>();
                if (match == null) return;
                match.Continued += () =>
                {
                    UI.Show<JungleHeistMain>();
                    if (!JungleHeistRules.Seen) UI.Show<JungleHeistRules>();
                };
            };
        }
    }
}

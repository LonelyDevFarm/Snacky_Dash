using System;
using System.Collections;
using System.Collections.Generic;
using SnackDash.Core;
using SnackDash.Economy;
using SnackDash.Presentation;
using UnityEngine;

namespace SnackDash.UI
{
    /// <summary>
    /// Màn sông JUNGLE HEIST (prefab <c>Prefabs/UI/Popups/JungleHeist/JungleHeistMain</c>, gốc: Popup_Lava_Quest_Main): dòng sông, 7 lá sen và
    /// thuyền kho báu; bảng trên cùng có tiêu đề, 2 ô đếm (Levels "n/7", Players "n/100"), lời nhắn và đồng hồ. Người chơi đứng
    /// trên lá sen của bước đã tới: mỗi lần thắng nhảy lên lá kế, người bị loại rơi xuống nước và chìm sau 2 giây
    /// (LavaQuestMapView._sinkDelay), ô đếm chạy trong 1.3 giây (LavaQuestPopupView._counterAnimationDuration); thắng đủ 7 lên
    /// thuyền, thua thì rơi xuống nước. Gốc vẽ người chơi là rắn 3D, ở đây là avatar (khối <c>Widgets/Avatar</c>).
    /// Vị trí lá sen / thuyền lấy từ prefab (kéo thả được), điểm đứng và điểm rơi sửa trong danh sách Pads.
    /// Mở bằng <c>UIManager.Show&lt;JungleHeistMain&gt;()</c>.
    /// </summary>
    public class JungleHeistMain : BasePopup
    {
        [Serializable]
        public class Pad
        {
            [Tooltip("Lá sen trên bản đồ (View_LavaQuest_Lilypad_n).")]
            public RectTransform pad;
            [Tooltip("Chỗ người chơi đứng, lệch so với tâm lá sen.")]
            public Vector2 stand;
            [Tooltip("Các điểm rơi xuống nước quanh lá sen (WaterAnchors), lệch so với tâm lá.")]
            public Vector2[] water = new Vector2[0];
        }

        [Header("Bản đồ")]
        [SerializeField] private Pad[] pads = new Pad[0];
        [Tooltip("Thuyền kho báu: đích sau 7 lần thắng.")]
        [SerializeField] private RectTransform boat;
        [Tooltip("Nơi chứa avatar người chơi (cùng hệ toạ độ với lá sen).")]
        [SerializeField] private RectTransform crowd;
        [SerializeField] private AvatarView avatarPrefab;
        [SerializeField] private float playerScale = 1.15f;
        [SerializeField] private float botScale = 0.9f;
        [Tooltip("Số người khác vẽ trên một lá sen (còn lại chỉ đếm).")]
        [SerializeField, Min(0)] private int shownBots = 5;

        [Header("Bảng")]
        [SerializeField] private CounterBox levels;
        [SerializeField] private CounterBox players;
        [SerializeField] private TMPro.TMP_Text hint;
        [SerializeField] private TimerBanner timer;
        [SerializeField] private UiButton infoButton;
        [SerializeField, TextArea] private string hintStart = "Win 7 levels in a row to complete the challenge!";
        [SerializeField, TextArea] private string hintAdvance = "Congratulations! You advanced to the next step!";
        [SerializeField, TextArea] private string hintLost = "You failed the challenge. Better luck next time!";

        [Header("Nhịp")]
        [SerializeField] private float jumpTime = 0.5f;
        [SerializeField] private float jumpHeight = 120f;
        [SerializeField] private float sinkDelay = 2f;
        [SerializeField] private float counterTime = 1.3f;

        private JungleHeist _heist;
        private AvatarView _player;
        private readonly List<AvatarView> _bots = new List<AvatarView>();
        private float _tick;

        protected override void OnInit()
        {
            _heist = JungleHeist.Shared;
            infoButton.Clicked += () => Services.Get<UIManager>().Show<JungleHeistRules>();
        }

        protected override void OnShow() => StartCoroutine(Play());

        private Vector2 PadPoint(int step) => step >= JungleHeist.Steps ? boat.anchoredPosition : pads[step].pad.anchoredPosition + pads[step].stand;

        private Vector2 Water(int step, int index)
        {
            var pad = pads[Mathf.Min(step, pads.Length - 1)];
            return pad.pad.anchoredPosition + pad.water[index % pad.water.Length];
        }

        /// <summary>Người chơi giữa lá sen, người khác xếp vòng quanh, tối đa <see cref="shownBots"/> người.</summary>
        private Vector2 Spot(int step, int index)
        {
            var centre = PadPoint(step);
            if (index == 0) return centre + new Vector2(0f, 6f);
            float angle = (index - 1) * 72f * Mathf.Deg2Rad;
            return centre + new Vector2(Mathf.Cos(angle) * 70f, Mathf.Sin(angle) * 28f + 20f);
        }

        private AvatarView Spawn(Vector2 at, float scale, int icon, bool player)
        {
            var avatar = Instantiate(avatarPrefab, crowd);
            var rect = (RectTransform)avatar.transform;
            rect.anchoredPosition = at;
            rect.localScale = Vector3.one * scale;
            avatar.Set(icon, player);
            return avatar;
        }

        private void Place(int step, int survivors)
        {
            int shown = Mathf.Min(shownBots, Mathf.Max(0, survivors - 1));
            for (int i = 0; i < shown; i++) _bots.Add(Spawn(Spot(step, i + 1), botScale, i + 1, false));
            _player = Spawn(Spot(step, 0), playerScale, 5, true);
        }

        private void ShowCounters(int step, int survivors)
        {
            levels.SetValue($"{step}/{JungleHeist.Steps}");
            players.SetValue($"{survivors}/{JungleHeist.Participants}");
        }

        /// <summary>Lúc mở: cuộc đua như lần xem trước; rồi từng lần thắng mới (nhảy, người bị loại rơi, ô đếm chạy), hoặc thua (rơi
        /// xuống nước), cuối cùng popup kết thúc / phần thưởng.</summary>
        private IEnumerator Play()
        {
            int seen = Mathf.Min(_heist.SeenStep, _heist.Step);
            Place(seen, JungleHeist.SurvivorsPerStep[seen]);
            ShowCounters(seen, JungleHeist.SurvivorsPerStep[seen]);
            hint.text = hintStart;
            yield return new WaitForSecondsRealtime(0.4f);

            while (seen < _heist.Step)
            {
                yield return Advance(seen, seen + 1);
                seen++;
                _heist.SeenStep = seen;
            }
            var ui = Services.Get<UIManager>();
            if (_heist.Current == JungleHeist.State.Lost)
            {
                hint.text = hintLost;
                yield return Fall(_player, Water(seen, 0));
                yield return new WaitForSecondsRealtime(0.6f);
                _heist.ResultSeen = true; // thua chỉ chiếu một lần ở Home
                ShowEnded(ui, false);
            }
            else if (_heist.Current == JungleHeist.State.Won)
            {
                yield return new WaitForSecondsRealtime(0.6f);
                // Phần thưởng trước (Popup_Lava_Quest_Rewards); nhận rồi thì popup kết thúc thắng với giờ đợt sau.
                if (!_heist.PrizeClaimed)
                {
                    var rewards = ui.Show<JungleHeistRewards>();
                    if (rewards != null) rewards.Closed += () => Close();
                }
                else ShowEnded(ui, true);
            }
        }

        /// <summary>Thắng: ai còn lại lần lượt nhảy sang lá sen kế (hoặc thuyền), số còn lại rơi xuống nước.</summary>
        private IEnumerator Advance(int from, int to)
        {
            int before = JungleHeist.SurvivorsPerStep[from], after = JungleHeist.SurvivorsPerStep[to];
            int keep = Mathf.Min(shownBots, Mathf.Max(0, after - 1));
            hint.text = hintAdvance;
            var jumps = new List<Coroutine> { StartCoroutine(Jump(_player, Spot(to, 0), 0f)) };
            for (int i = 0; i < _bots.Count; i++)
            {
                if (i < keep) jumps.Add(StartCoroutine(Jump(_bots[i], Spot(to, i + 1), 0.08f * (i + 1))));
                else StartCoroutine(Fall(_bots[i], Water(from, i)));
            }
            if (_bots.Count > keep) _bots.RemoveRange(keep, _bots.Count - keep);
            StartCoroutine(RunCounters(from, to, before, after));
            foreach (var jump in jumps) yield return jump;
            yield return new WaitForSecondsRealtime(0.3f);
        }

        private IEnumerator Jump(AvatarView who, Vector2 to, float delay)
        {
            yield return new WaitForSecondsRealtime(delay);
            var rect = (RectTransform)who.transform;
            Vector2 from = rect.anchoredPosition;
            var scale = rect.localScale;
            SnackDash.Audio.Haptics.Light();
            yield return Tween.Run(jumpTime, Tween.Linear, t =>
            {
                rect.anchoredPosition = Vector2.Lerp(from, to, Tween.InOutSine(t)) + Vector2.up * Mathf.Sin(t * Mathf.PI) * jumpHeight;
                rect.localScale = scale * (1f + 0.15f * Mathf.Sin(t * Mathf.PI));
            }, true);
        }

        /// <summary>Rơi xuống nước ở một điểm rơi của lá sen; sau <see cref="sinkDelay"/> giây chìm (thu nhỏ, mờ dần).</summary>
        private IEnumerator Fall(AvatarView who, Vector2 water)
        {
            var rect = (RectTransform)who.transform;
            Vector2 from = rect.anchoredPosition;
            yield return Tween.Run(0.45f, Tween.Linear, t =>
            {
                rect.anchoredPosition = Vector2.Lerp(from, water, t) + Vector2.up * Mathf.Sin(t * Mathf.PI) * 60f;
                rect.localRotation = Quaternion.Euler(0f, 0f, -40f * t);
            }, true);
            SnackDash.Audio.Haptics.Light();
            yield return new WaitForSecondsRealtime(sinkDelay);
            var group = who.gameObject.AddComponent<CanvasGroup>();
            var scale = rect.localScale;
            yield return Tween.Run(0.5f, Tween.InQuad, t =>
            {
                rect.localScale = scale * (1f - 0.6f * t);
                rect.anchoredPosition = water + Vector2.down * 25f * t;
                group.alpha = 1f - t;
            }, true);
            if (who != _player) Destroy(who.gameObject);
        }

        private IEnumerator RunCounters(int fromStep, int toStep, int fromPlayers, int toPlayers)
        {
            yield return Tween.Run(counterTime, Tween.OutQuad, t =>
            {
                levels.SetValue($"{Mathf.RoundToInt(Mathf.Lerp(fromStep, toStep, t))}/{JungleHeist.Steps}");
                players.SetValue($"{Mathf.RoundToInt(Mathf.Lerp(fromPlayers, toPlayers, t))}/{JungleHeist.Participants}");
            }, true);
            foreach (var box in new[] { levels, players })
            {
                var target = box.Value.transform;
                StartCoroutine(Tween.Punch(0.1f, s => target.localScale = Vector3.one * (1f + 0.2f * s)));
            }
        }

        private void Update()
        {
            if (_heist == null || (_tick -= Time.unscaledDeltaTime) > 0f) return;
            _tick = 1f;
            timer.Show(_heist.Left);
        }

        /// <summary>Popup kết thúc (thắng / thua); đóng nó là đóng luôn màn sông.</summary>
        private void ShowEnded(UIManager ui, bool won)
        {
            var ended = ui.Show<JungleHeistEnded>();
            if (ended == null) { Close(); return; }
            ended.Setup(won);
            ended.Closed += () => Close();
        }
    }
}

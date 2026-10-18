using System;
using System.Collections;
using System.Collections.Generic;
using SnackDash.Economy;
using SnackDash.Presentation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SnackDash.UI
{
    /// <summary>
    /// Popup chính Lucky Ladybug (prefab <c>Prefabs/UI/Popups/LuckyLadybug/LuckyLadybugPopup</c>): khung gỗ có lá, nền rừng, tiêu đề, đồng hồ của
    /// vòng, 5 ô đá (sáng lên khi đã có bọ), rương, nút Play (đủ bọ thì Claim), nút i mở luật. Claim: nhận rương (LuckyLadybug.Claim)
    /// rồi popup rương mở ra. Bọ mới (từ lần xem trước) bay từ nút Play vào ô của nó; ô có bọ có ánh sáng lướt qua; đủ bọ thì
    /// 5 ô nảy lần lượt và rương rung rồi mới mở [user 18/10]. Mở bằng <c>UIManager.Show&lt;LuckyLadybugPopup&gt;()</c>; Home mở nó tự động và gọi <see cref="ClaimNow"/> khi đủ bọ.
    /// </summary>
    public class LuckyLadybugPopup : BasePopup
    {
        [Tooltip("5 ô theo đường đi, theo thứ tự thu.")]
        [SerializeField] private Image[] slots = new Image[0];
        [SerializeField] private Sprite emptySlot, filledSlot;
        [SerializeField] private Image chest;
        [SerializeField] private TimerBanner timer;
        [SerializeField] private TMP_Text hint;
        [Tooltip("{0}: số bọ còn thiếu.")]
        [SerializeField] private string hintWords = "Catch {0} more ladybugs!";
        [SerializeField] private string doneWords = "All ladybugs caught!";
        [SerializeField] private UiButton playButton;
        [SerializeField] private UiButton infoButton;
        [Header("Phần thưởng (popup rương)")]
        [SerializeField] private Sprite coinSprite;
        [SerializeField] private Sprite infiniteLivesSprite;
        [Tooltip("Xẻng, boomerang, hoàn tác, nam châm.")]
        [SerializeField] private Sprite[] boosterSprites = new Sprite[4];
        [Header("Hiệu ứng — clip trong Source/Animation/LuckyLadybug, sửa bằng cửa sổ Animation")]
        [Tooltip("Bọ nhỏ (tắt sẵn) bay từ nút Play vào ô mới điền.")]
        [SerializeField] private RectTransform flyingBug;
        [Tooltip("Chùm lấp lánh (prefab LadybugSparkle, component Animation clip LB_Sparkle): chạy lại ở ô vừa điền và ở rương.")]
        [SerializeField] private Animation sparkle;
        [SerializeField] private float openWait = 0.35f, flyTime = 0.55f;
        [Tooltip("Đủ bọ: 5 ô nảy lần lượt (clip LB_Pop trên mỗi ô) cách nhau chừng này giây, rồi rương rung (clip LB_ChestShake trên Chest).")]
        [SerializeField] private float celebrateStep = 0.09f;

        /// <summary>Số ô popup đã cho xem điền (ô mới hơn số này thì bọ bay vào khi mở popup).</summary>
        private const string ShownKey = "lb_popup_shown";
        private float _tick;
        private bool _claiming, _filling;

        private static Core.UIManager UI => Core.Services.Get<Core.UIManager>();

        protected override void OnInit()
        {
            playButton.Clicked += () =>
            {
                var ladybug = LuckyLadybug.Shared;
                if (ladybug.Complete && !ladybug.Claimed) ClaimNow();
                else Close(StartLevel);
            };
            infoButton.Clicked += () => UI.Show<LuckyLadybugRules>();
        }

        protected override void OnShow()
        {
            _claiming = false;
            int count = LuckyLadybug.Shared.Count;
            int shown = PlayerPrefs.GetInt(ShownKey, 0);
            if (shown > count) shown = 0; // vòng mới
            Refresh(shown);
            if (count > shown) StartCoroutine(Fill(shown, count));
            else Remember(count);
        }

        private static void Remember(int shown)
        {
            PlayerPrefs.SetInt(ShownKey, shown);
            PlayerPrefs.Save();
        }

        /// <summary>Vẽ ô, nhãn nút, lời nhắn, đồng hồ; <paramref name="filled"/>: số ô tô sáng (nhỏ hơn số bọ khi còn ô chờ bọ bay vào).</summary>
        private void Refresh(int filled = -1)
        {
            var ladybug = LuckyLadybug.Shared;
            int count = ladybug.Count;
            if (filled < 0) filled = count;
            for (int i = 0; i < slots.Length; i++) SetSlot(i, i < filled);
            bool claim = ladybug.Complete && !ladybug.Claimed;
            playButton.Label = claim ? "Claim" : "Play";
            hint.text = claim ? doneWords : string.Format(hintWords, Mathf.Max(0, LuckyLadybug.Target - count));
            ShowTime();
        }

        /// <summary>Ô có bọ: ảnh sáng và ánh sáng lướt qua (UiShiny trên ô, tắt khi trống).</summary>
        private void SetSlot(int i, bool filled)
        {
            slots[i].sprite = filled ? filledSlot : emptySlot;
            var shine = slots[i].GetComponent<UiShiny>();
            if (shine != null) shine.enabled = filled;
        }

        /// <summary>Mục 1: mỗi bọ mới bay từ nút Play vào ô của nó, ô nảy và lấp lánh.</summary>
        private IEnumerator Fill(int from, int to)
        {
            _filling = true;
            yield return new WaitForSecondsRealtime(openWait);
            var parent = (RectTransform)flyingBug.parent;
            for (int i = from; i < to && i < slots.Length; i++)
            {
                Vector2 start = Local(parent, playButton.transform.position), end = Local(parent, slots[i].transform.position);
                Vector2 control = (start + end) * 0.5f + new Vector2(end.x >= start.x ? -220f : 220f, 120f);
                flyingBug.gameObject.SetActive(true);
                yield return Tween.Run(flyTime, Tween.InOutSine, t =>
                {
                    float u = 1f - t;
                    flyingBug.anchoredPosition = u * u * start + 2f * u * t * control + t * t * end;
                    flyingBug.localScale = Vector3.one * Mathf.Lerp(1.3f, 1f, t);
                }, true);
                flyingBug.gameObject.SetActive(false);
                SetSlot(i, true);
                Remember(i + 1);
                Audio.SoundPlayer.Play("CollectCoin");
                Audio.Haptics.Light();
                Burst(slots[i].transform.position);
                yield return new WaitForSecondsRealtime(ClipPlayer.Play(slots[i]));
            }
            _filling = false;
        }

        private void Burst(Vector3 at)
        {
            if (sparkle == null) return;
            sparkle.transform.position = at;
            ClipPlayer.Play(sparkle);
        }

        private static Vector2 Local(RectTransform parent, Vector3 world)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, RectTransformUtility.WorldToScreenPoint(null, world), null, out var p);
            return p;
        }

        /// <summary>Mục 2: đủ bọ thì 5 ô nảy lần lượt, rồi rương rung trước khi mở.</summary>
        private IEnumerator Celebrate()
        {
            float pop = 0f;
            for (int i = 0; i < slots.Length; i++)
            {
                pop = ClipPlayer.Play(slots[i]);
                yield return new WaitForSecondsRealtime(celebrateStep);
            }
            yield return new WaitForSecondsRealtime(pop);
            Burst(chest.transform.position);
            yield return new WaitForSecondsRealtime(ClipPlayer.Play(chest));
        }

        private void ShowTime()
        {
            var ladybug = LuckyLadybug.Shared;
            if (ladybug.Complete) timer.ShowText("Done!");
            else timer.Show(ladybug.Left);
        }

        private void Update()
        {
            if ((_tick -= Time.unscaledDeltaTime) > 0f) return;
            _tick = 0.25f;
            ShowTime();
        }

        /// <summary>Đủ bọ: chờ bọ bay vào ô xong, ăn mừng (mục 2), nhận rương (thưởng vào ví ngay), popup rương mở từ chỗ rương;
        /// xong thì vòng mới đã mở, popup vẽ lại.</summary>
        public void ClaimNow()
        {
            if (_claiming) return;
            var ladybug = LuckyLadybug.Shared;
            if (!ladybug.Complete || ladybug.Claimed) return;
            _claiming = true;
            StartCoroutine(Claim());
        }

        private IEnumerator Claim()
        {
            while (_filling) yield return null;
            yield return Celebrate();
            var ladybug = LuckyLadybug.Shared;
            var rewards = new List<(Sprite, string)> { (coinSprite, LuckyLadybug.RewardCoins.ToString()) };
            if (LuckyLadybug.RewardEachBooster > 0)
                foreach (var sprite in boosterSprites) rewards.Add((sprite, "x" + LuckyLadybug.RewardEachBooster));
            if (LuckyLadybug.RewardInfiniteMinutes > 0) rewards.Add((infiniteLivesSprite, LuckyLadybug.RewardInfiniteMinutes + "m"));
            ladybug.Claim();
            Remember(0);
            var popup = UI.Show<LadybugChestPopup>();
            Action done = () => { _claiming = false; Refresh(); };
            if (popup != null) popup.Play(chest.sprite, rewards, chest.transform.position, done);
            else done();
        }

        /// <summary>Nút Play: vào màn như nút Play của Home (hết tim thì popup nạp tim).</summary>
        private static void StartLevel()
        {
            if (PlayerEconomy.Shared.CanPlay) LoadingScreen.Load(LoadingScreen.GameScene);
            else UI.Show<LivesPopup>();
        }
    }
}

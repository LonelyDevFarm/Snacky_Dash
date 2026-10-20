using System;
using System.Collections;
using System.Collections.Generic;
using SnackDash.Core;
using SnackDash.Economy;
using SnackDash.Presentation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SnackDash.UI
{
    /// <summary>
    /// WEEKLY ADVENTURE (prefab <c>Prefabs/UI/Popups/WeeklyMissions/WeeklyMissionsPopup</c>, gốc: Resources/liveops/wm, video sk1 / sk1.1). Hàng nhiệm vụ,
    /// tab ngày, mốc rương và bóng nói là prefab riêng trong <c>Prefabs/UI/Popups/WeeklyMissions</c>, script này chỉ xếp và chạy chúng: hoạt ảnh mở
    /// (Weekly Missions_Opening.anim), các hàng của ngày đang chọn hiện lần lượt (nhận được trước, đang làm, đã nhận sau cùng), nhận
    /// thưởng (phần thưởng phóng to tại chỗ, mũ bay về thanh, xu và tim bay về ô đếm Home, các hàng trượt về chỗ mới) và màn rương khi
    /// đạt mốc (<see cref="WeeklyChestReward"/>). Mở bằng <c>UIManager.Show&lt;WeeklyMissionsPopup&gt;()</c>.
    /// </summary>
    public class WeeklyMissionsPopup : BasePopup
    {
        [Serializable]
        public struct GoalIcon
        {
            public MissionGoal goal;
            public Sprite icon;
        }

        [Header("Khung")]
        [Tooltip("Nền tối (độ đậm lấy theo màu đang đặt).")]
        [SerializeField] private Image cover;
        [Tooltip("Bảng tiêu đề (khối WeeklyTitle): giãn ngang lúc mở.")]
        [SerializeField] private RectTransform header;
        [SerializeField] private RectTransform headerText;
        [Tooltip("Mask: phần thân trượt xuống từ dưới tiêu đề.")]
        [SerializeField] private RectTransform mask;
        [SerializeField] private RectTransform body;
        [SerializeField] private UiButton cancelButton;
        [SerializeField] private UiButton infoButton;
        [SerializeField] private TMP_Text timer;
        [SerializeField] private Transform clockHand;

        [Header("Thanh mũ và mốc rương")]
        [SerializeField] private RectTransform fill;
        [SerializeField] private RectTransform markers;
        [SerializeField] private RectTransform hatCounter;
        [SerializeField] private TMP_Text hats;
        [Tooltip("Vị trí các mốc dọc thanh (0..1): đều nhau từ mũ tới giải lớn [user: đều, sk1.1].")]
        [SerializeField] private float[] markerAt = { 0.271f, 0.514f, 0.757f, 1f };
        [SerializeField] private WeeklyMilestone milestonePrefab;
        [SerializeField] private WeeklyMilestone grandMilestonePrefab;
        [SerializeField] private WeeklyMilestoneTip tipPrefab;
        [SerializeField] private float tipTime = 2.5f;

        [Header("Ngày và nhiệm vụ")]
        [SerializeField] private RectTransform rows;
        [SerializeField] private RectTransform tabs;
        [SerializeField] private WeeklyMissionRow rowPrefab;
        [SerializeField] private WeeklyDayTab tabPrefab;
        [SerializeField] private float tabStep = 107f;
        [Tooltip("Tab đang chọn to hơn bao nhiêu.")]
        [SerializeField] private float tabSelectedScale = 1.18f;
        [Tooltip("sk1.1 14.8 s: các hàng đổi từ trên xuống, khoảng 1-2 khung một hàng.")]
        [SerializeField] private float rowStagger = 0.04f;
        [SerializeField] private string lockedNotice = "UNLOCK AT DAY {0}!";
        [Tooltip("Chữ nổi \"Unlock at day N!\" (mẫu, tắt sẵn).")]
        [SerializeField] private TMP_Text noticeTemplate;

        [Header("Hình")]
        [SerializeField] private Sprite hatSprite;
        [SerializeField] private Sprite coinSprite;
        [Tooltip("Một đồng xu bay về ô xu Home.")]
        [SerializeField] private Sprite singleCoinSprite;
        [Tooltip("Xẻng, boomerang, undo, nam châm.")]
        [SerializeField] private Sprite[] boosterSprites = new Sprite[4];
        [SerializeField] private Sprite infiniteLivesSprite;
        [Tooltip("Đồng, xanh, tím, rồi giải lớn đỏ vàng [user: cái đầu là đồng].")]
        [SerializeField] private Sprite[] chestSprites = new Sprite[4];
        [SerializeField] private GoalIcon[] goalIcons = new GoalIcon[0];

        [Header("Nhịp bay")]
        [Tooltip("Mũ và tim bay [user: 0.35 s hơi nhanh].")]
        [SerializeField] private float flightTime = 0.55f;
        [SerializeField] private float coinFlightTime = 0.5f;

        private WeeklyMissions _missions;
        private int _day;
        private float _coverAlpha;
        private bool _busy;
        private float _tick;
        private readonly Dictionary<int, WeeklyMissionRow> _rowOf = new Dictionary<int, WeeklyMissionRow>();
        private readonly List<WeeklyMilestone> _markers = new List<WeeklyMilestone>();
        private RectTransform _tip;
        private Coroutine _tipTimer;

        protected override void OnInit()
        {
            _missions = WeeklyMissions.Shared;
            _day = _missions.Day;
            _coverAlpha = cover.color.a;
            cancelButton.Clicked += () => { if (!_busy) Close(); };
            infoButton.Clicked += () => Services.Get<UIManager>().Show<WeeklyMissionsRules>();
            _missions.Changed += Refresh;
        }

        protected override void OnShow()
        {
            ShowPoints(_missions.Points);
            BuildMarkers();
            BuildTabs();
            BuildRows(null, 0.55f);
            StartCoroutine(PopIn());
        }

        private void OnDestroy() { if (_missions != null) _missions.Changed -= Refresh; }

        /// <summary>Khoá Hermite tiếp tuyến phẳng (mọi độ dốc của Weekly Missions_Opening là 0): smoothstep giữa các khoá.</summary>
        private static float Keys(float t, params float[] timeValue)
        {
            if (t <= timeValue[0]) return timeValue[1];
            for (int i = 2; i < timeValue.Length; i += 2)
                if (t <= timeValue[i])
                {
                    float u = (t - timeValue[i - 2]) / (timeValue[i] - timeValue[i - 2]);
                    return Mathf.Lerp(timeValue[i - 1], timeValue[i + 1], u * u * (3f - 2f * u));
                }
            return timeValue[timeValue.Length - 1];
        }

        /// <summary>
        /// Weekly Missions_Opening.anim (0.83 s): nền tối dần (0.15 s), bảng tiêu đề giãn ngang (0.08-0.28 s) trong lúc chữ nảy
        /// (1.22 x 1.31 ở 0.13 s), thân trượt xuống từ dưới tiêu đề (1441 về 0, 0.22-0.53 s) và mask nảy (cao 1.075 ở 0.5 s), rồi
        /// tab ngày mọc xuống (0.53-0.7 s).
        /// </summary>
        private IEnumerator PopIn()
        {
            _busy = true;
            var bodyRest = body.anchoredPosition;
            var tabsScale = tabs.localScale;
            Action<float> step = p =>
            {
                float t = p * 0.8333f;
                cover.color = new Color(0f, 0f, 0f, Keys(t, 0f, 0f, 0.15f, _coverAlpha));
                header.localScale = new Vector3(Keys(t, 0f, 0f, 0.0833f, 0f, 0.2833f, 1f), Keys(t, 0f, 0f, 0.0833f, 1f), 1f);
                headerText.localScale = new Vector3(Keys(t, 0.0333f, 1f, 0.1333f, 1.225f, 0.3667f, 1f), Keys(t, 0.0333f, 1f, 0.1333f, 1.3083f, 0.3667f, 1f), 1f);
                body.anchoredPosition = bodyRest + new Vector2(0f, Keys(t, 0.2167f, 1441f, 0.5333f, 0f));
                mask.localScale = new Vector3(1f, Keys(t, 0.4333f, 1f, 0.5f, 1.075f, 0.7f, 1f), 1f);
                tabs.localScale = new Vector3(tabsScale.x, tabsScale.y * Keys(t, 0.5333f, 0f, 0.7f, 1f), 1f);
            };
            step(0f); // ẩn ngay khung đầu (tween lồng chỉ chạy từ khung sau)
            yield return Tween.Run(0.8333f, Tween.Linear, step, true);
            _busy = false;
        }

        /// <summary>Kim đồng hồ quay một phần tư vòng mỗi giây, mỗi nấc khoảng 0.2 s (video đo 0.7 s, trên máy thấy nhanh quá [user]).</summary>
        private void Update()
        {
            float now = Time.unscaledTime;
            int steps = Mathf.FloorToInt(now);
            clockHand.localRotation = Quaternion.Euler(0f, 0f, -90f * (steps + Tween.OutQuad(Mathf.Clamp01((now - steps) / 0.2f))));
            if (_missions == null || (_tick -= Time.unscaledDeltaTime) > 0f) return;
            _tick = 1f;
            var left = _missions.Left;
            timer.text = left.TotalDays >= 1 ? $"{(int)left.TotalDays}D {left.Hours}H" : left.TotalHours >= 1 ? $"{left.Hours}H {left.Minutes}M" : $"{left.Minutes}M {left.Seconds}S";
        }

        /// <summary>Mũ <paramref name="points"/> tới đâu trên thanh (0..1): thẳng giữa các mốc.</summary>
        private float FillAt(int points)
        {
            var milestones = WeeklyMissionsData.Milestones;
            float from = 0f; int fromPoints = 0;
            for (int i = 0; i < milestones.Length; i++)
            {
                if (points < milestones[i].points)
                    return Mathf.Lerp(from, markerAt[i], (points - fromPoints) / (float)(milestones[i].points - fromPoints));
                from = markerAt[i]; fromPoints = milestones[i].points;
            }
            return 1f;
        }

        private void ShowPoints(int points)
        {
            hats.text = points.ToString();
            fill.anchorMax = new Vector2(FillAt(points), 1f);
        }

        private void Refresh()
        {
            if (_busy) return;
            if (_tip != null) Destroy(_tip.gameObject);
            ShowPoints(_missions.Points);
            BuildMarkers();
            BuildTabs();
            BuildRows(null, -1f);
        }

        // ------------------------------------------------------------------ mốc và bóng nói

        private void BuildMarkers()
        {
            foreach (var marker in _markers) Destroy(marker.gameObject);
            _markers.Clear();
            var milestones = WeeklyMissionsData.Milestones;
            for (int i = 0; i < milestones.Length; i++)
            {
                bool grand = i == milestones.Length - 1;
                var marker = Instantiate(grand ? grandMilestonePrefab : milestonePrefab, markers);
                var rect = (RectTransform)marker.transform;
                rect.anchorMin = rect.anchorMax = new Vector2(markerAt[i], 0.5f);
                marker.Show(ChestSprite(i), i < _missions.MilestonesReached);
                int index = i;
                marker.Clicked += () => ShowTip(index, rect, grand);
                _markers.Add(marker);
            }
        }

        private void ShowTip(int milestone, RectTransform marker, bool grand)
        {
            if (_tip != null) Destroy(_tip.gameObject);
            if (_tipTimer != null) StopCoroutine(_tipTimer);
            var tip = Instantiate(tipPrefab, marker);
            tip.Show(WeeklyMissionsData.Milestones[milestone].points, Extras(WeeklyMissionsData.Milestones[milestone].reward, "x"), grand);
            _tip = (RectTransform)tip.transform;
            _tip.anchoredPosition = Vector2.zero;
            _tip.SetParent(transform, true); // nằm trên tiêu đề, như trên máy
            var rect = _tip;
            rect.localScale = Vector3.zero;
            StartCoroutine(Tween.Run(0.2f, Tween.OutBack(1.7f), t => { if (rect != null) rect.localScale = Vector3.one * t; }, true));
            _tipTimer = StartCoroutine(HideTip(rect));
        }

        private IEnumerator HideTip(RectTransform tip)
        {
            yield return new WaitForSecondsRealtime(tipTime);
            if (tip == null) yield break;
            yield return Tween.Run(0.15f, Tween.Linear, t => { if (tip != null) tip.localScale = Vector3.one * (1f - t); }, true);
            if (tip != null) Destroy(tip.gameObject);
        }

        public Sprite ChestSprite(int milestone) => chestSprites[Mathf.Clamp(milestone, 0, chestSprites.Length - 1)];

        // ------------------------------------------------------------------ ngày và nhiệm vụ

        private void BuildTabs(bool grow = false)
        {
            for (int i = tabs.childCount - 1; i >= 0; i--) Destroy(tabs.GetChild(i).gameObject);
            var rest = ((RectTransform)tabPrefab.transform).anchoredPosition;
            var restScale = tabPrefab.transform.localScale;
            for (int day = 1; day <= WeeklyMissions.WeekDays; day++)
            {
                bool selected = day == _day;
                var tab = Instantiate(tabPrefab, tabs);
                tab.name = "WeeklyMissionDayItem " + day;
                var rect = (RectTransform)tab.transform;
                rect.anchoredPosition = rest + new Vector2((day - 4) * tabStep, 0f);
                if (selected)
                {
                    // Tab đang chọn to hơn và thõng xuống; tab vừa chọn lớn dần lên (khoảng 0.13 s, sk1.1 14.8 s).
                    var to = restScale * tabSelectedScale;
                    rect.localScale = grow ? restScale : to;
                    if (grow) StartCoroutine(Tween.Run(0.13f, Tween.OutQuad, t => { if (rect != null) rect.localScale = Vector3.Lerp(restScale, to, t); }, true));
                }
                tab.Show(day, selected, _missions.DayOpen(day), _missions.DayHasClaimable(day));
                int chosen = day;
                tab.Clicked += () =>
                {
                    if (_busy || chosen == _day) return;
                    _day = chosen;
                    BuildTabs(true);
                    BuildRows(null, 0f);
                };
            }
        }

        /// <summary>Nhiệm vụ của ngày: nhận được trước, đang làm, đã nhận sau cùng; ngày chưa mở thì đang làm trước.</summary>
        private List<int> Order()
        {
            // Ngày chưa mở: nhiệm vụ đã xong chưa nhận được nên nằm dưới các nhiệm vụ đang làm (sk1 26 s, sk1.1 ngày 5) [user].
            bool open = _missions.DayOpen(_day);
            var order = new List<int>();
            int count = WeeklyMissionsData.Days[_day - 1].Length;
            for (int pass = 0; pass < 3; pass++)
                for (int i = 0; i < count; i++)
                {
                    int state = _missions.Claimable(_day, i) ? (open ? 0 : 1) : _missions.Claimed(_day, i) ? 2 : (open ? 1 : 0);
                    if (state == pass) order.Add(i);
                }
            return order;
        }

        private float RowY(int slot)
        {
            var template = (RectTransform)rowPrefab.transform;
            return template.anchoredPosition.y - slot * template.rect.height;
        }

        /// <summary>
        /// Các hàng của ngày đang chọn. Không có <paramref name="from"/>: hàng hiện lần lượt từ trên xuống và dấu tích bật sau
        /// (mở popup: sau khi thân xuống, <paramref name="delay"/>; delay âm: hiện ngay, sau khi nhận xong); có <paramref name="from"/>
        /// (vừa nhận): mỗi hàng trượt từ chỗ cũ về chỗ mới.
        /// </summary>
        private void BuildRows(Dictionary<int, float> from, float delay)
        {
            // Đổi ngày: hàng cũ ở lại tới khi hàng mới thế chỗ từng cái một (sk1.1 14.8 s).
            bool stagger = from == null && delay >= 0f;
            var old = new List<GameObject>();
            for (int i = 0; i < rows.childCount; i++) old.Add(rows.GetChild(i).gameObject);
            if (!stagger) foreach (var row in old) Destroy(row);
            _rowOf.Clear();
            var order = Order();
            for (int slot = 0; slot < order.Count; slot++)
            {
                var row = Row(order[slot], slot);
                _rowOf[order[slot]] = row;
                var rect = (RectTransform)row.transform;
                if (from != null && from.TryGetValue(order[slot], out float oldY)) StartCoroutine(Slide(rect, oldY, RowY(slot)));
                else if (stagger) StartCoroutine(Appear(row, delay + slot * rowStagger, slot < old.Count ? old[slot] : null));
            }
            if (stagger) for (int i = order.Count; i < old.Count; i++) Destroy(old[i]);
        }

        private static IEnumerator Slide(RectTransform row, float fromY, float toY)
        {
            var x = row.anchoredPosition.x;
            yield return Tween.Run(0.15f, Tween.OutQuad, t => { if (row != null) row.anchoredPosition = new Vector2(x, Mathf.Lerp(fromY, toY, t)); }, true);
        }

        /// <summary>Hàng thế chỗ hàng cũ với một cú nảy nhỏ; dấu tích của hàng đã nhận bật ra sau một chút (sk1.1 3.4 s, 14.0 s, 14.8 s).</summary>
        private static IEnumerator Appear(WeeklyMissionRow row, float delay, GameObject replaces)
        {
            var rect = (RectTransform)row.transform;
            var group = row.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            var tick = row.Tick;
            var tickScale = tick != null ? tick.localScale : Vector3.one;
            if (tick != null) tick.localScale = Vector3.zero;
            for (float t = 0f; t < delay; t += Time.unscaledDeltaTime) yield return null;
            if (replaces != null) Destroy(replaces);
            if (row == null) yield break;
            group.alpha = 1f;
            var scale = rect.localScale;
            yield return Tween.Run(0.12f, Tween.OutBack(2f), t => { if (rect != null) rect.localScale = scale * Mathf.LerpUnclamped(0.96f, 1f, t); }, true);
            if (tick == null) yield break;
            yield return Tween.Run(0.15f, Tween.OutBack(2f), t => { if (tick != null) tick.localScale = tickScale * t; }, true);
        }

        private WeeklyMissionRow Row(int index, int slot)
        {
            var mission = WeeklyMissions.MissionAt(_day, index);
            var row = Instantiate(rowPrefab, rows);
            row.name = "Mission " + index;
            var rect = (RectTransform)row.transform;
            rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, RowY(slot));
            var items = new List<(Sprite icon, string amount)> { (hatSprite, mission.reward.points.ToString()) };
            items.AddRange(Extras(mission.reward, "x"));
            bool claimable = _missions.Claimable(_day, index);
            row.Show(mission.Text.ToUpperInvariant(), GoalSprite(mission.goal), mission.goal == MissionGoal.CompletePurchase,
                claimable, _missions.Claimed(_day, index), _missions.DayOpen(_day), _missions.Progress(_day, index), mission.amount, items);
            if (claimable) row.Clicked += () => OnClaim(index, row);
            return row;
        }

        private Sprite GoalSprite(MissionGoal goal)
        {
            foreach (var entry in goalIcons) if (entry.goal == goal) return entry.icon;
            return null;
        }

        /// <summary>Xu, booster ("x1") và tim vô hạn ("15M", "2H") của một phần thưởng, kèm hình.</summary>
        public List<(Sprite icon, string amount)> Extras(MissionReward reward, string times)
        {
            var list = new List<(Sprite, string)>();
            if (reward.coins > 0) list.Add((coinSprite, reward.coins.ToString()));
            var boosters = reward.Boosters;
            for (int b = 0; b < boosters.Length; b++) if (boosters[b] > 0) list.Add((boosterSprites[b], times + boosters[b]));
            if (reward.infiniteMinutes > 0) list.Add((infiniteLivesSprite, reward.infiniteMinutes % 60 == 0 ? reward.infiniteMinutes / 60 + "H" : reward.infiniteMinutes + "M"));
            return list;
        }

        // ------------------------------------------------------------------ nhận thưởng

        private void OnClaim(int index, WeeklyMissionRow row)
        {
            if (_busy) return;
            if (!_missions.DayOpen(_day)) { StartCoroutine(Notice((RectTransform)row.transform, string.Format(lockedNotice, _day))); return; }
            _busy = true;
            int before = _missions.Points;
            var reward = WeeklyMissions.MissionAt(_day, index).reward;

            // Thứ bay đi: bản sao các ô phần thưởng của hàng, đúng chỗ hiện tại.
            var flying = new List<(RectTransform item, string kind)>();
            var extras = Extras(reward, "x");
            for (int i = 0; i < 2; i++)
            {
                var item = row.Item(i);
                if (item == null) continue;
                string kind = i == 0 ? "hat" : reward.coins > 0 ? "coins" : reward.infiniteMinutes > 0 && extras.Count == 1 ? "lives" : "booster";
                string label = i == 0 ? "+" + reward.points : kind == "coins" ? "+" + reward.coins : extras[0].amount.ToUpperInvariant();
                var copy = (RectTransform)Instantiate(item, transform);
                copy.position = item.position;
                copy.localScale = Vector3.one * item.lossyScale.x / transform.lossyScale.x;
                copy.GetComponentInChildren<TMP_Text>().text = label;
                flying.Add((copy, kind));
            }

            var oldY = new Dictionary<int, float>();
            foreach (var pair in _rowOf) oldY[pair.Key] = ((RectTransform)pair.Value.transform).anchoredPosition.y;
            var reached = _missions.Claim(_day, index);
            SnackDash.Audio.SoundPlayer.Play("CollectCoin");
            SnackDash.Audio.Haptics.Light();
            BuildRows(oldY, 0f); // các hàng trượt về thứ tự mới ngay (sk1.1 22.7 s)
            BuildTabs();
            StartCoroutine(ClaimFlow(flying, before, before + reward.points, reached));
        }

        /// <summary>
        /// "Unlock at Day N!" (sk1 26.8-27.3 s): chữ trắng viền tối sinh thấp trên hàng vừa chạm, bay thẳng lên quá mép trên trong
        /// 0.6 s và mờ dần cuối; mỗi lần chạm thêm một chữ [user].
        /// </summary>
        private IEnumerator Notice(RectTransform row, string message)
        {
            var label = Instantiate(noticeTemplate, transform);
            label.gameObject.SetActive(true);
            label.text = message;
            float h = row.rect.height;
            var start = row.TransformPoint(new Vector3(-56f, -0.3f * h, 0f));
            var end = row.TransformPoint(new Vector3(-44f, 0.95f * h, 0f)); // cao hơn mép trên một chút [user]
            label.rectTransform.position = start;
            var scale = label.rectTransform.localScale;
            yield return Tween.Run(0.6f, Tween.Linear, t =>
            {
                if (label == null) return;
                label.rectTransform.position = Vector3.Lerp(start, end, Tween.OutQuad(t));
                label.rectTransform.localScale = scale * Mathf.Lerp(1.25f, 1f, Mathf.Clamp01(t / 0.18f));
                label.alpha = t < 0.7f ? 1f : 1f - (t - 0.7f) / 0.3f;
            }, true);
            if (label != null) Destroy(label.gameObject);
        }

        /// <summary>
        /// sk1.1 22.7-24.0 s: phần thưởng phóng to tại chỗ kèm "+N" (1.5 lần trong 0.12 s) và nhích lên nửa giây; rồi mũ bay về thanh
        /// (đếm lên và đầy dần), đống xu vỡ thành từng đồng bay về ô xu Home, tim về ô tim; booster bay lên và mờ. Sau đó tới rương.
        /// </summary>
        private IEnumerator ClaimFlow(List<(RectTransform item, string kind)> flying, int from, int to, List<int> reached)
        {
            var starts = new Vector3[flying.Count];
            var scales = new Vector3[flying.Count];
            for (int i = 0; i < flying.Count; i++) { starts[i] = flying[i].item.localPosition; scales[i] = flying[i].item.localScale; }
            yield return Tween.Run(0.12f, Tween.OutBack(2f), t =>
            {
                for (int i = 0; i < flying.Count; i++) flying[i].item.localScale = scales[i] * Mathf.LerpUnclamped(1f, 1.5f, t);
            }, true);
            yield return Tween.Run(0.5f, Tween.OutQuad, t =>
            {
                for (int i = 0; i < flying.Count; i++) flying[i].item.localPosition = starts[i] + Vector3.up * 25f * t;
            }, true);

            var home = SnackDash.Core.Services.Get<HomeTopBar>(); // HomeTopBar tự đăng ký khi Home mở
            int pending = 0;
            foreach (var (item, kind) in flying)
            {
                pending++;
                switch (kind)
                {
                    case "hat": StartCoroutine(FlyTo(item, hatCounter, () => { pending--; StartCoroutine(CountHats(from, to)); })); break;
                    case "coins": StartCoroutine(CoinBurst(item, home != null ? home.CoinIcon : null, () => pending--)); break;
                    case "lives": StartCoroutine(FlyTo(item, home != null ? home.LivesIcon : null, () => pending--)); break;
                    default: StartCoroutine(FlyTo(item, null, () => pending--)); break;
                }
            }
            while (pending > 0) yield return null;
            yield return Tween.Run(0.4f, Tween.Linear, _ => { }, true); // thanh đếm xong

            foreach (int milestone in reached)
            {
                bool done = false;
                var marker = _markers.Count > milestone ? (RectTransform)_markers[milestone].transform : hatCounter;
                var chestPopup = Services.Get<UIManager>().Show<WeeklyChestPopup>();
                if (chestPopup != null) chestPopup.Play(ChestSprite(milestone), Extras(WeeklyMissionsData.Milestones[milestone].reward, "x"), marker.position, () => done = true);
                else done = true;
                while (!done) yield return null;
            }
            _busy = false;
            Refresh();
        }

        /// <summary>Bay vòng cung tới <paramref name="target"/> và thu nhỏ vào (hoặc bay lên và biến mất khi không có đích).</summary>
        private IEnumerator FlyTo(RectTransform item, Transform target, Action arrived)
        {
            float unit = transform.lossyScale.y;
            Vector3 start = item.position, end = target != null ? target.position : start + Vector3.up * 300f * unit;
            var scale = item.localScale;
            var group = item.gameObject.AddComponent<CanvasGroup>();
            yield return Tween.Run(flightTime, Tween.Linear, t =>
            {
                float e = Tween.InOutCubic(t);
                item.position = Vector3.Lerp(start, end, e) + Vector3.up * Mathf.Sin(e * Mathf.PI) * 80f * unit;
                item.localScale = scale * Mathf.Lerp(1f, 0.5f, e);
                if (target == null) group.alpha = 1f - e;
            }, true);
            Destroy(item.gameObject);
            if (target != null) StartCoroutine(Tween.Punch(0.08f, s => { if (target != null) target.localScale = Vector3.one * (1f + 0.15f * s); }));
            arrived();
        }

        /// <summary>Đống xu vỡ thành từng đồng bay lần lượt về ô xu (sk1.1 23.4 s).</summary>
        private IEnumerator CoinBurst(RectTransform pile, Transform target, Action arrived)
        {
            const int count = 6;
            Vector3 centre = pile.position;
            float unit = transform.lossyScale.y;
            Destroy(pile.gameObject);
            int left = count;
            for (int i = 0; i < count; i++)
            {
                var coin = UiBuilder.Rect(transform, "Flying Coin", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(60f, 60f));
                UiBuilder.Image(coin, singleCoinSprite, Color.white).preserveAspect = true;
                coin.position = centre;
                var spread = centre + (Vector3)(UnityEngine.Random.insideUnitCircle * 70f * unit);
                StartCoroutine(Coin(coin, spread, target, i * 0.05f, () => left--));
            }
            while (left > 0) yield return null;
            if (target != null) StartCoroutine(Tween.Punch(0.08f, s => { if (target != null) target.localScale = Vector3.one * (1f + 0.15f * s); }));
            arrived();
        }

        private IEnumerator Coin(RectTransform coin, Vector3 spread, Transform target, float delay, Action done)
        {
            Vector3 start = coin.position;
            yield return Tween.Run(0.16f, Tween.OutQuad, t => coin.position = Vector3.Lerp(start, spread, t), true);
            for (float t = 0f; t < delay; t += Time.unscaledDeltaTime) yield return null;
            Vector3 end = target != null ? target.position : spread + Vector3.up * 400f * transform.lossyScale.y;
            yield return Tween.Run(coinFlightTime, Tween.Linear, t =>
            {
                float e = Tween.InOutCubic(t);
                coin.position = Vector3.Lerp(spread, end, e);
                coin.localScale = Vector3.one * Mathf.Lerp(1f, 0.7f, e);
            }, true);
            Destroy(coin.gameObject);
            done();
        }

        private IEnumerator CountHats(int from, int to)
        {
            SnackDash.Audio.Haptics.Light();
            yield return Tween.Run(0.4f, Tween.OutQuad, t => ShowPoints(Mathf.RoundToInt(Mathf.Lerp(from, to, t))), true);
        }
    }
}

using System.Collections;
using System.Collections.Generic;
using SnackDash.Economy;
using SnackDash.Presentation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static SnackDash.UI.UiBuilder;
using static SnackDash.UI.JungleHeistUi;

namespace SnackDash.UI
{
    /// <summary>
    /// JUNGLE HEIST's main screen (original Resources/Popup_Lava_Quest_Main): the jungle river (green-to-teal water,
    /// banks, shadows, waterfall, the treasure boat with its "Grand Prize 10 000" sign), seven lily pads up the river and the
    /// boat at the top; the panel over it with the title, the two counters (Levels "n/7", Players "n/100"), the hint and
    /// the timer. The players stand on the pad of the step reached: after each win they jump to the next pad, those who
    /// drop out fall into the water and sink after 2 s (LavaQuestMapView._sinkDelay), the counters run up in 1.3 s
    /// (LavaQuestPopupView._counterAnimationDuration); seven wins take you onto the boat; a lost level drops you in.
    /// The original draws the players as rigged snakes through an overlay camera; here they are their avatars.
    /// Laid out on a 1920 high canvas as the original and scaled to the screen's height. Prefab Prefabs/UI/JungleHeistMain [user].
    /// </summary>
    public class JungleHeistMain : MonoBehaviour
    {
        private const float Width = 1080f, Height = 1920f;

        // View_LavaQuest_Lilypad_0..6: position on the map, lily pad sprite and size, where players stand, where they fall.
        private static readonly (Vector2 at, string sprite, Vector2 size, Vector2 stand, Vector2[] water)[] Pads =
        {
            (new Vector2(28f, -301.25f), "S_Lilypad_1_0", new Vector2(286f, 173f), new Vector2(3.2f, 5.9f), new[] { new Vector2(-214.7f, -83.5f), new Vector2(-186.4f, -129.9f), new Vector2(-92f, -176f) }),
            (new Vector2(-238.2f, -205f), "S_Lilypad_2_0", new Vector2(261f, 161f), new Vector2(-10.8f, 10.3f), new[] { new Vector2(-198.7f, 4.9f), new Vector2(-23.5f, -120.5f), new Vector2(150f, -209f) }),
            (new Vector2(-204f, -42f), "S_Lilypad_3_0", new Vector2(224f, 136f), new Vector2(1.4f, -1.6f), new[] { new Vector2(-183.1f, -30f), new Vector2(178.7f, -43.4f), new Vector2(-263f, -146f) }),
            (new Vector2(-11.9f, 83.3f), "S_Lilypad_4_0", new Vector2(224f, 136f), new Vector2(6.2f, 6f), new[] { new Vector2(3.9f, -116f), new Vector2(164.9f, -75f), new Vector2(0f, -214f) }),
            (new Vector2(214.4f, 157.9f), "S_Lilypad_5_0", new Vector2(207f, 122f), new Vector2(0.5f, 7.2f), new[] { new Vector2(-182.6f, 36f), new Vector2(165.3f, 12.3f), new Vector2(-52f, -113.7f) }),
            (new Vector2(251f, 293f), "S_Lilypad_6_0", new Vector2(184f, 106f), new Vector2(-2.9f, 7.9f), new[] { new Vector2(160f, -64.7f), new Vector2(-182.6f, -54f), new Vector2(151f, -153f) }),
            (new Vector2(8f, 360f), "S_Lilypad_7_0", new Vector2(285f, 138f), new Vector2(-0.8f, 14.1f), new[] { new Vector2(-195.3f, -99f), new Vector2(18.9f, -136.6f), new Vector2(-124f, -174f) }),
        };
        private static readonly string[] PadEdges = { "Tex_FX_Lilypad_WaterEdge_0", "Tex_FX_Lilypad_WaterEdge_1", "Tex_FX_Lilypad_WaterEdge_2", "Tex_FX_Lilypad_WaterEdge_3", "Tex_FX_Lilypad_WaterEdge_4", "Tex_FX_Lilypad_WaterEdge_5", "Tex_FX_Lilypad_WaterEdge_5" };
        private static readonly Vector2 BoatAt = new Vector2(0f, 694f);

        /// <summary>How many other players are drawn on a pad at most (the rest are only counted).</summary>
        private const int ShownBots = 5;
        public const float SinkDelay = 2f, CounterTime = 1.3f, JumpTime = 0.5f;

        private UiSkin _skin;
        private JungleHeist _heist;
        private RectTransform _root, _fit, _map, _crowd;
        private TextMeshProUGUI _levels, _players, _hint, _timer;
        private Transform _hand;
        private GameObject _rulesPrefab, _endedPrefab, _rewardsPrefab;
        private RectTransform _player;
        private readonly List<RectTransform> _bots = new List<RectTransform>();
        private bool _busy;
        private float _tick;

        public static JungleHeistMain Open(RectTransform canvas, UiSkin skin, GameObject prefab, GameObject rulesPrefab, GameObject endedPrefab, GameObject rewardsPrefab)
        {
            RectTransform root;
            if (prefab != null)
            {
                root = (RectTransform)Instantiate(prefab, canvas).transform;
                root.name = "Popup_Lava_Quest_Main";
            }
            else
            {
                root = Stretch(canvas, "Popup_Lava_Quest_Main");
                BuildLayout(root, skin);
            }
            var view = root.GetComponent<JungleHeistMain>();
            if (view == null) view = root.gameObject.AddComponent<JungleHeistMain>();
            view._skin = skin;
            view._heist = JungleHeist.Shared;
            view._rulesPrefab = rulesPrefab;
            view._endedPrefab = endedPrefab;
            view._rewardsPrefab = rewardsPrefab;
            view.Bind(root);
            return view;
        }

        // ================================================================== layout (saved as the prefab)

        public static void BuildLayout(RectTransform root, UiSkin skin)
        {
            if (root.GetComponent<JungleHeistMain>() == null) root.gameObject.AddComponent<JungleHeistMain>();
            var fit = Centre(root, "Fit", 0f, 0f, Width, Height);
            Image(Stretch(root, "Blocker"), null, Color.clear, raycast: true).transform.SetAsFirstSibling();

            // Background: the water (UIGradient green 0.41/0.75/0.43 to teal 0.11/0.25/0.29) and View_LavaQuest_Background.
            var water = Image(Stretch(fit, "Background"), null, Color.white);
            UiGradient.Add(water, new Color(0.41f, 0.75f, 0.43f), new Color(0.11f, 0.25f, 0.29f));
            var scene = Rect(fit, "View_LavaQuest_Background", Vector2.zero, Vector2.one, new Vector2(-26f, 0f), Vector2.zero);
            var edgeColour = new Color(0.753f, 0.937f, 0.71f);
            Layer(scene, skin, "Wateredge_1", "Tex_FX_Environment_WaterEdge_0", new Vector2(-512f, -493f), new Vector2(234.5f, 1160.5f), new Vector3(1f, 0.8875f, 1f), -24.3f, edgeColour);
            Layer(scene, skin, "Wateredge_2", "Tex_FX_Environment_WaterEdge_1", new Vector2(552f, -413f), new Vector2(133f, 564f), Vector3.one * 1.318f, 5f, edgeColour);
            Layer(scene, skin, "Wateredge_3", "Tex_FX_Environment_WaterEdge_2", new Vector2(58f, 65f), new Vector2(226.5f, 1058.3f), Vector3.one, -90f, edgeColour);
            var shade = new Color(0.061f, 0.226f, 0.223f, 0.616f);
            Layer(scene, skin, "Shadow_1", "Tex_FX_Environment_Shadows_1", new Vector2(-52.6f, 54f), new Vector2(234.4f, 858.3f), Vector3.one, 90f, shade);
            Layer(scene, skin, "Shadow_2", "Tex_FX_Environment_Shadows_2", new Vector2(299.8f, -628.3f), new Vector2(523.2f, 663.5f), Vector3.one, 0f, shade);
            Layer(scene, skin, "Shadow_3", "Tex_FX_Environment_Shadows_0", new Vector2(514f, -140f), new Vector2(209.6f, 425.1f), Vector3.one, 0f, shade);
            // Foreground: the 2048 square picture kept square on the full height (AspectRatioFitter height-led, preserve aspect), the sides cut.
            var foreground = Rect(scene, "Foreground", new Vector2(0.5f, 0f), new Vector2(0.5f, 1f), Vector2.zero, Vector2.zero);
            Image(foreground, skin.Get("Tex_LavaQuest_Illustration_1_0"), Color.white).preserveAspect = true;
            var square = foreground.gameObject.AddComponent<AspectRatioFitter>();
            square.aspectMode = AspectRatioFitter.AspectMode.HeightControlsWidth;
            square.aspectRatio = 1f;
            var fall = Centre(scene, "FX_Waterfall", -342.77f, -130.8f, 150f, 300f);
            var foam = new Color(0.824f, 0.906f, 0.825f);
            Layer(fall, skin, "WaterOverlay_1", "S_Waterfall_3", new Vector2(-24.4f, 31f), new Vector2(76.1f, 270.4f), Vector3.one, 0f, foam);
            Layer(fall, skin, "WaterOverlay_2", "S_Waterfall_1", new Vector2(-90.6f, 167.8f), new Vector2(202.1f, 61.2f), Vector3.one, -37.3f, foam);
            Layer(fall, skin, "WaterOverlay_3", "S_Waterfall_2", new Vector2(-330.4f, 344.5f), new Vector2(383.1f, 74.1f), Vector3.one, -28.8f, foam);
            var boat = Centre(scene, "FX_Boat", 27.7f, 176f, 100f, 100f);
            var sign = Centre(boat, "Sign_Bg", 45.7f, -28.2f, 195.1f, 123.3f);
            Image(sign, skin.Get("S_Sign_Bg_0"), Color.white);
            var signText = Text(Centre(sign, "Text (TMP)", 7.7f, -7.5f, 173.2f, 76f), "Grand Prize\n<size=140%>10 000</size>", skin.font, skin.EventMaterial("LuckiestGuy-Regular LavaQuest Counter"), 28.8f, Color.white);
            signText.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 5.8f);

            // View_LavaQuest_Map: 890 wide, from the bottom up to 842 under the top; seven pads and the boat.
            var map = Rect(fit, "View_LavaQuest_Map", new Vector2(0.5f, 0f), new Vector2(0.5f, 1f), new Vector2(0f, -421f), new Vector2(890f, -842f));
            for (int i = 0; i < Pads.Length; i++)
            {
                var pad = Centre(map, "View_LavaQuest_Lilypad_" + i, Pads[i].at.x, Pads[i].at.y, 300f, 200f);
                var edge = Image(Centre(pad, "Wateredge", 3f, -8f, 348f * 0.82f, 230.4f * 0.7f), skin.Get(PadEdges[i]), edgeColour);
                edge.raycastTarget = false;
                Image(Centre(pad, "Lilypad", 0f, 0f, Pads[i].size.x, Pads[i].size.y), skin.Get(Pads[i].sprite), Color.white);
            }
            Centre(map, "Boat", BoatAt.x, BoatAt.y, 611.8f, 120f);
            Stretch(map, "Crowd");

            // Overlay (gradient01, turned over) darkening the top behind the panel.
            // It ends where the map begins (842 under the top): on screens taller than 1920 it must not reach further down.
            var shadeTop = Rect(fit, "Overlay", new Vector2(0f, 1f), Vector2.one, new Vector2(0f, -421f), new Vector2(1150f, 842f));
            shadeTop.localRotation = Quaternion.Euler(0f, 0f, 180f);
            Image(shadeTop, skin.bottomGradient, Color.black); // gradient01
            // Foreground_Mask (front leaves) left out: it is fixed to the middle while the picture stretches, so it slips on taller screens.

            // Popup_Container: the panel at the top (880.6 x 380.7, 368 down, 0.9).
            var panel = Rect(fit, "Popup_Container", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -368f), new Vector2(880.6f, 380.7f));
            panel.localScale = Vector3.one * 0.9f;
            Leaves(panel, skin, 113f);
            Image(Stretch(panel, "Background"), skin.Get("S_Lava_Quest_Bg_0"), Color.white, sliced: true).pixelsPerUnitMultiplier = 0.6f;
            var content = Rect(panel, "Content", Vector2.zero, Vector2.one, new Vector2(0f, 12f), new Vector2(-56f, -80f));
            // Content (vertical layout: padding 22/22/73/30): the hint, then Counters (150 high, padding 37/37/12, 32 apart).
            var hint = Text(Rect(content, "Description (TMP)", new Vector2(0f, 1f), Vector2.one, new Vector2(0f, -73f - 24f), new Vector2(-44f, 48f)), "Win 7 levels in a row to complete the challenge!", skin.font, skin.plainMaterial, 32.4f, new Color(0.455f, 0.263f, 0.082f));
            hint.enableAutoSizing = true;
            hint.fontSizeMin = 20f;
            hint.fontSizeMax = 32.4f;
            float counterWidth = (824.6f - 44f - 74f - 32f) / 2f;
            Counter(content, skin, "Levels_Counter", "Levels", "0/7", new Vector2(22f + 37f + counterWidth / 2f, 30f + 69f), counterWidth);
            Counter(content, skin, "Players_Counter", "Players", "100/100", new Vector2(22f + 37f + counterWidth * 1.5f + 32f, 30f + 69f), counterWidth);
            Timer(panel, skin, new Vector2(19.5f, -190.35f + 25.4f), 1.25f, out _);
            Title(panel, skin, new Vector2(0f, 191.3f), new Vector2(617.7f, 187.4f));
            var close = Rect(panel, "Close_Button", Vector2.one, Vector2.one, new Vector2(-19.3f, -17.7f), new Vector2(86f, 86f));
            Image(close, skin.Get("S_Icon_Cancel"), Color.white, raycast: true);
            var info = Rect(panel, "Info_Button", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(43f, -27.4f), new Vector2(60f, 60f));
            Image(info, skin.Get("S_Icon_Info"), Color.white, raycast: true);
            CentreOnLetters(root);
        }

        private static void Layer(RectTransform parent, UiSkin skin, string name, string sprite, Vector2 at, Vector2 size, Vector3 scale, float angle, Color colour)
        {
            var rect = Centre(parent, name, at.x, at.y, size.x, size.y);
            rect.localScale = scale;
            rect.localRotation = Quaternion.Euler(0f, 0f, angle);
            Image(rect, skin.Get(sprite), colour);
        }

        /// <summary>A counter (S_Bg_Counter_Lava_Quest): its name on top, "n/N" under it (LavaQuest Counter, 51 and 68).</summary>
        private static void Counter(RectTransform content, UiSkin skin, string name, string label, string value, Vector2 at, float width)
        {
            var counter = Rect(content, name, Vector2.zero, Vector2.zero, at, new Vector2(width, 138f));
            Image(counter, skin.Get("S_Bg_Counter_Lava_Quest_0"), Color.white, sliced: true);
            var counterMaterial = skin.EventMaterial("LuckiestGuy-Regular LavaQuest Counter");
            Text(Rect(counter, label + " (TMP)", new Vector2(0f, 1f), Vector2.one, new Vector2(0f, -37.2f), new Vector2(0f, 74.4f)), label, skin.font, counterMaterial, 51f, Color.white);
            Text(Rect(counter, "Counter (TMP)", Vector2.zero, new Vector2(1f, 0f), new Vector2(0f, 34f), new Vector2(0f, 68f)), value, skin.font, counterMaterial, 67.95f, Color.white);
        }

        // ================================================================== behaviour

        private void Bind(RectTransform root)
        {
            _root = root;
            _fit = (RectTransform)root.Find("Fit");
            _map = (RectTransform)_fit.Find("View_LavaQuest_Map");
            _crowd = (RectTransform)_map.Find("Crowd");
            var panel = _fit.Find("Popup_Container");
            _levels = panel.Find("Content/Levels_Counter/Counter (TMP)").GetComponent<TextMeshProUGUI>();
            _players = panel.Find("Content/Players_Counter/Counter (TMP)").GetComponent<TextMeshProUGUI>();
            _hint = panel.Find("Content/Description (TMP)").GetComponent<TextMeshProUGUI>();
            _timer = panel.Find("Timer_Banner/Text (TMP)").GetComponent<TextMeshProUGUI>();
            _hand = panel.Find("Timer_Banner/Timer_Clock/TimerArrow");
            AddClick(panel.Find("Close_Button"), Close);
            AddClick(panel.Find("Info_Button"), () => JungleHeistRules.Open(_root, _skin, _rulesPrefab));
            FitHeight();
            Pressable(root);
            StartCoroutine(Play());
        }

        private static void AddClick(Transform target, System.Action action)
        {
            if (target == null) return;
            var button = target.GetComponent<Button>();
            if (button == null) button = target.gameObject.AddComponent<Button>();
            button.onClick.AddListener(() => action());
        }

        private void FitHeight()
        {
            UnityEngine.Canvas.ForceUpdateCanvases();
            var size = _root.rect.size;
            if (size.x <= 0f) return;
            float k = size.y / Height; // the original is laid out on a 1920 high canvas (Home's scaler matches height: k is 1 there)
            _fit.localScale = new Vector3(k, k, 1f);
            _fit.sizeDelta = new Vector2(size.x / k, Height);
        }

        private Vector2 PadPoint(int step) => step >= JungleHeist.Steps ? BoatAt : Pads[step].at + Pads[step].stand;

        /// <summary>The player in the middle of the pad, the others around (a step apart), as many as are still on (up to five drawn).</summary>
        private Vector2 Spot(int step, int index)
        {
            var centre = PadPoint(step);
            if (index == 0) return centre + new Vector2(0f, 6f);
            float angle = (index - 1) * 72f * Mathf.Deg2Rad;
            return centre + new Vector2(Mathf.Cos(angle) * 70f, Mathf.Sin(angle) * 28f + 20f);
        }

        private void Place(int step, int survivors)
        {
            foreach (var bot in _bots) if (bot != null) Destroy(bot.gameObject);
            _bots.Clear();
            int shown = Mathf.Min(ShownBots, Mathf.Max(0, survivors - 1));
            for (int i = 0; i < shown; i++)
                _bots.Add(Avatar(_crowd, _skin, "Bot " + i, Spot(step, i + 1), 0.9f, i + 1, false));
            if (_player == null) _player = Avatar(_crowd, _skin, "Player", Spot(step, 0), 1.15f, 5, true);
            _player.anchoredPosition = Spot(step, 0);
            _player.SetAsLastSibling();
        }

        private void ShowCounters(int step, int survivors)
        {
            _levels.text = $"{step}/{JungleHeist.Steps}";
            _players.text = $"{survivors}/{JungleHeist.Participants}";
        }

        /// <summary>
        /// Opening: the run as it was last seen; then each new win in turn (jump, drop-outs fall and sink, counters run),
        /// or the lost run (the player falls in), and the end popup when the run is over.
        /// </summary>
        private IEnumerator Play()
        {
            _busy = true;
            int seen = Mathf.Min(_heist.SeenStep, _heist.Step);
            Place(seen, JungleHeist.SurvivorsPerStep[seen]);
            ShowCounters(seen, JungleHeist.SurvivorsPerStep[seen]);
            _hint.text = "Win 7 levels in a row to complete the challenge!";
            yield return new WaitForSecondsRealtime(0.4f);

            while (seen < _heist.Step)
            {
                yield return Advance(seen, seen + 1);
                seen++;
                _heist.SeenStep = seen;
            }
            if (_heist.Current == JungleHeist.State.Lost)
            {
                _hint.text = "You failed the challenge. Better luck next time!";
                yield return Fall(_player, Pads[Mathf.Min(seen, Pads.Length - 1)].at + Pads[Mathf.Min(seen, Pads.Length - 1)].water[0]);
                yield return new WaitForSecondsRealtime(0.6f);
                _heist.ResultSeen = true; // the failed run is shown once on Home
                JungleHeistEnded.Open(_root, _skin, _endedPrefab, false, Close);
            }
            else if (_heist.Current == JungleHeist.State.Won)
            {
                yield return new WaitForSecondsRealtime(0.6f);
                // The prize first (Popup_Lava_Quest_Rewards); once paid, the won end popup with the next run's timer.
                if (!_heist.PrizeClaimed) JungleHeistRewards.Open(_root, _skin, _rewardsPrefab, Close);
                else JungleHeistEnded.Open(_root, _skin, _endedPrefab, true, Close);
            }
            _busy = false;
        }

        /// <summary>A win: everyone still on jumps to the next pad (or the boat) one after another; the rest fall in.</summary>
        private IEnumerator Advance(int from, int to)
        {
            int before = JungleHeist.SurvivorsPerStep[from], after = JungleHeist.SurvivorsPerStep[to];
            int keep = Mathf.Min(ShownBots, Mathf.Max(0, after - 1));
            _hint.text = "Congratulations! You advanced to the next step!";
            var jumps = new List<Coroutine>();
            jumps.Add(StartCoroutine(Jump(_player, Spot(to, 0), 0f)));
            for (int i = 0; i < _bots.Count; i++)
            {
                if (i < keep) jumps.Add(StartCoroutine(Jump(_bots[i], Spot(to, i + 1), 0.08f * (i + 1))));
                else StartCoroutine(Fall(_bots[i], Pads[Mathf.Min(from, Pads.Length - 1)].at + Pads[Mathf.Min(from, Pads.Length - 1)].water[i % 3]));
            }
            StartCoroutine(RunCounters(from, to, before, after));
            foreach (var jump in jumps) yield return jump;
            _bots.RemoveAll(b => b == null || b.parent != _crowd || b.name.StartsWith("Falling"));
            yield return new WaitForSecondsRealtime(0.3f);
        }

        private IEnumerator Jump(RectTransform who, Vector2 to, float delay)
        {
            if (who == null) yield break;
            yield return new WaitForSecondsRealtime(delay);
            Vector2 from = who.anchoredPosition;
            var scale = who.localScale;
            SnackDash.Audio.Haptics.Light();
            yield return Tween.Run(JumpTime, Tween.Linear, t =>
            {
                if (who == null) return;
                who.anchoredPosition = Vector2.Lerp(from, to, Tween.InOutSine(t)) + Vector2.up * Mathf.Sin(t * Mathf.PI) * 120f;
                who.localScale = scale * (1f + 0.15f * Mathf.Sin(t * Mathf.PI));
            }, true);
        }

        /// <summary>Into the water at one of the pad's WaterAnchors; after <see cref="SinkDelay"/> it sinks (shrinks and fades).</summary>
        private IEnumerator Fall(RectTransform who, Vector2 water)
        {
            if (who == null) yield break;
            who.name = "Falling " + who.name;
            Vector2 from = who.anchoredPosition;
            yield return Tween.Run(0.45f, Tween.Linear, t =>
            {
                if (who == null) return;
                who.anchoredPosition = Vector2.Lerp(from, water, t) + Vector2.up * Mathf.Sin(t * Mathf.PI) * 60f;
                who.localRotation = Quaternion.Euler(0f, 0f, -40f * t);
            }, true);
            SnackDash.Audio.Haptics.Light();
            yield return new WaitForSecondsRealtime(SinkDelay);
            var group = who.gameObject.AddComponent<CanvasGroup>();
            var scale = who.localScale;
            yield return Tween.Run(0.5f, Tween.InQuad, t =>
            {
                if (who == null) return;
                who.localScale = scale * (1f - 0.6f * t);
                who.anchoredPosition = water + Vector2.down * 25f * t;
                group.alpha = 1f - t;
            }, true);
            if (who != null && who != _player) Destroy(who.gameObject);
        }

        private IEnumerator RunCounters(int fromStep, int toStep, int fromPlayers, int toPlayers)
        {
            yield return Tween.Run(CounterTime, Tween.OutQuad, t =>
            {
                _levels.text = $"{Mathf.RoundToInt(Mathf.Lerp(fromStep, toStep, t))}/{JungleHeist.Steps}";
                _players.text = $"{Mathf.RoundToInt(Mathf.Lerp(fromPlayers, toPlayers, t))}/{JungleHeist.Participants}";
            }, true);
            StartCoroutine(Tween.Punch(0.1f, s => { if (_levels != null) _levels.transform.localScale = Vector3.one * (1f + 0.2f * s); }));
            StartCoroutine(Tween.Punch(0.1f, s => { if (_players != null) _players.transform.localScale = Vector3.one * (1f + 0.2f * s); }));
        }

        private void Update()
        {
            Tick(_hand);
            if (_heist == null || (_tick -= Time.unscaledDeltaTime) > 0f) return;
            _tick = 1f;
            _timer.text = Clock(_heist.Left);
        }

        private void Close()
        {
            if (this == null) return;
            Destroy(gameObject);
        }
    }
}

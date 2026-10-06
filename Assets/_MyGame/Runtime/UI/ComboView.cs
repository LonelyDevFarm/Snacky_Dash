using System.Collections;
using SnackDash.Audio;
using SnackDash.Gameplay;
using SnackDash.Presentation;
using TMPro;
using UnityEngine;
using static SnackDash.UI.UiBuilder;

namespace SnackDash.UI
{
    /// <summary>
    /// Praise for crates filled one after another in one go (original ComboView, LocData combo_N_text_option_*):
    /// from the second crate of a run [user] a word pops up with "+N" coins (the Nth crate of the run earns
    /// N-1), and once the run is over "TOTAL +sum" (video: four crates in one delivery, +1 +2 +3, "TOTAL +6"). The coins are for show
    /// until there is a coin economy [user]. A run ends when the snake moves again or the deliveries have
    /// settled. Tier thresholds are stripped from the build: 2-4 crates tier 1 (video), 5-6 tier 2, 7+ tier 3
    /// (inferred). Texts use the original "Combo - BG" / "Combo - Base" presets.
    /// </summary>
    public class ComboView : MonoBehaviour
    {
        private static readonly string[] Tier1 = { "NICE!", "GREAT!", "TASTY!", "WOW!", "CRUNCHY!" };
        private static readonly string[] Tier2 = { "SUCCULENT!", "SCRUMPTIOUS!", "EXQUISITE!", "SUBLIME!" };
        private static readonly string[] Tier3 = { "LEGENDARY!", "DIVINE!", "SNACKY!", "HOLY SNACK!" };

        [SerializeField] private UiSkin skin = new UiSkin();
        [Tooltip("Crates in a run for tier 2 and tier 3 words (not in the build; inferred).")]
        [SerializeField] private int tier2From = 5, tier3From = 7; // video: four crates in a row were still tier 1
        [Tooltip("After the deliveries have settled, wait this long before the total shows.")]
        [SerializeField] private float runGap = 0.6f;
        [Tooltip("Centre of the praise above the screen middle (canvas units).")]
        [SerializeField] private float height = 150f;
        [Tooltip("How far below the filled crate the praise shows (canvas units).")]
        [SerializeField] private float belowCrate = 330f;

        private RectTransform _root;
        private GameSession _session;
        private Vector2 _at;
        private int _chain, _total;
        private int _pending;
        private float _lastCompletion;
        private Coroutine _shown;
        private string _lastWord;

        public void Bind(GameSession session, CratesView crates)
        {
            _session = session;
            _root = Canvas(transform, "Combo", 7);
            session.MoveStarted += direction => EndRun();
            // A crate fills (session) well before its flash (view): count the ones still to show.
            session.CrateCompleted += (column, crate) => _pending++;
            crates.CompletionEffectStarted += column => { _pending = Mathf.Max(0, _pending - 1); _at = Below(column); OnCrateDone(); };
        }

        private void OnCrateDone()
        {
            _chain++;
            _total += _chain - 1; // video: the 2nd crate +1, 3rd +2, 4th +3, TOTAL +6
            _lastCompletion = Time.time;
            if (_chain >= 2) { Show(Word(_chain), "+" + (_chain - 1), _chain >= tier3From); SoundPlayer.Play("Combo"); }
        }

        /// <summary>Video: the praise shows by the crate that filled, a little below it (canvas units).</summary>
        private Vector2 Below(int column)
        {
            var camera = Camera.main;
            if (camera == null || !_session.Board.CrateAnchors.TryGetValue(column, out var anchor)) return new Vector2(0f, height);
            var screen = camera.WorldToScreenPoint(CratesView.CratePosition(anchor));
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_root, screen, null, out var local);
            float halfWidth = _root.rect.width / 2f - 300f;
            float y = local.y - belowCrate;
            if (y < -_root.rect.height / 2f + 520f) y = local.y + belowCrate * 0.6f; // a low crate: above it, clear of the booster bar
            return new Vector2(Mathf.Clamp(local.x, -halfWidth, halfWidth), y);
        }

        /// <summary>The run is over once the deliveries have settled (crates complete about 1.4 s apart, so no timer).</summary>
        private void Update()
        {
            if (_chain > 0 && _pending == 0 && !_session.IsDelivering && Time.time - _lastCompletion > runGap) EndRun();
        }

        private void EndRun()
        {
            if (_chain >= 2) { Show(_lastWord, "TOTAL +" + _total, // the last word stays for the total (video)
                _chain >= tier3From); SoundPlayer.Play("Combo_GiveTotal"); }
            _chain = 0;
            _total = 0;
        }

        private string Word(int chain)
        {
            var pool = chain >= tier3From ? Tier3 : chain >= tier2From ? Tier2 : Tier1;
            string word;
            do word = pool[Random.Range(0, pool.Length)]; while (pool.Length > 1 && word == _lastWord);
            _lastWord = word;
            return word;
        }

        private void Show(string word, string value, bool big)
        {
            if (_shown != null) StopCoroutine(_shown);
            foreach (Transform child in _root) Destroy(child.gameObject);
            _shown = StartCoroutine(Pop(Build(word, value, big)));
            if (GameSettings.Haptics) Haptics.Light();
        }

        private RectTransform Build(string word, string value, bool big)
        {
            var group = Rect(_root, "ComboText", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), _at, new Vector2(900f, 260f));
            group.localRotation = Quaternion.Euler(0f, 0f, 5f); // tilted up to the right (video)
            var title = Rect(group, "Title", new Vector2(0f, 1f), Vector2.one, Vector2.zero, new Vector2(0f, 170f), new Vector2(0.5f, 1f));
            float size = big ? 150f : 125f;
            Text(Stretch(title, "TitleBack"), word, skin.font, skin.titleBackMaterial, size, Color.white);
            Golden(Text(Stretch(title, "TitleFront"), word, skin.font, skin.titleFrontMaterial, size, Color.white));
            var row = Rect(group, "Value", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 45f), new Vector2(600f, 90f));
            Text(Stretch(row, "ValueBack"), value, skin.font, skin.titleBackMaterial, 64f, Color.white);
            var label = Golden(Text(Stretch(row, "ValueText"), value, skin.font, skin.titleFrontMaterial, 64f, Color.white));
            float width = label.GetPreferredValues(value).x;
            if (skin.coinIcon != null)
                Image(Rect(row, "Coin", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(width / 2f + 40f, 4f), new Vector2(70f, 70f)), skin.coinIcon, Color.white).preserveAspect = true;
            CentreOnLetters(group);
            return group;
        }

        /// <summary>Video: light yellow at the top of the letters to orange at the foot.</summary>
        private static TMP_Text Golden(TMP_Text text)
        {
            text.enableVertexGradient = true;
            text.colorGradient = new VertexGradient(Hex("FFF27AFF"), Hex("FFF27AFF"), Hex("FFA21CFF"), Hex("FFA21CFF"));
            return text;
        }

        /// <summary>Pop in (0.25 s, overshoot), hold, rise and fade out.</summary>
        private static IEnumerator Pop(RectTransform group)
        {
            var canvasGroup = group.gameObject.AddComponent<CanvasGroup>();
            group.localScale = Vector3.zero;
            yield return Tween.Run(0.25f, Tween.OutBack(3f), t => { if (group != null) group.localScale = Vector3.one * t; });
            yield return new WaitForSeconds(0.7f);
            var start = group.anchoredPosition;
            yield return Tween.Run(0.3f, Tween.InQuad, t =>
            {
                if (group == null) return;
                group.anchoredPosition = start + new Vector2(0f, 60f * t);
                canvasGroup.alpha = 1f - t;
            });
            if (group != null) Destroy(group.gameObject);
        }
    }
}

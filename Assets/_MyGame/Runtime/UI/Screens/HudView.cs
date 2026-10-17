using System.Collections;
using System.Collections.Generic;
using SnackDash.Economy;
using SnackDash.Gameplay;
using SnackDash.Presentation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SnackDash.UI
{
    /// <summary>
    /// Thanh trên trong màn chơi (Panel_InGame_HUD của Game.unity gốc), nằm trên gốc prefab Prefabs/UI/Screens/Game/HUD đặt sẵn trong scene Game:
    /// biển LEVEL N, thanh tiến độ đầy dần theo trái đã ăn, đầu rắn, nút cài đặt. Mỗi đợt trừ đợt cuối có một mốc trên thanh, ăn hết
    /// đợt đó thì mốc thành dấu tích [user; LevelCompletionBarUI wavePendingPrefab / waveCompletePrefab, delayMilestoneCompleteSpawnMs 150].
    /// Ô "+N" xu của từng combo bật lên dưới đầu phải thanh [user]; streak đang cháy thì ngọn lửa sau đầu rắn (View_StreakActive_HUD).
    /// </summary>
    public class HudView : MonoBehaviour
    {
        [SerializeField] private TMP_Text levelText;
        [SerializeField] private RectTransform fill;
        [SerializeField] private RectTransform pendingHolder, completedHolder;
        [Tooltip("Prefab Prefabs/UI/Screens/Game/WavePending_HUD và WaveComplete_HUD (có CheckIcon_Img).")]
        [SerializeField] private RectTransform pendingMarker, completeMarker;
        [SerializeField] private Button settingsButton;
        [Tooltip("ComboHolder: ô \"+N\" xu của một combo, tắt sẵn.")]
        [SerializeField] private RectTransform coinTag;
        [SerializeField] private TMP_Text coinTagText;
        [Tooltip("View_StreakActive_HUD (prefab FX_StreakHud), tắt sẵn.")]
        [SerializeField] private GameObject streakFire;
        [Tooltip("RemainingFruitsView: thanh đếm trái còn lại dưới thanh tiến độ (bản mới).")]
        [SerializeField] private RemainingFruitBar remainingFruit;
        [Tooltip("Tốc độ đầy của thanh, số lần chiều dài mỗi giây (cho mượt; không đo).")]
        [SerializeField] private float fillSpeed = 1.5f;
        [SerializeField] private float markerCompleteDelay = 0.15f;
        [Tooltip("Popup cài đặt có BACK TO HOME từ level này [user: từ Level 6].")]
        [SerializeField] private int backToHomeFromLevel = 6;
        [Tooltip("Ô \"+N\" của một combo đứng bao lâu rồi ẩn.")]
        [SerializeField] private float coinTagHold = 1.2f;

        [Header("Lúc NO WAY OUT (Top_Container_Version_B của LoseInBoardPopup gốc)")]
        [Tooltip("ProgressBar: thu hẹp lại để chừa chỗ cho ô xu.")]
        [SerializeField] private RectTransform progressBar;
        [Tooltip("CurrencyCoins: ô xu góc phải, thế chỗ nút cài đặt; tắt sẵn.")]
        [SerializeField] private GameObject coinsBox;
        [SerializeField] private TMP_Text coinsText;
        [Tooltip("Thanh lúc hiện xu: tâm X và phần bớt bề ngang (gốc: Panel -107.86 / -242.09, ProgressBar -176.29).")]
        [SerializeField] private float coinsBarX = -107.86f, coinsBarWidthDelta = -418.38f;
        [Tooltip("Giây ô xu hiện dần, cùng nhịp với nút SNIP TAIL (NoWayOutPopup Body Fade Duration).")]
        [SerializeField] private float coinsFadeDuration = 0.2f;

        private readonly List<(float fraction, int fruit, GameObject pending)> _markers = new List<(float, int, GameObject)>();
        private GameSession _session;
        private float _shown, _total;
        private Coroutine _coinTagShow;
        private Vector2 _barPosition, _barSize;
        private int _coinsShown = -1;

        /// <summary>Đích bay của xu combo.</summary>
        public RectTransform CoinTag => coinTag;

        public void Bind(GameSession session, int levelNumber)
        {
            _session = session;
            _total = Mathf.Max(1, session.TotalFruit);
            levelText.text = "LEVEL " + levelNumber;
            streakFire.SetActive(PlayerEconomy.Shared.StreakOnFire && PlayerEconomy.StreakShown(levelNumber));
            settingsButton.onClick.AddListener(() => OpenSettings(levelNumber));
            if (remainingFruit != null) remainingFruit.Bind(session);
            for (int wave = 0; wave < _session.WaveCount - 1; wave++)
            {
                int fruit = _session.FruitInWaves(wave);
                float fraction = fruit / _total;
                _markers.Add((fraction, fruit, Marker(pendingMarker, pendingHolder, fraction).gameObject));
            }
        }

        /// <summary>Cài đặt trong màn: game dừng tới khi đóng; BACK TO HOME từ level <see cref="backToHomeFromLevel"/> (mất 1 tim [user]).</summary>
        private void OpenSettings(int levelNumber)
        {
            var ui = Core.Services.Get<Core.UIManager>();
            if (ui.IsOpen<SettingsPopup>()) return;
            var settings = ui.Show<SettingsPopup>();
            if (settings == null) return;
            Time.timeScale = 0f;
            settings.Closed += () => Time.timeScale = 1f;
            settings.Setup(levelNumber >= backToHomeFromLevel ? () => { PlayerEconomy.Shared.Lose(); Core.EventBus.Publish(new Core.LevelLostEvent(levelNumber)); LoadingScreen.Load(LoadingScreen.HomeScene); } : (System.Action)null);
        }

        /// <summary>
        /// Lúc NO WAY OUT: nút cài đặt nhường chỗ cho ô xu để người chơi thấy còn bao nhiêu xu (video Man_Lose, ảnh user 15/10).
        /// </summary>
        public void ShowCoins(bool show)
        {
            if (coinsBox == null || coinsBox.activeSelf == show) return;
            if (show) { _barPosition = progressBar.anchoredPosition; _barSize = progressBar.sizeDelta; }
            settingsButton.gameObject.SetActive(!show);
            coinsBox.SetActive(show);
            if (show) StartCoroutine(FadeIn(coinsBox));
            progressBar.anchoredPosition = show ? new Vector2(coinsBarX, _barPosition.y) : _barPosition;
            progressBar.sizeDelta = show ? new Vector2(coinsBarWidthDelta, _barSize.y) : _barSize;
            _coinsShown = -1;
        }

        private IEnumerator FadeIn(GameObject box)
        {
            if (!box.TryGetComponent<CanvasGroup>(out var group)) group = box.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            yield return Tween.Run(coinsFadeDuration, Tween.Linear, t => group.alpha = t);
        }

        /// <summary>Xu của một combo [user]: ô bật lên với "+N" của riêng combo đó, đứng một lúc rồi ẩn; tổng cả màn chỉ cộng ở màn thắng.</summary>
        public void ShowComboCoins(int coins)
        {
            if (coins <= 0) return;
            if (_coinTagShow != null) StopCoroutine(_coinTagShow);
            _coinTagShow = StartCoroutine(CoinTagRoutine(coins));
        }

        private IEnumerator CoinTagRoutine(int coins)
        {
            coinTagText.text = "+" + coins;
            if (!coinTag.gameObject.activeSelf)
            {
                coinTag.gameObject.SetActive(true);
                yield return Tween.Run(0.25f, Tween.OutBack(3f), t => coinTag.localScale = Vector3.one * t);
            }
            else yield return Tween.Punch(0.12f, t => coinTag.localScale = Vector3.one * (1f + 0.25f * t));
            yield return new WaitForSeconds(coinTagHold);
            yield return Tween.Run(0.2f, Tween.InBack(2f), t => coinTag.localScale = Vector3.one * (1f - t));
            coinTag.gameObject.SetActive(false);
            coinTag.localScale = Vector3.one;
            _coinTagShow = null;
        }

        /// <summary>Một mốc đặt ở <paramref name="fraction"/> chiều dài thanh.</summary>
        private static RectTransform Marker(RectTransform prefab, RectTransform holder, float fraction)
        {
            var marker = Instantiate(prefab, holder);
            marker.anchorMin = new Vector2(fraction, 0f);
            marker.anchorMax = new Vector2(fraction, 1f);
            return marker;
        }

        /// <summary>WaveComplete_HUD: mốc đã xong với dấu tích nảy ra.</summary>
        private IEnumerator CompleteMarker(float fraction, GameObject pending)
        {
            yield return new WaitForSeconds(markerCompleteDelay);
            if (pending != null) Destroy(pending);
            var check = Marker(completeMarker, completedHolder, fraction).Find("CheckIcon_Img"); // FX_Reward_Particles nằm trước, bắn khi mốc hiện
            yield return Tween.Run(0.25f, Tween.OutBack(3f), t => { if (check != null) check.localScale = Vector3.one * t; });
        }

        private void Update()
        {
            if (_session == null) return;
            if (coinsBox != null && coinsBox.activeSelf && PlayerEconomy.Shared.Coins != _coinsShown)
            {
                _coinsShown = PlayerEconomy.Shared.Coins;
                coinsText.text = _coinsShown.ToString();
            }
            int eaten = _session.FruitEaten;
            _shown = Mathf.MoveTowards(_shown, eaten / _total, fillSpeed * Time.deltaTime);
            fill.anchorMax = new Vector2(_shown, 1f);

            for (int i = 0; i < _markers.Count; i++)
            {
                var marker = _markers[i];
                if (marker.pending == null || eaten < marker.fruit) continue;
                StartCoroutine(CompleteMarker(marker.fraction, marker.pending));
                _markers[i] = (marker.fraction, marker.fruit, null);
            }
        }
    }
}

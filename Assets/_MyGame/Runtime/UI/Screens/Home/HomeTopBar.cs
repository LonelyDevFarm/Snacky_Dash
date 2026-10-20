using System;
using System.Collections;
using SnackDash.Economy;
using SnackDash.Presentation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SnackDash.UI
{
    /// <summary>
    /// Thanh trên của Home (HomeTopView): ô xu (bấm mở tab Shop), ô tim (số tim và FULL, giờ tới tim sau, hoặc ∞ với giờ còn lại;
    /// bấm mở LivesPopup, trừ lúc tim vô hạn [user, sk1 cuối video]), nút cài đặt. Xu từ màn thắng / popup bay về ô xu, ô đếm
    /// tăng dần theo từng đồng chạm tới.
    /// </summary>
    public class HomeTopBar : MonoBehaviour
    {
        [SerializeField] private Button coinsButton;
        [SerializeField] private Button livesButton;
        [SerializeField] private Button settingsButton;
        [SerializeField] private RectTransform coinIcon;
        [SerializeField] private RectTransform livesIcon;
        [SerializeField] private TMP_Text coins;
        [Tooltip("FULL / giờ tới tim sau / giờ còn của tim vô hạn.")]
        [SerializeField] private TMP_Text livesValue;
        [SerializeField] private TMP_Text livesAmount;
        [SerializeField] private Image livesHeart;
        [Tooltip("Icon_Unlimited: trái tim có dấu ∞, hiện thay khi tim vô hạn.")]
        [SerializeField] private GameObject livesInfinite;
        [Header("Xu bay về")]
        [Tooltip("Một đồng xu bay (prefab Prefabs/UI/Screens/Home/FlyingCoin).")]
        [SerializeField] private RectTransform flyingCoinPrefab;
        [Tooltip("Chồng xu \"+N\" trên nút Play khi về từ màn thắng (prefab Prefabs/UI/Screens/Home/WinCoins).")]
        [SerializeField] private RectTransform winCoinsPrefab;
        [Tooltip("Chồng xu đặt lệch so với tâm nút Play (sk2 7.0 s).")]
        [SerializeField] private Vector2 winCoinsOffset = new Vector2(-15f, 215f);
        [Tooltip("Mỗi đồng cách nhau (giây) và bay mất bao lâu (sk2 8.0-8.7 s).")]
        [SerializeField] private float coinGap = 0.06f, coinFlyTime = 0.55f;

        private const string WinCoinsKey = "home_win_coins";
        private int _held; // xu đã vào ví nhưng còn đang bay, chưa cộng lên ô
        private float _tick;
        private RectTransform _winCoins;

        /// <summary>Bấm ô xu (hoặc dấu + của nó).</summary>
        public event Action CoinsClicked;

        /// <summary>Đích bay của xu / tim từ popup.</summary>
        public RectTransform CoinIcon => coinIcon;
        public RectTransform LivesIcon => livesIcon;

        /// <summary>Đang có chồng xu thắng trên màn.</summary>
        public bool Busy => _winCoins != null;

        /// <summary>Xu của màn thắng (đã vào ví) để Home cho bay về ô xu khi người chơi về tới.</summary>
        public static void HoldWinCoins(int amount)
        {
            PlayerPrefs.SetInt(WinCoinsKey, PlayerPrefs.GetInt(WinCoinsKey, 0) + amount);
            PlayerPrefs.Save();
        }

        private static Core.UIManager UI => Core.Services.Get<Core.UIManager>();

        private RectTransform Layer => (RectTransform)GetComponentInParent<Canvas>().rootCanvas.transform;

        private void Awake()
        {
            coinsButton.onClick.AddListener(() => CoinsClicked?.Invoke());
            livesButton.onClick.AddListener(() => { if (!PlayerEconomy.Shared.InfiniteLives) UI.Show<LivesPopup>(); });
            settingsButton.onClick.AddListener(() => UI.Show<SettingsPopup>());
            Core.Services.Register(this); // popup sự kiện cho xu / tim bay về các ô của thanh này
        }

        private void OnDestroy() => Core.Services.Unregister(this);

        private void OnEnable()
        {
            PlayerEconomy.Shared.Changed += Refresh;
            Refresh();
        }

        private void OnDisable() => PlayerEconomy.Shared.Changed -= Refresh;

        private void Update()
        {
            if ((_tick -= Time.unscaledDeltaTime) > 0f) return;
            _tick = 0.25f; // đồng hồ hồi tim
            Refresh();
        }

        private void Refresh()
        {
            var economy = PlayerEconomy.Shared;
            coins.text = (economy.Coins - _held).ToString();
            bool infinite = economy.InfiniteLives;
            int lives = economy.Lives;
            livesAmount.text = lives.ToString();
            livesAmount.gameObject.SetActive(!infinite);
            livesInfinite.SetActive(infinite);
            livesHeart.enabled = !infinite;
            livesValue.text = infinite ? LivesPopup.Clock(economy.InfiniteLeft)
                : lives >= economy.LifeCap ? "FULL" : $"{(int)economy.NextLifeIn.TotalMinutes:00}:{economy.NextLifeIn.Seconds:00}";
        }

        /// <summary>Có xu thắng đang giữ thì hiện chồng xu trên <paramref name="playButton"/> và cho bay về, sau khi <paramref name="wait"/> hết đúng.</summary>
        public void ShowWinCoins(Transform playButton, Func<bool> wait)
        {
            int amount = PlayerPrefs.GetInt(WinCoinsKey, 0);
            if (amount <= 0) return;
            PlayerPrefs.DeleteKey(WinCoinsKey);
            PlayerPrefs.Save();
            _held = amount;
            Refresh();
            _winCoins = Instantiate(winCoinsPrefab, Layer);
            _winCoins.gameObject.SetActive(false);
            StartCoroutine(WinCoins(playButton, amount, wait));
        }

        /// <summary>
        /// sk2 6.6-9.3 s: lúc màn chờ tan, chồng xu "+N" đứng trên nút Play và lắc; rồi tách thành từng đồng bay vòng lên ô xu,
        /// ô đếm tăng theo từng đồng chạm tới (khoảng 0.8 s) và nảy.
        /// </summary>
        private IEnumerator WinCoins(Transform playButton, int amount, Func<bool> wait)
        {
            yield return null;
            while (wait()) yield return null;
            var pile = _winCoins;
            pile.gameObject.SetActive(true);
            pile.position = playButton.position;
            pile.anchoredPosition += winCoinsOffset;
            pile.GetComponentInChildren<TMP_Text>().text = "+" + amount;

            // Bật ra, rồi lắc.
            yield return Tween.Run(0.25f, Tween.OutBack(2f), t => pile.localScale = Vector3.one * t, true);
            yield return Tween.Run(0.8f, Tween.Linear, t => pile.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(t * Mathf.PI * 6f) * 7f * (1f - t * 0.5f)), true);
            pile.localRotation = Quaternion.identity;

            StartCoroutine(Tween.Run(0.25f, Tween.Linear, t => { pile.localScale = Vector3.one * (1f - t); }, true));
            yield return StreamCoins(pile.position, amount);
            _held = 0;
            Refresh();
            Destroy(pile.gameObject);
            _winCoins = null;
        }

        /// <summary>Xu đã vào ví bay từ một popup về ô xu, ô đếm tăng theo từng đồng chạm tới (thưởng Jungle Heist).</summary>
        public void FlyCoins(Vector3 from, int amount)
        {
            _held += amount;
            Refresh();
            StartCoroutine(StreamCoins(from, amount));
        }

        /// <summary>Từng đồng một, mỗi đồng vòng lên ô xu và trả phần xu của nó.</summary>
        private IEnumerator StreamCoins(Vector3 from, int amount)
        {
            int count = Mathf.Clamp(amount / 5, 5, 12);
            int landed = 0, given = 0;
            for (int i = 0; i < count; i++)
            {
                int share = (amount - given) / (count - i);
                given += share;
                StartCoroutine(StreamCoin(from, i * coinGap, () =>
                {
                    landed++;
                    _held = Mathf.Max(0, _held - share);
                    Refresh();
                    Audio.SoundPlayer.Play("CollectCoin");
                    StartCoroutine(Tween.Punch(0.06f, s => coinIcon.localScale = Vector3.one * (1f + 0.15f * s)));
                }));
            }
            while (landed < count) yield return null;
        }

        private IEnumerator StreamCoin(Vector3 from, float delay, Action landed)
        {
            for (float t = 0f; t < delay; t += Time.unscaledDeltaTime) yield return null;
            var layer = Layer;
            var coin = Instantiate(flyingCoinPrefab, layer);
            Vector3 to = coinIcon.position;
            // Lên và hơi lệch trái trước, rồi vào ô xu (sk2 8.0-8.7 s).
            Vector3 control = new Vector3(Mathf.Lerp(from.x, to.x, 0.2f) - 120f * layer.lossyScale.y, Mathf.Lerp(from.y, to.y, 0.55f), from.z);
            yield return Tween.Run(coinFlyTime, Tween.Linear, t =>
            {
                float e = Tween.InOutSine(t), u = 1f - e;
                coin.position = u * u * from + 2f * u * e * control + e * e * to;
                coin.localScale = Vector3.one * Mathf.Lerp(1f, 0.75f, e);
            }, true);
            Destroy(coin.gameObject);
            landed();
        }
    }
}

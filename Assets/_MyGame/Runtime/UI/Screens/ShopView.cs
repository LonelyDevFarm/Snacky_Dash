using System;
using System.Collections;
using System.Collections.Generic;
using SnackDash.Economy;
using SnackDash.Presentation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Random = UnityEngine.Random;

namespace SnackDash.UI
{
    /// <summary>
    /// Shop (prefab <c>Prefabs/UI/Screens/Shop/ShopView</c>, gốc: Resources/shop/ShopView.prefab, video "home"): đầu tím với ô xu và chữ "Shop",
    /// rồi trang cuộn các mục, mỗi mục một băng màu trên đường vàng. Bundles dùng thẻ <see cref="ShopBundleCard"/>, Coins là lưới 3 cột
    /// <see cref="ShopCoinCard"/>; danh sách gói lấy từ <see cref="ShopCatalog"/>. Mọi giá là 0đ, mua là nhận ngay [user], xu bay về ô xu.
    /// Bỏ [user]: Special Offers và Restore Purchases.
    /// <para>Hai cách mở: tab Shop ở Home (<see cref="ShowAsTab"/>, không có X, chừa chỗ thanh tab) hoặc popup khi thiếu xu
    /// (<c>UIManager.Show&lt;ShopView&gt;()</c>, có X quay lại chỗ cũ [user]).</para>
    /// </summary>
    public class ShopView : BasePopup
    {
        [SerializeField] private RectTransform safeArea;
        [SerializeField] private ScrollRect scroll;
        [Tooltip("Mục Bundles và Coins (mỗi mục có băng tiêu đề sẵn).")]
        [SerializeField] private RectTransform bundles, coins;
        [SerializeField] private ShopBundleCard bundlePrefab;
        [SerializeField] private ShopCoinCard coinPrefab;
        [SerializeField] private RectTransform coinIcon;
        [SerializeField] private TMP_Text coinText;
        [Tooltip("Xẻng, boomerang, undo, nam châm.")]
        [SerializeField] private Sprite[] boosterSprites = new Sprite[4];
        [SerializeField] private Sprite infiniteLivesSprite;
        [Tooltip("Đồng xu bay về ô xu khi mua.")]
        [SerializeField] private Sprite flyingCoin;
        [Tooltip("Prefab mẫu một đồng xu bay (Prefabs/UI/Widgets/FlyingCoin): lấy từ kho tái dùng, không dựng bằng code.")]
        [SerializeField] private Image flyingCoinPrefab;
        private UiPool _coins;
        [Header("Bố cục (gốc)")]
        [SerializeField] private float cardHeight = 410f, coinCellWidth = 268.67f, coinCellHeight = 350f, gap = 20f;

        private int _shownCoins;
        private Coroutine _count;

        protected override void OnInit()
        {
            _shownCoins = PlayerEconomy.Shared.Coins;
            coinText.text = _shownCoins.ToString();
            BuildPage();
        }

        protected override void OnShow()
        {
            var area = Screen.safeArea;
            safeArea.anchorMin = new Vector2(area.xMin / Screen.width, 0f);
            safeArea.anchorMax = new Vector2(area.xMax / Screen.width, area.yMax / Screen.height);
        }

        /// <summary>Tab Shop ở Home: không có X, chừa <paramref name="bottomInset"/> cho thanh tab.</summary>
        public void ShowAsTab(float bottomInset)
        {
            closeButton.gameObject.SetActive(false);
            var rect = (RectTransform)scroll.transform;
            rect.offsetMin = new Vector2(0f, bottomInset);
            Show();
        }

        // ------------------------------------------------------------------ trang

        private void BuildPage()
        {
            float y = bundles.anchoredPosition.y * -1f; // lề trên của Content
            int count = ShopCatalog.Bundles.Length;
            y = Place(bundles, y, count, cardHeight);
            for (int i = 0; i < count; i++)
            {
                var pack = ShopCatalog.Bundles[i];
                var card = Instantiate(bundlePrefab, bundles);
                ((RectTransform)card.transform).anchoredPosition = new Vector2(0f, -(115f + i * (cardHeight + gap)));
                card.Show(pack.image, ShopCatalog.Amount(pack.coins), pack.title, Cells(pack));
                card.Bought += from => Buy(pack, from);
            }
            int rows = (ShopCatalog.Coins.Length + 2) / 3;
            y = Place(coins, y, 1, 10f + rows * coinCellHeight + (rows - 1) * gap);
            for (int i = 0; i < ShopCatalog.Coins.Length; i++)
            {
                var pack = ShopCatalog.Coins[i];
                var card = Instantiate(coinPrefab, coins);
                ((RectTransform)card.transform).anchoredPosition = new Vector2(25f + (i % 3) * (coinCellWidth + gap), -(115f + 10f + (i / 3) * (coinCellHeight + gap)));
                card.Show(pack.image, ShopCatalog.Amount(pack.coins));
                card.Bought += from => Buy(pack, from);
            }
            scroll.content.sizeDelta = new Vector2(0f, y + 50f); // lề dưới của Content
        }

        /// <summary>Một mục: băng 95 cao ở trên, các thẻ từ 115, cách nhau 20, dưới cùng 15.</summary>
        private float Place(RectTransform section, float y, int items, float itemHeight)
        {
            float height = 115f + items * itemHeight + (items - 1) * gap + 15f;
            section.anchoredPosition = new Vector2(0f, -y);
            section.sizeDelta = new Vector2(section.sizeDelta.x, height);
            return y + height;
        }

        /// <summary>Các ô của thẻ gói: booster gộp theo số lượng trước, rồi tim vô hạn (thứ tự trong video).</summary>
        private List<(Sprite[] icons, string label)> Cells(ShopPack pack)
        {
            var cells = new List<(Sprite[] icons, string label)>();
            if (pack.boosters != null)
            {
                var icons = new List<Sprite>();
                int count = 0;
                for (int i = 0; i < pack.boosters.Length; i++) if (pack.boosters[i] > 0) { icons.Add(boosterSprites[i]); count = pack.boosters[i]; }
                if (icons.Count > 0) cells.Add((icons.ToArray(), "x" + count));
            }
            if (pack.infiniteSeconds > 0) cells.Add((new[] { infiniteLivesSprite }, ShopCatalog.Duration(pack.infiniteSeconds)));
            return cells;
        }

        // ------------------------------------------------------------------ mua

        private void Buy(ShopPack pack, RectTransform from)
        {
            var economy = PlayerEconomy.Shared;
            int before = economy.Coins;
            pack.Grant(economy);
            economy.Report(MissionGoal.CompletePurchase); // mỗi gói tính là một lần mua, không trả tiền ở đây [user]
            SnackDash.Audio.SoundPlayer.Play("CollectCoin");
            if (pack.coins > 0) FlyCoins(from, before, economy.Coins);
        }

        /// <summary>Một nắm xu bung ra từ nút và bay vào ô xu, ô xu đếm lên khi xu tới.</summary>
        private void FlyCoins(RectTransform from, int start, int end)
        {
            const int count = 8;
            Vector3 origin = from.TransformPoint(from.rect.center), target = coinIcon.TransformPoint(coinIcon.rect.center);
            for (int i = 0; i < count; i++)
            {
                if (_coins == null) _coins = new UiPool(flyingCoinPrefab, transform);
                var image = _coins.Get();
                image.sprite = flyingCoin;
                var coin = image.rectTransform;
                coin.sizeDelta = new Vector2(80f, 80f);
                coin.position = origin;
                Vector3 burst = origin + (Vector3)(Random.insideUnitCircle * 120f * coin.lossyScale.x);
                StartCoroutine(Fly(coin, burst, target, i * 0.05f, i == count - 1 ? () => CountTo(start, end) : (Action)null));
            }
        }

        private IEnumerator Fly(RectTransform coin, Vector3 burst, Vector3 target, float delay, Action landed)
        {
            Vector3 origin = coin.position;
            yield return Tween.Run(0.2f, Tween.OutQuad, t => coin.position = Vector3.LerpUnclamped(origin, burst, t));
            if (delay > 0f) yield return new WaitForSeconds(delay);
            yield return Tween.Run(0.45f, Tween.InQuad, t =>
            {
                coin.position = Vector3.LerpUnclamped(burst, target, t);
                coin.localScale = Vector3.one * Mathf.Lerp(1f, 0.7f, t);
            });
            if (coin != null) _coins.Release(coin.GetComponent<Image>());
            if (coinIcon != null) StartCoroutine(Tween.Punch(0.06f, t => coinIcon.localScale = Vector3.one * (1f + 0.2f * t)));
            landed?.Invoke();
        }

        private void CountTo(int start, int end)
        {
            if (_count != null) StopCoroutine(_count);
            _count = StartCoroutine(Tween.Run(0.5f, Tween.OutQuad, t => { _shownCoins = Mathf.RoundToInt(Mathf.Lerp(start, end, t)); coinText.text = _shownCoins.ToString(); }));
        }
    }
}

using System;
using System.Collections;
using SnackDash.Economy;
using SnackDash.Presentation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static SnackDash.UI.UiBuilder;
using Random = UnityEngine.Random;

namespace SnackDash.UI
{
    /// <summary>
    /// The Shop (original Resources/shop/ShopView.prefab, video "home"): purple header with the coin counter and
    /// "Shop", then a scrolling page of sections, each a coloured banner on a gold line. Bundles uses the
    /// BundleShopItem_Bundle_Offer cards: coins in a box with turning rays, reward cells (boosters, infinite lives),
    /// the pack name and a green price button. Coins uses a 3-column grid of CoinPackShopItem cards. Every price is
    /// 0đ and buying gives the pack at once [user]; coins then fly into the counter. Left out [user]: Special Offers
    /// (No Ads, Starter Pack, No Ads Pack) and Restore Purchases.
    /// <para>From the Home tab it is a page (no X); opened when coins run short it covers the screen and its X
    /// goes back to where the player was [user].</para>
    /// </summary>
    public class ShopView : MonoBehaviour
    {
        private const float SectionWidth = 896f, ItemWidth = 846f, CardHeight = 410f;
        private static readonly Color Cream = new Color(0.98f, 0.965f, 0.784f); // FAF6C8, the Shop's text colour

        private UiSkin _skin;
        private GameObject _host;
        private RectTransform _content, _coinIcon;
        private TextMeshProUGUI _coins;
        private int _shownCoins;
        private Coroutine _count;
        private readonly System.Collections.Generic.List<RectTransform> _rays = new System.Collections.Generic.List<RectTransform>();

        /// <param name="parent">A full-screen rect (Home or Game canvas).</param>
        /// <param name="bottomInset">Room left at the bottom (the Home tab bar).</param>
        /// <param name="onClose">Null for the Home page; otherwise the X shows and calls it after closing.</param>
        public static ShopView Open(RectTransform parent, UiSkin skin, float bottomInset, Action onClose)
        {
            // Over a level or a popup it gets its own canvas so it covers every HUD canvas.
            var host = onClose != null ? Canvas(null, "Shop", 50) : parent;
            var root = Stretch(host, "ShopView");
            Image(root, null, Hex("1E003FFF"), raycast: true); // the page behind the cards (video)
            var shop = root.gameObject.AddComponent<ShopView>();
            shop._skin = skin;
            shop._host = host != parent ? host.gameObject : root.gameObject;
            var safe = Stretch(root, "SafeArea");
            var area = Screen.safeArea;
            safe.anchorMin = new Vector2(area.xMin / Screen.width, 0f);
            safe.anchorMax = new Vector2(area.xMax / Screen.width, area.yMax / Screen.height);
            shop.BuildPage(safe, bottomInset);
            shop.BuildHeader(safe, onClose);
            SlicedCapFit.AddTo(root); // the gold line, shadows, counter and buttons kept pointed ends when squeezed [user]
            CentreOnLetters(root);
            Pressable(root);
            return shop;
        }

        public void Close() => Destroy(_host);

        // ------------------------------------------------------------------ header

        private void BuildHeader(RectTransform safe, Action onClose)
        {
            var top = Rect(safe, "TopPanel", new Vector2(0f, 1f), Vector2.one, Vector2.zero, new Vector2(0f, 140f), new Vector2(0.5f, 1f));
            // The purple band runs 600 up past the panel so it also fills the notch.
            Image(Rect(top, "Background", Vector2.zero, Vector2.one, new Vector2(0f, 300f), new Vector2(0f, 600f)), _skin.Get("S_Shop_Header_Bg"), Color.white, sliced: true);
            var title = Rect(top, "TopPanelText (TMP)", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(7.7f, 10f), new Vector2(250f, 90f));
            title.localScale = Vector3.one * 1.05f;
            ShopText(title, "Shop", "LilitaOne-Regular - Shop - Title 2", 72f, Hex("EFBE42FF"));
            ShopText(Rect(title, "TopPanelText (TMP) (1)", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(250f, 90f)), "Shop", "LilitaOne-Regular - Shop - Title", 72f, Hex("FAF1CBFF"));

            var hud = Rect(top, "CurrencyHUD", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(30f, 3f), new Vector2(240f, 90f), new Vector2(0f, 0.5f));
            Image(Rect(hud, "BG", Vector2.zero, Vector2.one, new Vector2(25f, 4f), new Vector2(-50f, -36f)), _skin.Get("S_Counter_Bg_0"), Color.white, sliced: true);
            var icon = Rect(hud, "Icon", Vector2.zero, new Vector2(0f, 1f), new Vector2(8f, 0f), new Vector2(85f, 0f), new Vector2(0f, 0.5f));
            _coinIcon = Rect(icon, "Currency Icon", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Vector2(0f, 0.5f));
            Image(_coinIcon, _skin.Get("S_Coin_Icon"), Color.white).preserveAspect = true; // no + here (video), unlike Home
            _shownCoins = PlayerEconomy.Shared.Coins;
            _coins = Text(Rect(hud, "Value Text", new Vector2(0.5f, 0f), new Vector2(0.5f, 1f), new Vector2(42f, 5f), new Vector2(115f, -38f)), _shownCoins.ToString(), _skin.font, _skin.homeScreenLevelsMaterial, 40f, Hex("4F3976FF"));
            AutoSize(_coins, 23f, 40f);

            if (onClose == null) return;
            var cancel = Rect(top, "Cancel Button", new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-30f, 5f), new Vector2(110f, 110f), new Vector2(1f, 0.5f));
            Image(cancel, null, Color.clear, raycast: true);
            Image(Rect(cancel, "cancel icon", Vector2.zero, Vector2.one, Vector2.zero, new Vector2(-20f, -20f)), _skin.Get("S_Close_Btn"), Color.white).preserveAspect = true;
            cancel.gameObject.AddComponent<Button>().onClick.AddListener(() => { Close(); onClose(); });
        }

        // ------------------------------------------------------------------ page

        private void BuildPage(RectTransform safe, float bottomInset)
        {
            var scroll = Rect(safe, "Scroll View", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            scroll.offsetMin = new Vector2(0f, bottomInset);
            scroll.offsetMax = new Vector2(0f, -140f);
            var viewport = Stretch(scroll, "Viewport");
            viewport.gameObject.AddComponent<RectMask2D>();
            Image(viewport, null, Color.clear, raycast: true); // catches drags between the cards
            _content = Rect(viewport, "Content", new Vector2(0f, 1f), Vector2.one, Vector2.zero, Vector2.zero, new Vector2(0.5f, 1f));
            var rect = scroll.gameObject.AddComponent<ScrollRect>();
            rect.horizontal = false;
            rect.viewport = viewport;
            rect.content = _content;
            rect.scrollSensitivity = 30f;

            float y = 21f; // Content padding top
            var bundles = Section(ref y, "BundlesSection", "S_Header_Purple_Bg", "Bundles", Color.white, "LilitaOne-Regular - Shop - Bundles Title", ShopCatalog.Bundles.Length, CardHeight);
            for (int i = 0; i < ShopCatalog.Bundles.Length; i++) BundleCard(bundles, 115f + i * (CardHeight + 20f), ShopCatalog.Bundles[i]);
            int rows = (ShopCatalog.Coins.Length + 2) / 3;
            var coins = Section(ref y, "CoinsSection", "S_Header_Orange_Bg", "Coins", Cream, "LilitaOne-Regular - Shop - Coins Title", 1, 10f + rows * 350f + (rows - 1) * 20f);
            for (int i = 0; i < ShopCatalog.Coins.Length; i++) CoinCard(coins, i, ShopCatalog.Coins[i]);
            _content.sizeDelta = new Vector2(0f, y + 50f); // Content padding bottom
        }

        /// <summary>A section: banner 95 high at the top (on a gold line across the screen), items from 115, 20 apart, 15 below.</summary>
        private RectTransform Section(ref float y, string name, string banner, string title, Color titleColor, string material, int items, float itemHeight)
        {
            float height = 115f + items * itemHeight + (items - 1) * 20f + 15f;
            var section = Rect(_content, name, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -y), new Vector2(SectionWidth, height), new Vector2(0.5f, 1f));
            var head = Rect(section, "SectionTitle", new Vector2(0f, 1f), Vector2.one, new Vector2(0f, -50f), new Vector2(0f, 95f));
            Image(Rect(head, "Shadow", Vector2.zero, Vector2.one, new Vector2(0f, -25f), new Vector2(-20f, 0f)), _skin.Get("S_CircleShadow"), new Color(0f, 0f, 0f, 0.5f), sliced: true).pixelsPerUnitMultiplier = 1.29f;
            Image(Rect(head, "Frame", new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0f, 4f), new Vector2(1200f, 12f)), _skin.Get("S_Shop_Frame"), Color.white, sliced: true);
            Image(Rect(head, "Background", Vector2.zero, Vector2.one, Vector2.zero, new Vector2(-50f, 0f)), _skin.Get(banner), Color.white, sliced: true).pixelsPerUnitMultiplier = 1.29f;
            AutoSize(ShopText(Rect(head, "Text (TMP)", Vector2.zero, Vector2.one, Vector2.zero, new Vector2(-20f, 0f)), title, material, 55.1f, titleColor), 18f, 55.1f);
            y += height;
            return section;
        }

        /// <summary>BundleShopItem_Bundle_Offer: 846 x 410; coins box 300 wide (video), reward cells, name and price below.</summary>
        private void BundleCard(RectTransform section, float y, ShopPack pack)
        {
            var card = Rect(section, "BundleShopItem_Bundle_Offer", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -y), new Vector2(ItemWidth, CardHeight), new Vector2(0.5f, 1f));
            Image(Rect(card, "Background", Vector2.zero, Vector2.one, new Vector2(0f, -7.5f), new Vector2(0f, 15f)), _skin.Get("S_Shop_Bundle_0"), Color.white, sliced: true).pixelsPerUnitMultiplier = 1.3f;

            // TopLayer (235 high, padding 26 / 25 / 26): MainReward, then the cells (8 apart, 10 from the right).
            const float left = 26f, right = 25f, top = 26f, layer = 235f, mainWidth = 300f;
            var main = Rect(card, "MainReward", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(left, -top), new Vector2(mainWidth, layer - top), new Vector2(0f, 1f));
            var rays = Stretch(main, "RaysAnimation");
            Image(rays, _skin.Get("S_Rewards_Banner_0"), Hex("F9EECDFF"), sliced: true).pixelsPerUnitMultiplier = 0.35f; // cream under white rays (video)
            rays.gameObject.AddComponent<RectMask2D>();
            var spin = Rect(rays, "Rays", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(938.6f, 750.8f));
            spin.localScale = Vector3.one * 0.6f;
            Image(spin, _skin.Get("GlowRays"), new Color(1f, 1f, 1f, 0.8f));
            _rays.Add(spin);
            Image(Rect(rays, "Glow", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 26.6f), new Vector2(382.6f, 279.3f)), _skin.glow, new Color(1f, 1f, 1f, 0.686f));
            Image(Rect(main, "Icon", new Vector2(0f, 1f), Vector2.one, Vector2.zero, new Vector2(0f, 151.9f), new Vector2(0.5f, 1f)), _skin.Get(pack.icon), Color.white).preserveAspect = true;
            AutoSize(ShopText(Rect(main, "AmountText (TMP)", Vector2.zero, new Vector2(1f, 0f), Vector2.zero, new Vector2(-10f, 70f), new Vector2(0.5f, 0f)), ShopCatalog.Amount(pack.coins), "LilitaOne-Regular - Shop", 55f, Cream), 18f, 55f);

            // Cells: boosters grouped by count first, then infinite lives (video card order). Each is as wide as
            // its icons plus an equal share of the rest (layout preferred + flexible).
            var cells = new System.Collections.Generic.List<(Sprite[] icons, string label)>();
            if (pack.boosters != null)
            {
                var icons = new System.Collections.Generic.List<Sprite>();
                Sprite[] all = { _skin.shovelIcon, _skin.boomerangIcon, _skin.undoIcon, _skin.magnetIcon };
                int count = 0;
                for (int i = 0; i < pack.boosters.Length; i++) if (pack.boosters[i] > 0) { icons.Add(all[i]); count = pack.boosters[i]; }
                if (icons.Count > 0) cells.Add((icons.ToArray(), "x" + count));
            }
            if (pack.infiniteSeconds > 0) cells.Add((new[] { _skin.Get("S_Icon_Heart_Infinit_Lives") }, ShopCatalog.Duration(pack.infiniteSeconds)));
            float x0 = left + mainWidth, area = ItemWidth - right - 10f - x0, gaps = (cells.Count - 1) * 8f;
            float preferred = 0f;
            foreach (var cell in cells) preferred += GridWidth(cell.icons.Length);
            float share = cells.Count > 0 ? (area - gaps - preferred) / cells.Count : 0f;
            float x = x0;
            foreach (var cell in cells)
            {
                float width = GridWidth(cell.icons.Length) + share;
                RewardCell(card, new Vector2(x, -(top + 3f)), new Vector2(width, layer - top - 3f), cell.icons, cell.label);
                x += width + 8f;
            }

            // BottomLayer (146 high, from 249.5): the name on the left, the price button (325 x 115) on the right.
            var bottom = Rect(card, "BottomLayer", new Vector2(0f, 1f), Vector2.one, new Vector2(0f, -249.5f), new Vector2(0f, 146f), new Vector2(0.5f, 1f));
            var pattern = Rect(bottom, "Background", Vector2.zero, new Vector2(1f, 0f), new Vector2(0f, 5f), new Vector2(0f, 137f), new Vector2(0.5f, 0f));
            Image(Rect(pattern, "Pattern", Vector2.zero, Vector2.one, new Vector2(-133.8f, 10.7f), new Vector2(-318.4f, -28.75f)), _skin.Get("S_Offer_Blueberries_Overlay_Bg"), Hex("6114BEFF"));
            float rowY = -(15f + (146f - 15f - 40f) * 0.5f); // centred in the padded row
            var name = ShopText(Rect(bottom, "TitleText (TMP)", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(40f, rowY), new Vector2(ItemWidth - 40f - 30f - 30f - 325f, 90f), new Vector2(0f, 0.5f)), pack.title, "LilitaOne-Regular - Shop - Bundles Title", 53f, Cream);
            name.alignment = TextAlignmentOptions.MidlineLeft;
            AutoSize(name, 20f, 53f);
            BuyButton(bottom, new Vector2(1f, 1f), new Vector2(-30f, rowY), new Vector2(325f, 115f), new Vector2(1f, 0.5f), 50f, pack);
        }

        /// <summary>Preferred width of a cell's icon grid (60 x 60 cells, 2 columns, 5 apart).</summary>
        private static float GridWidth(int icons) => icons >= 2 ? 125f : 60f;

        /// <summary>RewardContainer_Bundle_Offers: beige box, icons on top, the brown strip (65 high) with the amount.</summary>
        private void RewardCell(RectTransform card, Vector2 position, Vector2 size, Sprite[] icons, string label)
        {
            var cell = Rect(card, "RewardContainer_Bundle_Offers", new Vector2(0f, 1f), new Vector2(0f, 1f), position, size, new Vector2(0f, 1f));
            Image(Stretch(cell, "Background"), _skin.Get("S_Offer_Dark_Beige_Inside_Bg"), Color.white, sliced: true);
            var strip = Rect(cell, "BottomLayer", Vector2.zero, new Vector2(1f, 0f), Vector2.zero, new Vector2(0f, 65f), new Vector2(0.5f, 0f));
            Image(Rect(strip, "Background", Vector2.zero, Vector2.one, new Vector2(0f, 1.25f), new Vector2(-5f, -2.5f)), _skin.Get("S_Rewards_Banner_0"), Hex("987C4DFF"), sliced: true).pixelsPerUnitMultiplier = 0.6f;
            Image(Rect(strip, "Blur", Vector2.zero, Vector2.one, new Vector2(0f, 1.25f), new Vector2(-5f, -2.5f)), _skin.Get("S_rewards_Banner_OutlineBlur_0"), Hex("824C1EFF"), sliced: true).pixelsPerUnitMultiplier = 0.65f;
            AutoSize(ShopText(Rect(strip, "Text (TMP)", Vector2.zero, Vector2.one, Vector2.zero, new Vector2(-10f, 0f)), label, "LilitaOne-Regular - Shop", 52.45f, Cream), 18f, 60f);
            // TopLayer: one icon shows big (the video's lives heart), several sit in the 2-column grid.
            var layer = Rect(cell, "TopLayer", Vector2.zero, Vector2.one, new Vector2(0f, 32.5f), new Vector2(0f, -65f));
            float iconSize = icons.Length == 1 ? 100f : 60f;
            int columns = Mathf.Min(2, icons.Length), rows = (icons.Length + 1) / 2;
            for (int i = 0; i < icons.Length; i++)
            {
                float ix = ((i % 2) - (columns - 1) * 0.5f) * (iconSize + 5f), iy = ((rows - 1) * 0.5f - i / 2) * (iconSize + 5f);
                Image(Rect(layer, "Icon", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(ix, iy), new Vector2(iconSize, iconSize)), icons[i], Color.white).preserveAspect = true;
            }
        }

        /// <summary>CoinPackShopItem_0 in the 3-column grid (cells 268.7 x 350, 20 apart, 10 below the banner).</summary>
        private void CoinCard(RectTransform section, int index, ShopPack pack)
        {
            const float cellWidth = 268.67f, cellHeight = 350f;
            float x = 25f + (index % 3) * (cellWidth + 20f), y = 115f + 10f + (index / 3) * (cellHeight + 20f);
            var card = Rect(section, "CoinPackShopItem", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(x, -y), new Vector2(cellWidth, cellHeight), new Vector2(0f, 1f));
            Image(Rect(card, "Shadow", Vector2.zero, Vector2.one, new Vector2(0f, -16.3f), new Vector2(20f, 17.6f)), _skin.Get("S_CircleShadow"), Hex("351600FF"), sliced: true).pixelsPerUnitMultiplier = 1.51f;
            Image(Stretch(card, "Image"), _skin.Get("CoinContainer"), Color.white);
            var coins = Rect(card, "CoinsContainer", Vector2.zero, Vector2.one, new Vector2(-0.37f, 64.76f), new Vector2(-35.46f, -168.58f));
            Image(Rect(coins, "RewardIcon", Vector2.zero, Vector2.one, new Vector2(0f, 22f), new Vector2(0f, -22f)), _skin.Get(pack.icon), Color.white).preserveAspect = true;
            AutoSize(ShopText(Rect(coins, "Text (TMP)", Vector2.zero, new Vector2(1f, 0f), new Vector2(0f, 2.4f), new Vector2(-20f, 57.3f), new Vector2(0.5f, 0f)), ShopCatalog.Amount(pack.coins), "LilitaOne-Regular - Shop", 50f, Cream), 18f, 60f);
            BuyButton(card, new Vector2(0.5f, 0f), new Vector2(0f, 11f), new Vector2(cellWidth - 22f, 102.5f), new Vector2(0.5f, 0f), 39f, pack);
        }

        /// <summary>The green price button (S_Btn_BG): "0 đ" [user], buying gives the pack and flies coins to the counter.</summary>
        private void BuyButton(RectTransform parent, Vector2 anchor, Vector2 position, Vector2 size, Vector2 pivot, float fontSize, ShopPack pack)
        {
            var button = Rect(parent, "PurchaseButton", anchor, anchor, position, size, pivot);
            var face = Image(button, _skin.Get("S_Btn_BG"), Color.white, sliced: true, raycast: true);
            face.pixelsPerUnitMultiplier = 1.7f; // the original's slice scale: at 1 its edge looked thick [user]
            AutoSize(ShopText(Rect(button, "PriceText (TMP)", Vector2.zero, Vector2.one, new Vector2(0f, -1.15f), new Vector2(-26f, -19.2f)), Price, "LilitaOne-Regular - Buy Btn", fontSize, Cream), 18f, fontSize);
            var click = button.gameObject.AddComponent<Button>();
            click.targetGraphic = face;
            click.onClick.AddListener(() => Buy(pack, button));
        }

        /// <summary>The video writes prices as "249.000 ₫"; LilitaOne may lack ₫, then "đ".</summary>
        private string Price => _skin.shopFont != null && _skin.shopFont.HasCharacter('₫', true, true) ? "0 ₫" : "0 đ";

        private void Buy(ShopPack pack, RectTransform from)
        {
            var economy = PlayerEconomy.Shared;
            int before = economy.Coins;
            pack.Grant(economy);
            economy.Report(MissionGoal.CompletePurchase); // a pack counts as a purchase, no payment here [user]
            SnackDash.Audio.SoundPlayer.Play("CollectCoin");
            if (pack.coins > 0) StartCoroutine(FlyCoins(from, before, economy.Coins));
        }

        /// <summary>A handful of coins burst from the button and fly into the counter, which counts up as they land.</summary>
        private IEnumerator FlyCoins(RectTransform from, int start, int end)
        {
            const int count = 8;
            var layer = (RectTransform)transform;
            Vector3 origin = from.TransformPoint(from.rect.center), target = _coinIcon.TransformPoint(_coinIcon.rect.center);
            for (int i = 0; i < count; i++)
            {
                var coin = Rect(layer, "FlyingCoin", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(80f, 80f));
                Image(coin, _skin.Get("S_Coin_Icon"), Color.white).preserveAspect = true;
                coin.position = origin;
                Vector3 burst = origin + (Vector3)(Random.insideUnitCircle * 120f * coin.lossyScale.x);
                bool last = i == count - 1;
                StartCoroutine(Fly(coin, burst, target, i * 0.05f, last ? () => CountTo(start, end) : (Action)null));
            }
            yield break;
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
            if (coin != null) Destroy(coin.gameObject);
            if (_coinIcon != null) StartCoroutine(Tween.Punch(0.06f, t => _coinIcon.localScale = Vector3.one * (1f + 0.2f * t)));
            landed?.Invoke();
        }

        private void CountTo(int start, int end)
        {
            if (_count != null) StopCoroutine(_count);
            _count = StartCoroutine(Tween.Run(0.5f, Tween.OutQuad, t => { _shownCoins = Mathf.RoundToInt(Mathf.Lerp(start, end, t)); _coins.text = _shownCoins.ToString(); }));
        }

        private void Update()
        {
            foreach (var spin in _rays) if (spin != null) spin.Rotate(0f, 0f, -20f * Time.unscaledDeltaTime); // RaysAnimation
        }

        // ------------------------------------------------------------------ text

        private TextMeshProUGUI ShopText(RectTransform rect, string text, string material, float size, Color color)
            => Text(rect, text, _skin.shopFont != null ? _skin.shopFont : _skin.font, _skin.ShopMaterial(material), size, color);

        private static TextMeshProUGUI AutoSize(TextMeshProUGUI text, float min, float max)
        {
            text.enableAutoSizing = true;
            text.fontSizeMin = min;
            text.fontSizeMax = max;
            return text;
        }
    }
}

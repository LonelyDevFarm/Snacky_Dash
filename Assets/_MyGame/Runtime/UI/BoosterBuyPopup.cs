using System;
using SnackDash.Economy;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static SnackDash.UI.UiBuilder;

namespace SnackDash.UI
{
    /// <summary>
    /// Boosters_Pop-up_Purchase (BoosterBuyUI in the original Game.unity), shown by the booster + when the
    /// stock is out: the coin counter top left, the tilted title with the booster's name, the big icon with
    /// "x3", its description, and Buy 1800 [user: 1800 coins for 3, every booster]. The original's video button
    /// is hidden (as it is when no ad is ready), so Buy sits alone in the middle. Short of coins: the Shop opens
    /// on top and its X comes back here [user].
    /// </summary>
    public static class BoosterBuyPopup
    {
        public static RectTransform Open(RectTransform canvas, UiSkin skin, string title, string description, Sprite icon, string stockKey, Action bought) =>
            Build(canvas, skin, "Boosters_Pop-up_Purchase", title, description.Replace("#8929F6", "#F59D00"), icon, "<size=85%>x</size>" + PlayerEconomy.BoosterPack,
                PlayerEconomy.BoosterPrice, 40f, 0.8f, true, () => { PlayerEconomy.Shared.AddBoosters(stockKey, PlayerEconomy.BoosterPack); bought?.Invoke(); });

        /// <summary>
        /// ExtraCratePurchasePopup: the same frame lower down (popup at -35, cover 83 %, no coin counter), the crate icon,
        /// "ADD A CRATE you can deliver into right away" and Buy 700 [user screenshot; the video button left out].
        /// </summary>
        public static RectTransform OpenExtraCrate(RectTransform canvas, UiSkin skin, Action paid) =>
            Build(canvas, skin, "ExtraCratePurchasePopup", "EXTRA CRATE", "<color=#8929F6>ADD A CRATE</color> YOU CAN DELIVER INTO RIGHT AWAY", skin.Get("S_Crate_Icon"), null,
                PlayerEconomy.ExtraCratePrice, -35f, 0.827f, false, paid);

        private static RectTransform Build(RectTransform canvas, UiSkin skin, string name, string title, string description, Sprite icon, string count,
            int cost, float popupY, float cover, bool coinCounter, Action paid)
        {
            var root = Stretch(canvas, name);
            Image(Stretch(root, "background overlay"), null, new Color(0f, 0f, 0f, cover), raycast: true); // plain black (original scene / prefab)
            if (coinCounter) CoinCounter(root, skin);

            var popup = Rect(root, "Popup Parent", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(1.75f, popupY), new Vector2(803f, 871f));
            BuildRest(root, popup, canvas, skin, title, description, icon, count, cost, paid);
            return root;
        }

        /// <summary>CurrencyHUD: white plate, coin, amount in blue.</summary>
        private static void CoinCounter(RectTransform root, UiSkin skin)
        {
            var hud = Rect(root, "CurrencyHUD", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(-235f, -200f), new Vector2(282.6f, 100f));
            hud.localScale = Vector3.one * 0.9f;
            Image(Rect(hud, "Image", Vector2.zero, Vector2.one, new Vector2(20.3f, -6f), new Vector2(-22.2f, -20f)), skin.whitePlate, Color.white, sliced: true);
            var amount = Text(Rect(hud, "Amount", new Vector2(0.5f, 0f), new Vector2(0.5f, 1f), new Vector2(17.5f, -6f), new Vector2(130.2f, -58.1f)), "", skin.font, skin.homeScreenLevelsMaterial, 40f, Hex("1066D4FF"));
            Image(Rect(hud, "Currency Icon", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-87.1f, -3f), new Vector2(90f, 90f)), skin.Get("JU-Texture-Common-Coin"), Color.white).preserveAspect = true;
            void ShowCoins() { if (amount != null) amount.text = PlayerEconomy.Shared.Coins.ToString(); }
            ShowCoins();
            PlayerEconomy.Shared.Changed += ShowCoins;
            root.gameObject.AddComponent<OnDestroyCallback>().Destroyed = () => PlayerEconomy.Shared.Changed -= ShowCoins;
        }

        private static void BuildRest(RectTransform root, RectTransform popup, RectTransform canvas, UiSkin skin, string title, string description, Sprite icon, string count, int cost, Action paid)
        {
            Image(Rect(popup, "Background Main Color", Vector2.zero, Vector2.one, new Vector2(0f, 115f), new Vector2(-100f, -70f)), skin.bannerFrame, Hex("D2E3FFFF"), sliced: true);
            Image(Rect(popup, "SP_BG_Title", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(-12f, 58f), new Vector2(600f, 178f)), skin.titleBanner, Color.white);
            var header = Text(Rect(popup, "Text (TMP) - header", new Vector2(0f, 1f), Vector2.one, new Vector2(-10f, 51f), new Vector2(-260f, 114f)), title, skin.font, skin.blueUnderlayMaterial, 72f, Color.white);
            header.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 2.54f);

            var content = Rect(popup, "Popup Content", new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0f, 171f), new Vector2(-20f, 509.4f));
            var contentBack = Rect(content, "Popup Content Background", Vector2.zero, Vector2.one, Vector2.zero, new Vector2(-80f, 0f));
            Image(Rect(contentBack, "popup content background color", new Vector2(0.0285f, 0f), new Vector2(0.9715f, 1f), new Vector2(0f, -21.1f), new Vector2(0f, -18.9f)), skin.bannerFrame, Color.white, sliced: true);
            var text = Text(Rect(content, "Text (TMP) - sub text", Vector2.zero, new Vector2(1f, 0f), new Vector2(0f, 40.4f), new Vector2(-160f, 66f)), description, skin.font, skin.plainMaterial, 33.45f, Hex("446890FF"));
            text.enableWordWrapping = true;
            text.enableAutoSizing = true;
            text.fontSizeMin = 20f;
            text.fontSizeMax = 33.45f;
            var inner = Rect(content, "Content", Vector2.zero, Vector2.one, new Vector2(-2.8f, 0f), new Vector2(-129.3f, 0f));
            Image(Rect(inner, "SP_Icon", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 31f), new Vector2(312.1f, 277.3f)), icon, Color.white).preserveAspect = true;
            if (count != null)
                Text(Rect(inner, "Text (TMP) Boosters", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(130f, -75f), new Vector2(150f, 130f)), count, skin.font, skin.plainMaterial, 100f, Hex("F59D00FF"));

            // Buttons Area: View_Button_Buy_Currency (311.7 x 210), "Buy" above the coin and the price.
            var area = Rect(popup, "Buttons Area", Vector2.zero, new Vector2(1f, 0.362f), new Vector2(0f, 54.8f), new Vector2(-100f, 85.5f), new Vector2(0.5f, 0.6405f));
            // The row puts its buttons 157.9 under the area's top (both originals sit there); alone, Buy is centred.
            var buy = Rect(area, "View_Button_Buy_Currency", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -157.9f), new Vector2(311.7f, 210f));
            var visual = Rect(buy, "Visual_Container", new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), Vector2.zero, new Vector2(0f, 210f));
            var sheets = skin.bigButton;
            var face = Image(Rect(visual, "Base", new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), Vector2.zero, new Vector2(-16.06f, 144f)), sheets[0], Hex("399F00FF"), sliced: true, raycast: true);
            Image(Rect(visual, "Front", new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0f, 8.19f), new Vector2(-20.27f, 124.99f)), sheets[1], Hex("4FCD04FF"), sliced: true);
            Image(Rect(visual, "Highlight", new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0.97f, -42.49f), new Vector2(-33.86f, 23.62f)), sheets[2], Hex("78F024FF"), sliced: true);
            Image(Rect(visual, "Outline", new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), Vector2.zero, new Vector2(-16.06f, 144.22f)), sheets[3], Hex("2D8700FF"), sliced: true);
            var labels = Rect(visual, "Tex_Container", new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0f, 6.33f), new Vector2(-119.5f, 121.25f));
            Text(Rect(labels, "Text (TMP)", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -12f), new Vector2(175f, 45f), new Vector2(0.5f, 1f)), "Buy", skin.font, skin.greenOutlineMaterial, 45f, Color.white);
            var price = Rect(labels, "Value (TMP)", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(12.8f, 8f), new Vector2(131.7f, 60f), new Vector2(0.5f, 0f));
            Text(price, cost.ToString(), skin.font, skin.greenOutlineMaterial, 60f, Color.white);
            Image(Rect(price, "GenericIcon", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(-28f, 0f), new Vector2(57.8f, 58.9f)), skin.Get("S_Icon_Coin_Single"), Color.white).preserveAspect = true;
            var button = buy.gameObject.AddComponent<Button>();
            button.targetGraphic = face;
            button.onClick.AddListener(() =>
            {
                var economy = PlayerEconomy.Shared;
                if (!economy.TrySpend(cost)) { ShopView.Open(canvas, skin, 0f, () => { }); return; }
                UnityEngine.Object.Destroy(root.gameObject);
                paid?.Invoke();
            });

            var close = Rect(popup, "View_Close_Button", Vector2.one, Vector2.one, new Vector2(-58.9f, 69.4f), new Vector2(86f, 86f));
            Image(close, skin.closeSquare, Color.white, sliced: true, raycast: true);
            Text(Rect(close, "X (TMP)", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 6.5f), new Vector2(86f, 72f)), "X", skin.font, skin.redCartoonMaterial, 74f, Color.white);
            close.gameObject.AddComponent<Button>().onClick.AddListener(() => UnityEngine.Object.Destroy(root.gameObject));

            CentreOnLetters(root);
            Pressable(root);
        }

        /// <summary>Runs a callback when its object goes (unhooks the coin counter).</summary>
        private sealed class OnDestroyCallback : MonoBehaviour
        {
            public Action Destroyed;
            private void OnDestroy() => Destroyed?.Invoke();
        }
    }
}

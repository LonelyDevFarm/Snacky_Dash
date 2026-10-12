using System;
using System.Collections;
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
    /// The prize of a finished Jungle Heist run (original Resources/Popup_Lava_Quest_Rewards, LavaQuestRewardsPopupView):
    /// on black, the title between its leaves, the grand prize chest, "You win" with the coin pack and the share, "You are
    /// sharing the reward with N other winners!" over the finishers' pictures (yours first) and "Tap to continue!"; the tap
    /// pays the share and its coins fly into Home's counter. Content is the original VerticalLayoutGroup (padding 262 / 377,
    /// all four rows stretched, the title asking 36.7). Prefab Prefabs/UI/JungleHeistRewards [user].
    /// </summary>
    public class JungleHeistRewards : MonoBehaviour
    {
        private Image _cover;
        private float _coverAlpha;
        private TextMeshProUGUI _tap;
        private RectTransform _coins;
        private bool _ready, _closing;
        private Action _closed;

        public static void Open(RectTransform parent, UiSkin skin, GameObject prefab, Action closed)
        {
            RectTransform root;
            if (prefab != null)
            {
                root = (RectTransform)Instantiate(prefab, parent).transform;
                root.name = "Popup_Lava_Quest_Rewards";
            }
            else
            {
                root = Stretch(parent, "Popup_Lava_Quest_Rewards");
                BuildLayout(root, skin);
            }
            var view = root.GetComponent<JungleHeistRewards>();
            if (view == null) view = root.gameObject.AddComponent<JungleHeistRewards>();
            view._closed = closed;
            view.Bind(root, skin);
        }

        // ================================================================== layout (saved as the prefab)

        public static void BuildLayout(RectTransform root, UiSkin skin)
        {
            if (root.GetComponent<JungleHeistRewards>() == null) root.gameObject.AddComponent<JungleHeistRewards>();
            var content = BlackRows(root, skin);

            // Reward: the grand prize chest and its sign.
            var reward = Stretch(content, "Reward");
            // STAND-IN scales: the prefab's saved row sizes are stale and overlap; on the 1920 high canvas each row gets
            // 311, so the chest (0.75) and the coin pack (0.55, under "You win") are shrunk to stay in their rows.
            var icons = Centre(reward, "Icons", 0f, -1f, 470.7f, 436.8f);
            icons.localScale = Vector3.one * 0.75f;
            Image(icons, skin.Get("S_GrandPrize_LavaQuest_0"), Color.white).preserveAspect = true;
            var grand = Text(Centre(icons, "Text (TMP)", 89.8f, -58.7f, 137.9f, 55.9f), "Grand Prize\n<size=140%>10 000</size>", skin.font, skin.EventMaterial("LuckiestGuy-Regular LavaQuest Counter"), 28.4f, Color.white);
            grand.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 5.8f);
            AutoSize(grand, 28.4f);

            // Rewards: "You win" and the coin pack with the share under it.
            var rewards = Stretch(content, "Rewards");
            AutoSize(Text(Rect(rewards, "Text (TMP)", new Vector2(0f, 1f), Vector2.one, new Vector2(0f, -40.9f), new Vector2(0f, 81.9f)), "You win", skin.font, skin.plainMaterial, 58.2f, Color.white), 58.2f);
            var pack = Centre(rewards, "Offer_BuyAll_RewardView", 0f, -25f, 434.5f, 434.5f);
            pack.localScale = Vector3.one * 0.55f;
            var coins = Stretch(pack, "Icon");
            Image(coins, skin.Get("S_Coins_Medium_Pack_1_0"), Color.white).preserveAspect = true;
            var shadow = coins.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.298f);
            shadow.effectDistance = new Vector2(1f, -6.6f);
            AutoSize(Text(Rect(coins, "Amount", Vector2.zero, new Vector2(1f, 0f), Vector2.zero, new Vector2(0f, 95.3f)), "500", skin.font, skin.EventMaterial("LuckiestGuy-Regular Brown Cartoon"), 96.8f, Color.white), 96.8f);

            // Players_Frames: the sharing line over the row of finishers (Players_Icon, filled when shown).
            var players = Stretch(content, "Players_Frames");
            var sharing = Text(Rect(players, "Text (TMP)", new Vector2(0f, 1f), Vector2.one, new Vector2(0f, -40.9f), new Vector2(-71.9f, 81.9f)), "You are sharing the reward with 4 other winners!", skin.font, skin.plainMaterial, 58.2f, Color.white);
            AutoSize(sharing, 58.2f);
            Rect(players, "Players_Icon", Vector2.zero, new Vector2(1f, 0f), new Vector2(0f, 90.06f), new Vector2(-71.9f, 180.1f));

            Text(Rect(root, "Tap_To_Claim", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 184f), new Vector2(950f, 194.3f)), "Tap to continue!", skin.font, skin.plainMaterial, 44f, new Color(0.553f, 0.569f, 0.588f));
            CentreOnLetters(root);
        }

        private static void AutoSize(TextMeshProUGUI text, float max)
        {
            text.richText = true;
            text.enableAutoSizing = true;
            text.fontSizeMin = 18f;
            text.fontSizeMax = max;
        }

        // ================================================================== behaviour

        private void Bind(RectTransform root, UiSkin skin)
        {
            _cover = root.GetComponent<Image>();
            _coverAlpha = _cover.color.a;
            var button = root.GetComponent<Button>();
            if (button == null) button = root.gameObject.AddComponent<Button>();
            button.onClick.AddListener(OnTap);
            _tap = root.Find("Tap_To_Claim").GetComponent<TextMeshProUGUI>();
            _coins = (RectTransform)root.Find("Content/Rewards/Offer_BuyAll_RewardView/Icon");

            int winners = JungleHeist.SurvivorsPerStep[JungleHeist.Steps];
            _coins.Find("Amount").GetComponent<TextMeshProUGUI>().text = JungleHeist.Share.ToString();
            root.Find("Content/Players_Frames/Text (TMP)").GetComponent<TextMeshProUGUI>().text = $"You are sharing the reward with {winners - 1} other winners!";

            // The finishers, yours first (PlayerAvatarIndex 0), 180 wide 25 apart; the row shrinks to fit the width.
            var row = (RectTransform)root.Find("Content/Players_Frames/Players_Icon");
            for (int i = 0; i < winners; i++)
                Avatar(row, skin, i == 0 ? "View_LavaQuest_Avatar_Player" : "View_LavaQuest_Avatar_" + i, new Vector2((i - (winners - 1) * 0.5f) * 205f, 0f), 1.8f, i == 0 ? 5 : i, i == 0);
            UnityEngine.Canvas.ForceUpdateCanvases();
            float wide = winners * 180f + (winners - 1) * 25f;
            if (wide > row.rect.width) row.localScale = Vector3.one * (row.rect.width / wide);
            StartCoroutine(Reveal(root));
        }

        /// <summary>Darken, then the title, the chest, the prize and the finishers pop in turn, and "Tap to continue!".</summary>
        private IEnumerator Reveal(RectTransform root)
        {
            var row = root.Find("Content/Players_Frames/Players_Icon");
            var parts = new System.Collections.Generic.List<(Transform part, float at)>
            {
                (root.Find("Content/Title/Header"), 0.1f), (root.Find("Content/Reward/Icons"), 0.35f), (root.Find("Content/Rewards"), 0.6f), (root.Find("Content/Players_Frames/Text (TMP)"), 0.85f),
            };
            for (int i = 0; i < row.childCount; i++) parts.Add((row.GetChild(i), 1f + 0.08f * i));
            float end = parts[parts.Count - 1].at + 0.3f;
            var scales = new Vector3[parts.Count];
            for (int i = 0; i < parts.Count; i++) { scales[i] = parts[i].part.localScale; parts[i].part.localScale = Vector3.zero; }
            float tapAlpha = _tap.alpha;
            _tap.alpha = 0f;
            for (float t = 0f; t < end + 0.2f && !_ready; t += Time.unscaledDeltaTime)
            {
                _cover.color = new Color(0f, 0f, 0f, _coverAlpha * Mathf.Clamp01(t / 0.2f));
                for (int i = 0; i < parts.Count; i++)
                    parts[i].part.localScale = scales[i] * Tween.OutBack(1.7f)(Mathf.Clamp01((t - parts[i].at) / 0.25f));
                _tap.alpha = Mathf.Clamp01((t - end) / 0.2f) * tapAlpha;
                yield return null;
            }
            _cover.color = new Color(0f, 0f, 0f, _coverAlpha);
            for (int i = 0; i < parts.Count; i++) parts[i].part.localScale = scales[i];
            _tap.alpha = tapAlpha;
            _ready = true;
        }

        private void OnTap()
        {
            if (_closing) return;
            if (!_ready) { _ready = true; return; }
            _closing = true;
            StartCoroutine(Close());
        }

        /// <summary>PlayCoinFlyThenClose: the share goes to the wallet and its coins fly from the pack into Home's counter.</summary>
        private IEnumerator Close()
        {
            if (JungleHeist.Shared.ClaimPrize())
            {
                var home = FindObjectOfType<HomeView>();
                if (home != null) home.FlyCoins(_coins.position, JungleHeist.Share);
            }
            var group = gameObject.AddComponent<CanvasGroup>();
            yield return Tween.Run(0.25f, Tween.Linear, t => group.alpha = 1f - t, true);
            Destroy(gameObject);
            _closed?.Invoke();
        }
    }
}

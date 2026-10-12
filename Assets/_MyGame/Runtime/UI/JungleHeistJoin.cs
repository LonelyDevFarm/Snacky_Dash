using System;
using System.Collections;
using SnackDash.Presentation;
using UnityEngine;
using UnityEngine.UI;
using static SnackDash.UI.UiBuilder;
using static SnackDash.UI.JungleHeistUi;

namespace SnackDash.UI
{
    /// <summary>
    /// "Jungle Heist is live!" (original Resources/Popup_Lava_Quest_Join): the brown frame under the leaves and the title,
    /// the treasure boat picture with its glow, turning rays and "Grand prize 10 000", the words and JOIN. The layout is
    /// the prefab Prefabs/UI/JungleHeistJoin [user]. It pops in and shrinks away like the other event popups.
    /// </summary>
    public class JungleHeistJoin : MonoBehaviour
    {
        private RectTransform _popup, _rays;
        private Image _overlay;
        private float _overlayAlpha;
        private bool _closing;

        public static void Open(RectTransform parent, UiSkin skin, GameObject prefab, Action joined, Action closed = null)
        {
            RectTransform root;
            if (prefab != null)
            {
                root = (RectTransform)Instantiate(prefab, parent).transform;
                root.name = "Popup_Lava_Quest_Join";
            }
            else
            {
                root = Stretch(parent, "Popup_Lava_Quest_Join");
                BuildLayout(root, skin);
            }
            var view = root.GetComponent<JungleHeistJoin>();
            if (view == null) view = root.gameObject.AddComponent<JungleHeistJoin>();
            view.Bind(root, joined, closed);
        }

        public static void BuildLayout(RectTransform root, UiSkin skin)
        {
            if (root.GetComponent<JungleHeistJoin>() == null) root.gameObject.AddComponent<JungleHeistJoin>();
            var popup = Frame(root, skin, out var mask);
            var visual = Stretch(mask, "Offer_Visual");
            Image(visual, skin.Get("Tex_Jungle_Heist_Start_Illustration_V2_0"), Color.white).preserveAspect = false;
            var additive = skin.uiAdditive;
            var glow = Image(Centre(visual, "FX_Background_Glow", 142f, -415.9f + 300f, 1392f, 836f), skin.Get("TX_FX_Glow"), new Color(1f, 0.646f, 0f));
            glow.material = additive;
            var rays = Centre(visual, "FX_Rays_Scalar", 240f, -188f + 300f, 1000f, 1000f);
            rays.localScale = new Vector3(0.9f, 0.7f, 1f);
            Image(Stretch(rays, "FX_Background_Rays"), skin.Get("Tex_FX_Rays"), new Color(1f, 0.932f, 0.335f)).material = additive;
            Image(Centre(visual, "Image_Boat_Overlay", 53.5f, -348.7f + 300f, 1100f, 508.2f), skin.Get("S_Jungle_Heist_Start_Boat_0"), Color.white).preserveAspect = true;
            var grant = Text(Centre(mask, "Grant_Text", 196f, 135f, 208.9f, 111.5f), "Grand prize\n<size=140%>10 000</size>", skin.font, skin.EventMaterial("LuckiestGuy-Regular Brown Cartoon"), 33.1f, Color.white);
            grant.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -8f);
            var top = Centre(mask, "TopGreenGradian", 0f, 464.8f, 782.6f, 319.2f);
            top.localRotation = Quaternion.Euler(0f, 0f, 180f);
            Image(top, skin.Get("S_LinearGradiant_0"), new Color(0.216f, 0.11f, 0.039f));
            Image(Rect(mask, "BottomGradient", Vector2.zero, new Vector2(1f, 0f), new Vector2(0f, 272f), new Vector2(0f, 544.4f)), skin.Get("S_Gradient_Overlay_LavaQuest"), new Color(0.165f, 0.114f, 0.031f), sliced: true);
            Inlay(popup, skin);

            var hint = Text(Centre(root, "Hint_Text", 0f, -317f, 748f, 202f),
                "<size=62><color=#FED025>Jungle Heist</color> is live!</size>\nWin 7 levels in a row to complete the challenge!", skin.font, skin.EventMaterial("LuckiestGuy-Regular Brown Cartoon"), 37f, Color.white);
            hint.richText = true;
            hint.enableWordWrapping = true;
            GreenButton(root, skin, "Join");
            CentreOnLetters(root);
        }

        private void Bind(RectTransform root, Action joined, Action closed)
        {
            _overlay = root.Find("Overlay").GetComponent<Image>();
            _overlayAlpha = _overlay.color.a;
            _popup = (RectTransform)root.Find("Popup_Container");
            _rays = root.Find("Popup_Container/Background/Image_Mask/Offer_Visual/FX_Rays_Scalar") as RectTransform;
            AddClick(_popup.Find("Close_Button"), () => StartCoroutine(Close(closed)));
            AddClick(root.Find("PlayButton/View_Button_Play_Base/Front"), () => StartCoroutine(Close(joined)));
            Pressable(root);
            StartCoroutine(PopIn());
        }

        private static void AddClick(Transform target, Action action)
        {
            if (target == null) return;
            var button = target.GetComponent<Button>();
            if (button == null) button = target.gameObject.AddComponent<Button>();
            button.onClick.AddListener(() => action());
        }

        private IEnumerator PopIn()
        {
            var scale = _popup.localScale;
            Action<float> step = t =>
            {
                _overlay.color = new Color(0f, 0f, 0f, _overlayAlpha * Mathf.Clamp01(t * 2f));
                _popup.localScale = scale * Tween.OutBack(1.6f)(t);
            };
            step(0f);
            yield return Tween.Run(0.35f, Tween.Linear, step, true);
        }

        private IEnumerator Close(Action then)
        {
            if (_closing) yield break;
            _closing = true;
            var scale = _popup.localScale;
            yield return Tween.Run(0.22f, Tween.Linear, t =>
            {
                _popup.localScale = scale * (1f - Tween.InBack(1.5f)(t));
                _overlay.color = new Color(0f, 0f, 0f, _overlayAlpha * (1f - t));
            }, true);
            Destroy(gameObject);
            then?.Invoke();
        }

        private void Update()
        {
            if (_rays != null) _rays.localRotation *= Quaternion.Euler(0f, 0f, -12f * Time.unscaledDeltaTime);
        }
    }
}

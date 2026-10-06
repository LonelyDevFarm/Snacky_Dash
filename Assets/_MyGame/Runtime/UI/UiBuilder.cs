using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SnackDash.UI
{
    /// <summary>Small helpers to build uGUI hierarchies in code from the original sprites.</summary>
    public static class UiBuilder
    {
        /// <summary>Screen-space canvas at the original reference resolution (1080x1920, match height).</summary>
        public static RectTransform Canvas(Transform parent, string name, int sortingOrder)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            go.transform.SetParent(parent, false);
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.matchWidthOrHeight = 1f;
            return (RectTransform)go.transform;
        }

        public static RectTransform Rect(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 position, Vector2 size, Vector2? pivot = null)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot ?? new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return rect;
        }

        public static RectTransform Stretch(Transform parent, string name) => Rect(parent, name, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        public static Image Image(RectTransform rect, Sprite sprite, Color color, bool sliced = false, bool raycast = false)
        {
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.type = sliced ? UnityEngine.UI.Image.Type.Sliced : UnityEngine.UI.Image.Type.Simple;
            image.raycastTarget = raycast;
            return image;
        }

        public static TextMeshProUGUI Text(RectTransform rect, string text, TMP_FontAsset font, Material material, float size, Color color)
        {
            var label = rect.gameObject.AddComponent<TextMeshProUGUI>();
            label.text = text;
            if (font != null) label.font = font;
            if (material != null) label.fontSharedMaterial = material;
            label.fontSize = size;
            label.color = color;
            label.alignment = TextAlignmentOptions.Center;
            label.enableWordWrapping = false;
            label.raycastTarget = false;
            return label;
        }

        /// <summary>
        /// LuckiestGuy's line box sits low, so line-centred text rides up to the top edge of buttons and bars
        /// [user]: centre labels on the letters themselves (Midline) instead.
        /// </summary>
        public static void CentreOnLetters(Transform root)
        {
            foreach (var label in root.GetComponentsInChildren<TMP_Text>(true))
                if (label.alignment == TextAlignmentOptions.Center) label.alignment = TextAlignmentOptions.Midline;
        }

        /// <summary>Every Button under <paramref name="root"/> dips when pressed, clicks and buzzes lightly (PressFeedback).</summary>
        public static void Pressable(Transform root)
        {
            foreach (var button in root.GetComponentsInChildren<UnityEngine.UI.Button>(true))
                if (button.GetComponent<PressFeedback>() == null) button.gameObject.AddComponent<PressFeedback>();
        }

        public static Color Hex(string rgba) => ColorUtility.TryParseHtmlString("#" + rgba, out var color) ? color : Color.white;
    }
}

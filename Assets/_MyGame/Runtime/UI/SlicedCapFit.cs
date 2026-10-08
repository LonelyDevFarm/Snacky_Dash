using UnityEngine;
using UnityEngine.UI;

namespace SnackDash.UI
{
    /// <summary>
    /// Keeps a sliced image's rounded corners and end caps round when its rect is smaller than the sprite's borders.
    /// Unity then squeezes the borders on that axis alone, which draws pointed ends [user: "bo nhọn 2 đầu"]. This scales
    /// the borders evenly (pixelsPerUnitMultiplier) until they fit both ways. A sprite with side caps only (no top or
    /// bottom border, e.g. ProgressBar_BG: 256 high, 128 caps) has caps as tall as the sprite, so they follow the height.
    /// </summary>
    [RequireComponent(typeof(Image))]
    public class SlicedCapFit : MonoBehaviour
    {
        private Image _image;

        private void OnEnable() => Fit();
        private void Start() => Fit();
        private void OnRectTransformDimensionsChange() => Fit();

        private void Fit()
        {
            if (_image == null) _image = GetComponent<Image>();
            if (_image == null || _image.sprite == null) return;
            var rect = ((RectTransform)transform).rect;
            if (rect.width <= 0f || rect.height <= 0f) return;
            var sprite = _image.sprite;
            float units = 100f / sprite.pixelsPerUnit; // canvas units per sprite pixel (reference 100 px per unit)
            var border = sprite.border; // left, bottom, right, top
            float vertical = border.y + border.w > 0f ? border.y + border.w : sprite.rect.height;
            float horizontal = border.x + border.z > 0f ? border.x + border.z : 0f;
            float multiplier = Mathf.Max(1f, vertical * units / rect.height, horizontal * units / rect.width);
            if (!Mathf.Approximately(_image.pixelsPerUnitMultiplier, multiplier)) _image.pixelsPerUnitMultiplier = multiplier;
        }

        /// <summary>Adds the fit to every sliced image under <paramref name="root"/>.</summary>
        public static void AddTo(Transform root)
        {
            foreach (var image in root.GetComponentsInChildren<Image>(true))
                if (image.type == Image.Type.Sliced && image.GetComponent<SlicedCapFit>() == null) image.gameObject.AddComponent<SlicedCapFit>();
        }
    }
}

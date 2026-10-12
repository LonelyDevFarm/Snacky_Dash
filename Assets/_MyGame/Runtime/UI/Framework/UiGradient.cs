using UnityEngine;
using UnityEngine.UI;

namespace SnackDash.UI
{
    /// <summary>
    /// A top-to-bottom colour fade over a graphic (multiplied with its colour): the Snacky Pass lanes, which go a shade
    /// lighter or darker downward in the videos (the original draws them with layered glows).
    /// </summary>
    [RequireComponent(typeof(Graphic))]
    public class UiGradient : BaseMeshEffect
    {
        public Color top = Color.white, bottom = Color.white;

        public static UiGradient Add(Graphic target, Color top, Color bottom)
        {
            var gradient = target.GetComponent<UiGradient>();
            if (gradient == null) gradient = target.gameObject.AddComponent<UiGradient>();
            gradient.Set(top, bottom);
            return gradient;
        }

        public void Set(Color top, Color bottom)
        {
            this.top = top;
            this.bottom = bottom;
            if (graphic != null) graphic.SetVerticesDirty();
        }

        public override void ModifyMesh(VertexHelper vh)
        {
            if (!IsActive()) return;
            var rect = graphic.rectTransform.rect;
            if (rect.height <= 0f) return;
            var vertex = new UIVertex();
            for (int i = 0; i < vh.currentVertCount; i++)
            {
                vh.PopulateUIVertex(ref vertex, i);
                float t = Mathf.Clamp01((vertex.position.y - rect.yMin) / rect.height);
                vertex.color = (Color)vertex.color * Color.Lerp(bottom, top, t);
                vh.SetUIVertex(vertex, i);
            }
        }
    }
}

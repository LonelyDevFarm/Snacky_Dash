using UnityEngine;
using UnityEngine.UI;

namespace SnackDash.UI
{
    /// <summary>
    /// A band of light sweeping over a graphic and again after a pause, as UIEffect's UIShiny in the original (claimable
    /// mission rows, unlocked Home widgets and their CLAIM bars). Writes each vertex's place across the graphic (along the
    /// rotation) in TEXCOORD1.x for the UI/Shiny shader and moves the band.
    /// </summary>
    [RequireComponent(typeof(Graphic))]
    public class UiShiny : BaseMeshEffect
    {
        /// <summary>The UI/Shiny shader (UiSkin.shinyShader, set by Home; found by name in the editor).</summary>
        public static Shader Shader;

        public float width = 0.25f, rotation = 108f, softness = 0.575f, brightness = 0.217f, gloss = 0.498f;
        public float duration = 1.3f, loopDelay = 1.5f;

        private Material _material;
        private float _time;

        /// <summary>Main.unity's widget icons and CLAIM bar fills: width 0.25, 108°, softness 0.575, brightness 0.217, gloss 0.498, 1.3 s every 2.8 s.</summary>
        public static UiShiny Widget(Graphic target) => Add(target, 0.25f, 108f, 0.575f, 0.217f, 0.498f, 1.3f, 1.5f);

        /// <summary>The Mission prefab's claimable row: width 0.204, 45°, softness 0.541, brightness 0.157, gloss 1, 1.1 s every 3.1 s.</summary>
        public static UiShiny Row(Graphic target) => Add(target, 0.204f, 45f, 0.541f, 0.157f, 1f, 1.1f, 2f);

        public static UiShiny Add(Graphic target, float width, float rotation, float softness, float brightness, float gloss, float duration, float loopDelay)
        {
            if (target == null) return null;
            var shiny = target.GetComponent<UiShiny>();
            if (shiny == null) shiny = target.gameObject.AddComponent<UiShiny>();
            shiny.width = width; shiny.rotation = rotation; shiny.softness = softness; shiny.brightness = brightness; shiny.gloss = gloss;
            shiny.duration = duration; shiny.loopDelay = loopDelay;
            shiny.graphic.SetVerticesDirty();
            return shiny;
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            var shader = Shader != null ? Shader : UnityEngine.Shader.Find("UI/Shiny");
            if (shader == null) return;
            if (_material == null) _material = new Material(shader) { name = "UI/Shiny (instance)" };
            graphic.material = _material;
            var canvas = graphic.canvas;
            if (canvas != null) canvas.rootCanvas.additionalShaderChannels |= AdditionalCanvasShaderChannels.TexCoord1;
        }

        protected override void OnDisable()
        {
            if (graphic != null && graphic.material == _material) graphic.material = null;
            base.OnDisable();
        }

        protected override void OnDestroy()
        {
            if (_material != null) Destroy(_material);
            base.OnDestroy();
        }

        public override void ModifyMesh(VertexHelper vh)
        {
            if (!IsActive()) return;
            var rect = graphic.rectTransform.rect;
            // UIShiny's rotation is the band's own angle; it sweeps across it (108: a steep band going left to right) [user].
            float rad = (rotation - 90f) * Mathf.Deg2Rad;
            var dir = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
            float half = (Mathf.Abs(rect.width * dir.x) + Mathf.Abs(rect.height * dir.y)) * 0.5f;
            if (half <= 0f) return;
            var vertex = new UIVertex();
            for (int i = 0; i < vh.currentVertCount; i++)
            {
                vh.PopulateUIVertex(ref vertex, i);
                float along = Vector2.Dot((Vector2)vertex.position - rect.center, dir);
                vertex.uv1 = new Vector4(along / (2f * half) + 0.5f, 0f, 0f, 0f);
                vh.SetUIVertex(vertex, i);
            }
        }

        private void Update()
        {
            if (_material == null) return;
            _time += Time.unscaledDeltaTime;
            float cycle = duration + loopDelay;
            float t = (_time % cycle) / duration;
            _material.SetFloat("_ShinyLocation", t <= 1f ? t : 2f); // off the graphic while it waits
            _material.SetFloat("_ShinyWidth", width);
            _material.SetFloat("_ShinySoftness", softness);
            _material.SetFloat("_ShinyBrightness", brightness);
            _material.SetFloat("_ShinyGloss", gloss);
        }
    }
}

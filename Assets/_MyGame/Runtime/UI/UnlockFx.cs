using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using static SnackDash.UI.UiBuilder;

namespace SnackDash.UI
{
    /// <summary>
    /// The light behind an unlocked mechanic on the win screen: FX_UI_Highlight_Rays_Golden of the original VictoryPopup
    /// (scale 3.35 behind LDfeature_Icon). An orange glow (TX_FX_Glow 256, 1 / 0.573 / 0), two gold ray fans (TX_FX_Flare
    /// 215 and TX_FX_Flare_1 256 flipped, 1 / 0.807 / 0) turning a full round in 20 s in opposite ways (its Rotation
    /// feedbacks), and two ParticleImage emitters on a circle of radius 50: dots (TX_FX_Sparkle, 3 a second) and sparkles
    /// (circle_glow_fx, 6 a second), each living 1-1.3 s, 15-25 big, growing in and out. All additive.
    /// </summary>
    public class UnlockFx : MonoBehaviour
    {
        private const float Scale = 3.35f, Radius = 50f, RoundSeconds = 20f;

        private RectTransform _raysA, _raysB;
        private readonly List<Emitter> _emitters = new List<Emitter>();

        private class Emitter
        {
            public Sprite sprite;
            public float rate, timer;
            public readonly List<(RectTransform rect, Image image, float age, float life, float size)> live = new List<(RectTransform, Image, float, float, float)>();
        }

        public static UnlockFx Add(RectTransform parent, Vector2 position, Material additive, Sprite glow, Sprite raysA, Sprite raysB, Sprite dot, Sprite sparkle)
        {
            var root = Rect(parent, "FX_UI_Highlight_Rays_Golden", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), position, new Vector2(100f, 100f));
            root.localScale = Vector3.one * Scale;
            var fx = root.gameObject.AddComponent<UnlockFx>();
            var scaler = Stretch(root, "Image_Scaler");
            Layer(scaler, "FX_Image_Glow", glow, 256f, new Color(1f, 0.573f, 0f), additive);
            fx._raysA = Layer(scaler, "FX_Image_Rays_A", raysA, 215f, new Color(1f, 0.807f, 0f), additive);
            fx._raysB = Layer(scaler, "FX_Image_Rays_B", raysB, 256f, new Color(1f, 0.807f, 0f), additive);
            fx._raysB.localScale = new Vector3(1f, -1f, 1f);
            fx.Emit(dot, 3f);
            fx.Emit(sparkle, 6f);
            return fx;
        }

        private static RectTransform Layer(RectTransform parent, string name, Sprite sprite, float size, Color colour, Material material)
        {
            var rect = Rect(parent, name, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(size, size));
            var image = Image(rect, sprite, colour);
            image.material = material;
            return rect;
        }

        private Material _material;

        private void Emit(Sprite sprite, float rate)
        {
            if (sprite == null) return;
            _emitters.Add(new Emitter { sprite = sprite, rate = rate });
        }

        private void Start() => _material = transform.Find("Image_Scaler/FX_Image_Glow")?.GetComponent<Image>()?.material;

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            float turn = 360f / RoundSeconds * dt;
            if (_raysA != null) _raysA.localRotation *= Quaternion.Euler(0f, 0f, -turn);
            if (_raysB != null) _raysB.localRotation *= Quaternion.Euler(0f, 0f, turn);
            foreach (var emitter in _emitters)
            {
                emitter.timer += dt;
                while (emitter.timer >= 1f / emitter.rate)
                {
                    emitter.timer -= 1f / emitter.rate;
                    Spawn(emitter);
                }
                for (int i = emitter.live.Count - 1; i >= 0; i--)
                {
                    var p = emitter.live[i];
                    p.age += dt;
                    if (p.age >= p.life) { Destroy(p.rect.gameObject); emitter.live.RemoveAt(i); continue; }
                    float k = p.age / p.life;
                    float grow = Mathf.Sin(k * Mathf.PI); // in and out over its life
                    p.rect.sizeDelta = Vector2.one * p.size * grow;
                    p.rect.anchoredPosition += p.rect.anchoredPosition.normalized * 0.75f * dt; // startSpeed 0.5-1, outward
                    emitter.live[i] = p;
                }
            }
        }

        private void Spawn(Emitter emitter)
        {
            float angle = Random.value * Mathf.PI * 2f;
            var at = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * Radius; // on the circle's edge (emit on surface)
            var rect = Rect(transform, "Particle", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), at, Vector2.zero);
            rect.localRotation = Quaternion.Euler(0f, 0f, Random.Range(-90f, 0f));
            var image = Image(rect, emitter.sprite, Color.white);
            image.material = _material;
            emitter.live.Add((rect, image, 0f, Random.Range(1f, 1.3f), Random.Range(15f, 25f)));
        }
    }
}

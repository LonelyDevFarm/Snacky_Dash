using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace SnackDash.UI
{
    /// <summary>
    /// A small particle emitter drawn as UI images, for the original's ParticleSystems that sit in canvases through
    /// UIParticle (not in this project): particles start on the shell of a box (or at the centre), fly a random way,
    /// turn, wiggle (the Noise module, roughly), take a random colour between two, fade out (and shrink, if asked)
    /// over their life. A burst fires once when the emitter starts; a rate keeps it going.
    /// </summary>
    public class UiParticles : MonoBehaviour
    {
        [System.Serializable]
        public struct Settings
        {
            public Sprite sprite;
            public Material material;
            public Vector2 rate;          // particles a second (min, max); zero for bursts only
            public int burst;             // fired once at the start
            public Vector2 lifetime, speed, size;
            public Color colourA, colourB;
            public Vector2 box;           // the shell they start on; zero: the centre
            public float spin;            // degrees a second, either way at random
            public bool shrink;           // size 1 -> 0 over the life (Size over Lifetime)
            public float noise;           // wiggle frequency (0: none)
        }

        private Settings _s;
        private float _rate, _timer;
        private readonly List<Particle> _live = new List<Particle>();

        private struct Particle
        {
            public RectTransform rect;
            public Image image;
            public Vector2 velocity;
            public float age, life, size, spin, seed;
            public Color colour;
        }

        public static UiParticles Add(RectTransform parent, string name, Settings settings)
        {
            var rect = UiBuilder.Rect(parent, name, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(100f, 100f));
            var particles = rect.gameObject.AddComponent<UiParticles>();
            particles._s = settings;
            particles._rate = Random.Range(settings.rate.x, settings.rate.y);
            for (int i = 0; i < settings.burst; i++) particles.Spawn();
            return particles;
        }

        /// <summary>
        /// The FX_Sparks of a burning streak (Main.unity, Btn_* / Streak_FX_Container; the HUD's too): FX_UI_Sparks keeps going
        /// (TX_FX_Sparkle, 12-25 a second off the box's outline, 100 fast, 0.2-0.7 s, 25-70 big, orange to yellow, +/-45 degrees a
        /// second, noise 0.3), and when it lights up FX_UI_Embers throws 5 (S_FX_SmallGlow_01, 25-250 fast, 0.1-0.4 s, 12-25,
        /// shrinking, from a 45 box) and FX_UI_Glow 3 soft glows (TX_FX_Square_Glow2, 5-25, 0.1-0.4 s, 85-95). All additive.
        /// </summary>
        public static void StreakSparks(RectTransform parent, Vector2 box, Sprite spark, Sprite ember, Sprite glow, Material additive)
        {
            Color orange = new Color(1f, 0.465f, 0f), yellow = new Color(1f, 0.86f, 0f);
            var holder = UiBuilder.Rect(parent, "FX_Sparks", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(100f, 100f));
            Add(holder, "FX_UI_Glow", new Settings { sprite = glow, material = additive, burst = 3, lifetime = new Vector2(0.1f, 0.4f), speed = new Vector2(5f, 25f), size = new Vector2(85f, 95f), colourA = new Color(1f, 0.69f, 0f), colourB = new Color(1f, 0.893f, 0.24f) });
            Add(holder, "FX_UI_Sparks", new Settings { sprite = spark, material = additive, rate = new Vector2(12f, 25f), lifetime = new Vector2(0.2f, 0.7f), speed = new Vector2(100f, 100f), size = new Vector2(25f, 70f), colourA = orange, colourB = yellow, box = box, spin = 45f, noise = 0.3f });
            Add(holder, "FX_UI_Embers", new Settings { sprite = ember, material = additive, burst = 5, lifetime = new Vector2(0.1f, 0.4f), speed = new Vector2(25f, 250f), size = new Vector2(12f, 25f), colourA = orange, colourB = yellow, box = new Vector2(45f, 45f), spin = 45f, shrink = true, noise = 1f });
        }

        /// <summary>The HUD's (Game.unity View_StreakActive_HUD): sparks (0.2-0.7 s) and embers (0.4-1 s), each 2-5 a second from the middle, 50 fast, 20-25 big.</summary>
        public static void HudSparks(RectTransform parent, Sprite spark, Sprite ember, Material additive)
        {
            Color orange = new Color(1f, 0.465f, 0f), yellow = new Color(1f, 0.86f, 0f);
            var holder = UiBuilder.Rect(parent, "FX_Sparks", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -24.5f), new Vector2(100f, 100f));
            Add(holder, "FX_UI_Sparks", new Settings { sprite = spark, material = additive, rate = new Vector2(2f, 5f), lifetime = new Vector2(0.2f, 0.7f), speed = new Vector2(50f, 50f), size = new Vector2(20f, 25f), colourA = orange, colourB = yellow, spin = 45f, noise = 0.3f });
            Add(holder, "FX_UI_Embers", new Settings { sprite = ember, material = additive, rate = new Vector2(2f, 5f), lifetime = new Vector2(0.4f, 1f), speed = new Vector2(50f, 50f), size = new Vector2(20f, 25f), colourA = orange, colourB = yellow, spin = 45f, shrink = true, noise = 1f });
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            if (_rate > 0f)
            {
                _timer += dt;
                while (_timer >= 1f / _rate)
                {
                    _timer -= 1f / _rate;
                    _rate = Random.Range(_s.rate.x, _s.rate.y);
                    Spawn();
                }
            }
            for (int i = _live.Count - 1; i >= 0; i--)
            {
                var p = _live[i];
                p.age += dt;
                if (p.age >= p.life || p.rect == null)
                {
                    if (p.rect != null) Destroy(p.rect.gameObject);
                    _live.RemoveAt(i);
                    continue;
                }
                float k = p.age / p.life;
                var velocity = p.velocity;
                if (_s.noise > 0f)
                {
                    float n = Time.unscaledTime * _s.noise + p.seed;
                    velocity += new Vector2(Mathf.PerlinNoise(n, 0f) - 0.5f, Mathf.PerlinNoise(0f, n) - 0.5f) * 2f * p.velocity.magnitude * 0.5f;
                }
                p.rect.anchoredPosition += velocity * dt;
                p.rect.localRotation *= Quaternion.Euler(0f, 0f, p.spin * dt);
                p.rect.sizeDelta = Vector2.one * p.size * (_s.shrink ? 1f - k : 1f);
                var colour = p.colour;
                colour.a *= 1f - k; // Color over Lifetime: alpha 1 -> 0
                p.image.color = colour;
                _live[i] = p;
            }
        }

        private void Spawn()
        {
            if (_s.sprite == null) return;
            Vector2 at = Vector2.zero;
            if (_s.box.x > 0f || _s.box.y > 0f)
            {
                // A point on the box's outline (BoxShell, flat): pick a side by its length.
                float w = _s.box.x, h = _s.box.y, t = Random.value * (w + h) * 2f;
                if (t < w) at = new Vector2(t - w / 2f, h / 2f);
                else if (t < 2f * w) at = new Vector2(t - w - w / 2f, -h / 2f);
                else if (t < 2f * w + h) at = new Vector2(-w / 2f, t - 2f * w - h / 2f);
                else at = new Vector2(w / 2f, t - 2f * w - h - h / 2f);
            }
            var rect = UiBuilder.Rect(transform, "Particle", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), at, Vector2.zero);
            rect.localRotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));
            var image = UiBuilder.Image(rect, _s.sprite, Color.clear);
            image.material = _s.material;
            _live.Add(new Particle
            {
                rect = rect,
                image = image,
                velocity = Random.insideUnitCircle.normalized * Random.Range(_s.speed.x, _s.speed.y),
                life = Random.Range(_s.lifetime.x, _s.lifetime.y),
                size = Random.Range(_s.size.x, _s.size.y),
                spin = Random.Range(-_s.spin, _s.spin),
                seed = Random.value * 100f,
                colour = Color.Lerp(_s.colourA, _s.colourB, Random.value),
            });
        }
    }
}

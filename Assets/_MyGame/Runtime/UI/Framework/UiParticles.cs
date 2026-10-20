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
    /// Các bộ thông số nằm trong prefab Prefabs/UI/Screens/Home (FX_StreakPlayButton) / Screens/Game (FX_StreakHud) (FX_StreakPlayButton: FX_UI_Glow, FX_UI_Sparks, FX_UI_Embers của
    /// Main.unity Streak_FX_Container; FX_StreakHud: của Game.unity View_StreakActive_HUD), chỉnh trong Inspector.
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
            public bool shrink;           // size 1 -> shrinkTo over the life (Size over Lifetime)
            public float shrinkTo;        // size left at the end when shrinking (0: gone)
            public float noise;           // wiggle frequency (0: none)
        }

        [SerializeField] private Settings settings;
        [Tooltip("Prefab mẫu một hạt (Prefabs/UI/Widgets/UiParticle): lấy từ kho tái dùng, không dựng bằng code.")]
        [SerializeField] private Image particlePrefab;

        private UiPool _pool;
        private UiPool Pool => _pool ?? (_pool = new UiPool(particlePrefab, transform));

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

        /// <summary>Mỗi lần bật: bắn loạt đầu rồi phát đều theo rate.</summary>
        private void OnEnable()
        {
            _rate = Random.Range(settings.rate.x, settings.rate.y);
            _timer = 0f;
            for (int i = 0; i < settings.burst; i++) Spawn();
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
                    _rate = Random.Range(settings.rate.x, settings.rate.y);
                    Spawn();
                }
            }
            for (int i = _live.Count - 1; i >= 0; i--)
            {
                var p = _live[i];
                p.age += dt;
                if (p.age >= p.life || p.rect == null)
                {
                    Pool.Release(p.image);
                    _live.RemoveAt(i);
                    continue;
                }
                float k = p.age / p.life;
                var velocity = p.velocity;
                if (settings.noise > 0f)
                {
                    float n = Time.unscaledTime * settings.noise + p.seed;
                    velocity += new Vector2(Mathf.PerlinNoise(n, 0f) - 0.5f, Mathf.PerlinNoise(0f, n) - 0.5f) * 2f * p.velocity.magnitude * 0.5f;
                }
                p.rect.anchoredPosition += velocity * dt;
                p.rect.localRotation *= Quaternion.Euler(0f, 0f, p.spin * dt);
                p.rect.sizeDelta = Vector2.one * p.size * (settings.shrink ? Mathf.Lerp(1f, settings.shrinkTo, k) : 1f);
                var colour = p.colour;
                colour.a *= 1f - k; // Color over Lifetime: alpha 1 -> 0
                p.image.color = colour;
                _live[i] = p;
            }
        }

        private void Spawn()
        {
            if (settings.sprite == null || particlePrefab == null) return;
            Vector2 at = Vector2.zero;
            if (settings.box.x > 0f || settings.box.y > 0f)
            {
                // A point on the box's outline (BoxShell, flat): pick a side by its length.
                float w = settings.box.x, h = settings.box.y, t = Random.value * (w + h) * 2f;
                if (t < w) at = new Vector2(t - w / 2f, h / 2f);
                else if (t < 2f * w) at = new Vector2(t - w - w / 2f, -h / 2f);
                else if (t < 2f * w + h) at = new Vector2(-w / 2f, t - 2f * w - h / 2f);
                else at = new Vector2(w / 2f, t - 2f * w - h - h / 2f);
            }
            var image = Pool.Get();
            var rect = image.rectTransform;
            rect.anchoredPosition = at;
            rect.sizeDelta = Vector2.zero;
            rect.localRotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));
            image.sprite = settings.sprite;
            image.color = Color.clear;
            image.material = settings.material;
            _live.Add(new Particle
            {
                rect = rect,
                image = image,
                velocity = Random.insideUnitCircle.normalized * Random.Range(settings.speed.x, settings.speed.y),
                life = Random.Range(settings.lifetime.x, settings.lifetime.y),
                size = Random.Range(settings.size.x, settings.size.y),
                spin = Random.Range(-settings.spin, settings.spin),
                seed = Random.value * 100f,
                colour = Color.Lerp(settings.colourA, settings.colourB, Random.value),
            });
        }
    }
}

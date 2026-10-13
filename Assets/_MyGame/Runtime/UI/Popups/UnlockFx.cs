using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using static SnackDash.UI.UiBuilder;

namespace SnackDash.UI
{
    /// <summary>
    /// Ánh sáng sau cơ chế vừa mở ở màn thắng (trang UNLOCKED!): FX_UI_Highlight_Rays_Golden của VictoryPopup gốc. Quầng cam, hai quạt
    /// tia vàng quay ngược chiều nhau mỗi vòng 20 s (Rotation feedbacks gốc), và hai nguồn hạt trên vòng tròn bán kính 50: chấm
    /// (TX_FX_Sparkle, 3 hạt / s) và lấp lánh (circle_glow_fx, 6 hạt / s), mỗi hạt sống 1-1.3 s, cỡ 15-25, to dần rồi nhỏ lại. Các phần
    /// nằm sẵn trong prefab Victory (nhánh "Feautre Unlocked"), chỉnh trên Inspector.
    /// </summary>
    public class UnlockFx : MonoBehaviour
    {
        [Tooltip("FX_Image_Rays_A / _B: hai quạt tia quay ngược chiều nhau.")]
        [SerializeField] private RectTransform raysA, raysB;
        [Tooltip("Giây cho một vòng quay của tia.")]
        [SerializeField] private float roundSeconds = 20f;
        [Tooltip("Hạt chấm và hạt lấp lánh.")]
        [SerializeField] private Sprite dot, sparkle;
        [SerializeField] private float dotRate = 3f, sparkleRate = 6f;
        [Tooltip("Hạt sinh trên vòng tròn bán kính này, bay ra ngoài chậm.")]
        [SerializeField] private float radius = 50f;
        [Tooltip("Vật liệu cộng sáng cho hạt (lấy theo quầng FX_Image_Glow).")]
        [SerializeField] private Image glow;

        private readonly List<Emitter> _emitters = new List<Emitter>();

        private class Emitter
        {
            public Sprite sprite;
            public float rate, timer;
            public readonly List<(RectTransform rect, Image image, float age, float life, float size)> live = new List<(RectTransform, Image, float, float, float)>();
        }

        private void Awake()
        {
            if (dot != null) _emitters.Add(new Emitter { sprite = dot, rate = dotRate });
            if (sparkle != null) _emitters.Add(new Emitter { sprite = sparkle, rate = sparkleRate });
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            float turn = 360f / roundSeconds * dt;
            if (raysA != null) raysA.localRotation *= Quaternion.Euler(0f, 0f, -turn);
            if (raysB != null) raysB.localRotation *= Quaternion.Euler(0f, 0f, turn);
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
                    float grow = Mathf.Sin(p.age / p.life * Mathf.PI); // to dần rồi nhỏ lại
                    p.rect.sizeDelta = Vector2.one * p.size * grow;
                    p.rect.anchoredPosition += p.rect.anchoredPosition.normalized * 0.75f * dt; // startSpeed 0.5-1, bay ra ngoài
                    emitter.live[i] = p;
                }
            }
        }

        private void Spawn(Emitter emitter)
        {
            float angle = Random.value * Mathf.PI * 2f;
            var at = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius; // trên mép vòng tròn
            var rect = Rect(transform, "Particle", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), at, Vector2.zero);
            rect.localRotation = Quaternion.Euler(0f, 0f, Random.Range(-90f, 0f));
            var image = Image(rect, emitter.sprite, Color.white);
            image.material = glow != null ? glow.material : null;
            emitter.live.Add((rect, image, 0f, Random.Range(1f, 1.3f), Random.Range(15f, 25f)));
        }
    }
}

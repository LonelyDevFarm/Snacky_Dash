using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace SnackDash.UI
{
    /// <summary>
    /// Chùm tia sáng toả từ tâm (gốc: hệ hạt "Back Rays 02" sau rương, ảnh lightray2 cộng sáng): tia mọc liên tục theo hướng ngẫu
    /// nhiên, hiện lên rồi tắt dần trong đời của nó; nhiều tia mờ chồng nhau thành quầng tia mềm. Tia lấy từ mẫu con (Image, điểm neo
    /// ở đầu nhọn), dựng một lần khi bật, chỉnh trên Inspector.
    /// </summary>
    public class UiRays : MonoBehaviour
    {
        [Tooltip("Mẫu một tia (tắt sẵn): Image lightray2, pivot (0, 0.5).")]
        [SerializeField] private Image template;
        [Tooltip("Tia mỗi giây (gốc 25) và đời một tia (gốc 1.1 s).")]
        [SerializeField] private float rate = 25f, life = 1.1f;
        [Tooltip("Dài x rộng một tia (đơn vị canvas; video sk1: tia chạm gần mép màn).")]
        [SerializeField] private Vector2 size = new Vector2(560f, 150f);
        [Tooltip("Màu (gốc startColor 1, 0.87, 0, alpha 0.12 qua shader hạt cũ; ở UI/Additive chỉnh còn 0.05 cho khớp độ sáng video sk1).")]
        [SerializeField] private Color colour = new Color(1f, 0.75f, 0f, 0.05f);

        private readonly List<(Image image, float age)> _rays = new List<(Image, float)>();
        private float _timer;

        private void OnEnable()
        {
            if (template == null) return;
            int count = Mathf.CeilToInt(rate * life) + 1;
            while (_rays.Count < count)
            {
                var image = Instantiate(template, transform);
                image.gameObject.SetActive(true);
                image.rectTransform.sizeDelta = size;
                _rays.Add((image, life)); // chưa mọc
            }
            // Đầy sẵn như hệ hạt đã chạy một lúc: tuổi rải đều.
            for (int i = 0; i < _rays.Count; i++) Respawn(i, life * i / _rays.Count);
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            _timer += dt;
            for (int i = 0; i < _rays.Count; i++)
            {
                var (image, age) = _rays[i];
                age += dt;
                if (age >= life && _timer >= 1f / rate) { _timer -= 1f / rate; Respawn(i, 0f); continue; }
                _rays[i] = (image, age);
                var c = colour;
                c.a *= age >= life ? 0f : Mathf.Sin(Mathf.PI * age / life); // hiện lên rồi tắt
                image.color = c;
            }
            _timer = Mathf.Min(_timer, 1f);
        }

        private void Respawn(int i, float age)
        {
            var image = _rays[i].image;
            image.rectTransform.localRotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));
            _rays[i] = (image, age);
        }
    }
}

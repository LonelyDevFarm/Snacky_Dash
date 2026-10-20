using System.Collections;
using TMPro;
using UnityEngine;

namespace SnackDash.Presentation
{
    /// <summary>
    /// Bộ đếm trên rèm (prefab Prefabs/InGame/Mechanics/Curtain/View_CurtainCounter): số trái còn phải ăn và hình trái.
    /// CurtainsView tạo và gọi: số nảy khi giảm, khói bung rồi ẩn khi rèm mở.
    /// </summary>
    public class CurtainCounterView : MonoBehaviour
    {
        [SerializeField] private TMP_Text countText;
        [Tooltip("KindIconParent: hình trái và bóng của nó.")]
        [SerializeField] private Transform iconsParent;
        [SerializeField] private SpriteRenderer[] icons;
        [Tooltip("Số phóng tới cỡ này rồi về (gốc _bounceScale 2.5, chỉnh theo hình).")]
        [SerializeField] private float bounceScale = 1.3f;
        [SerializeField] private float bounceDuration = 0.18f;
        [Tooltip("Feedback/FX_CurtainDestroy: khói lúc rèm mở.")]
        [SerializeField] private ParticleSystem revealSmoke;
        [SerializeField] private float hideDuration = 0.15f;

        private Vector3 _textScale = Vector3.one;
        private Coroutine _bounce;

        public void Init(Sprite fruit, int count)
        {
            if (countText != null) { _textScale = countText.transform.localScale; countText.text = count.ToString(); }
            foreach (var icon in icons) if (icon != null) icon.sprite = fruit;
        }

        public void SetCount(int count)
        {
            if (countText == null || countText.text == count.ToString()) return;
            countText.text = count.ToString();
            if (_bounce != null) StopCoroutine(_bounce);
            _bounce = StartCoroutine(Bounce());
        }

        private IEnumerator Bounce()
        {
            var text = countText.transform;
            yield return Tween.Run(bounceDuration, Tween.Linear, t => text.localScale = _textScale * Mathf.Lerp(1f, bounceScale, Mathf.Sin(t * Mathf.PI)));
            text.localScale = _textScale;
            _bounce = null;
        }

        public IEnumerator Reveal()
        {
            if (revealSmoke != null) { revealSmoke.transform.SetParent(transform.parent, true); revealSmoke.Play(true); Destroy(revealSmoke.gameObject, 2f); }
            var start = transform.localScale;
            yield return Tween.Run(hideDuration, Tween.Linear, t => transform.localScale = start * (1f - t));
            gameObject.SetActive(false);
        }
    }
}

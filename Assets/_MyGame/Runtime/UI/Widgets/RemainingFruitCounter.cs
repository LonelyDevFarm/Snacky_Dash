using System.Collections;
using SnackDash.LevelSystem;
using SnackDash.Presentation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SnackDash.UI
{
    /// <summary>
    /// Một ô của thanh đếm trái còn lại (gốc RemainingFruitCounter trong Game.unity): icon trái, số còn lại ở góc dưới phải, hết thì
    /// dấu tích nảy lên. Prefab Prefabs/UI/Screens/Game/RemainingFruitCounter, RemainingFruitBar bật / gán số; số hiệu ứng lấy từ gốc.
    /// </summary>
    public class RemainingFruitCounter : MonoBehaviour
    {
        [SerializeField] private PillKind kind;
        [SerializeField] private Image icon;
        [SerializeField] private TMP_Text countText;
        [SerializeField] private GameObject checkmark;
        [Header("Hiệu ứng (gốc: punch 0.25 / 0.35 s, hiện ra từ 0.65 trong 0.35 s, OutBack 2)")]
        [SerializeField] private float checkmarkPunchScale = 0.25f;
        [SerializeField] private float checkmarkPunchDuration = 0.35f;
        [SerializeField] private float enableScaleFrom = 0.65f;
        [SerializeField] private float enableScaleDuration = 0.35f;
        [SerializeField] private float enableScaleOvershoot = 2f;

        private static readonly string[] Numbers = new string[1000];
        private int _count = -1;

        public PillKind Kind => kind;

        public void SetIcon(Sprite sprite) => icon.sprite = sprite;

        private void OnEnable()
        {
            if (enableScaleDuration <= 0f) return;
            transform.localScale = Vector3.one * enableScaleFrom;
            StartCoroutine(Tween.Run(enableScaleDuration, Tween.OutBack(enableScaleOvershoot),
                t => transform.localScale = Vector3.one * Mathf.LerpUnclamped(enableScaleFrom, 1f, t)));
        }

        /// <summary>Số còn lại; về 0 thì số ẩn và dấu tích nảy (chỉ khi vừa đổi, không chạy lại mỗi lần gọi).</summary>
        public void SetCount(int count)
        {
            if (count == _count) return;
            bool done = count <= 0, wasDone = _count == 0;
            _count = Mathf.Max(0, count);
            countText.gameObject.SetActive(!done);
            if (!done) countText.text = _count < Numbers.Length ? Numbers[_count] ?? (Numbers[_count] = _count.ToString()) : _count.ToString();
            checkmark.SetActive(done);
            if (done && !wasDone && isActiveAndEnabled) StartCoroutine(PunchCheckmark());
        }

        private IEnumerator PunchCheckmark()
        {
            var target = checkmark.transform;
            yield return Tween.Punch(checkmarkPunchDuration * 0.5f, t => target.localScale = Vector3.one * (1f + checkmarkPunchScale * t));
            target.localScale = Vector3.one;
        }
    }
}

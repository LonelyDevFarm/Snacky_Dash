using System.Collections;
using System.Collections.Generic;
using SnackDash.Economy;
using SnackDash.Gameplay;
using SnackDash.Presentation;
using UnityEngine;
using UnityEngine.EventSystems;

namespace SnackDash.UI
{
    /// <summary>
    /// Con bọ Lucky Ladybug trong màn (prefab Prefabs/UI/Screens/Game/Ladybug, lớp phủ toàn màn trong HUD; Docs/KE_HOACH_LUCKY_LADYBUG.md).
    /// Màn có bọ: khi đợt trái thứ nhất được rải (không tính đợt có sẵn), bọ bay lượn lên đậu trên quả chuối ở cuối thanh tiến độ HUD
    /// [user 19/10]; đợt thứ 2 rải hoặc thắng thì bay đi. Chạm trúng: bắt (<see cref="LuckyLadybug.Catch"/>), lần chạm đó không thành
    /// vuốt, bọ bay một vòng rồi vào lọ ở đầu trái thanh (lọ thay chỗ đầu rắn). Bay tới chậm cho người chơi kịp thấy [user 20/10].
    /// Lúc đậu: bọ nhún vỗ cánh gọi (trạng thái Call, mỗi 4 giây) và nhóm Hint sáng lên [user 20/10: thay bàn tay]. Lần đầu gặp: đậu tới khi bị bắt / thắng. Đường bay chỉ lượn giữa HUD và mép trên bàn chơi, không bay qua bản đồ.
    /// Hình và dáng nằm trong Unity [user 19/10]: Animator trên Body (trạng thái Fly / Idle, clip LB_Ladybug_*: đổi ảnh, nhún, dáng đậu
    /// quay trái ngóc đầu); đường bay là các điểm đặt sẵn trong HUD (ProgressBar/Fruits/LadybugPath), kéo thả để sửa. Code chỉ cho bọ
    /// bay mượt qua các điểm và lật hình theo hướng bay. Chỉ nghe sự kiện của màn, không đổi gì trong luật chơi.
    /// </summary>
    public class LadybugView : MonoBehaviour, IPointerDownHandler
    {
        private const string HandSeenKey = "lb_hand_seen";
        private static readonly int FlyState = Animator.StringToHash("Fly"), CallState = Animator.StringToHash("Call");

        [Tooltip("Con bọ: chỗ code di chuyển; ảnh nằm ở con Sprite (nhận chạm).")]
        [SerializeField] private RectTransform body;
        [Tooltip("Animator trên Body (LB_Ladybug): Fly lúc bay, Idle lúc đậu.")]
        [SerializeField] private Animator animator;
        [Tooltip("Ảnh bay (Ladybug_Fly) vẽ quay mặt sang trái: lật hình theo đó để bọ luôn bay về phía trước.")]
        [SerializeField] private bool flyFacesLeft = true;

        [Header("Đường bay — điểm đặt sẵn trong HUD, kéo thả để chỉnh")]
        [Tooltip("Bay tới: từ điểm đầu (ngoài màn) qua các điểm; điểm cuối là chỗ đậu trên quả chuối.")]
        [SerializeField] private RectTransform[] arrivePath = new RectTransform[0];
        [Tooltip("Bị bắt: bay vòng rồi về phía lọ; sau điểm cuối bọ chui vào lọ.")]
        [SerializeField] private RectTransform[] catchPath = new RectTransform[0];
        [Tooltip("Bay đi (đợt 2 / thắng): ra khỏi màn.")]
        [SerializeField] private RectTransform[] leavePath = new RectTransform[0];
        [Tooltip("Cỡ bọ lúc đậu so với lúc bay (cho vừa HUD).")]
        [SerializeField] private float sitScale = 0.55f;
        [Tooltip("Bay tới chậm cho người chơi kịp thấy bọ là gì [user 20/10: bay nhanh quá].")]
        [SerializeField] private float flyInTime = 2.2f;
        [SerializeField] private float flyOutTime = 0.9f, catchTime = 1.5f;

        [Tooltip("Gợi ý lúc bọ đậu (Body/Hint, tắt sẵn): quầng sáng, vòng sáng lan, sao bật lên — clip LB_Hint. Bọ chơi trạng thái Call (nhún vỗ cánh mỗi 4 giây) [user 20/10: thay bàn tay].")]
        [SerializeField] private GameObject hint;
        [SerializeField] private LadybugJar jar;

        private RectTransform _canvas;
        private bool _sitting, _busy, _first;
        private readonly List<Vector2> _points = new List<Vector2>();

        public void Bind(GameSession session)
        {
            var ladybug = LuckyLadybug.Shared;
            bool here = ladybug.LadybugThisLevel;
            jar.Show(here);
            body.gameObject.SetActive(false);
            if (hint != null) hint.SetActive(false);
            if (!here) return;
            _canvas = (RectTransform)transform;
            session.WaveSpawned += OnWave;
            session.Won += () => { if (_sitting) StartCoroutine(Leave()); };
        }

        private void OnWave(int wave)
        {
            if (wave == 1 && !_sitting && !_busy && !LuckyLadybug.Shared.CaughtThisLevel) StartCoroutine(Land());
            else if (wave >= 2 && _sitting && !_first) StartCoroutine(Leave());
        }

        // ------------------------------------------------------------------ đậu / bay đi

        private IEnumerator Land()
        {
            if (arrivePath.Length == 0) yield break;
            _busy = true;
            _first = PlayerPrefs.GetInt(HandSeenKey, 0) == 0;
            body.gameObject.SetActive(true);
            animator.Play(FlyState, 0, 0f);
            Path(null, arrivePath, null);
            yield return Follow(flyInTime, 1f, sitScale);
            body.localScale = Vector3.one * sitScale; // dáng đậu (quay trái, ngóc đầu) nằm trong clip Idle
            animator.Play(CallState, 0, 0f); // đậu: nhún vỗ cánh gọi người chơi (clip LB_Ladybug_Call)
            _busy = false;
            _sitting = true;
            if (hint != null) hint.SetActive(true);
            if (_first) { PlayerPrefs.SetInt(HandSeenKey, 1); PlayerPrefs.Save(); }
        }

        private IEnumerator Leave()
        {
            if (!_sitting) yield break;
            _sitting = false;
            _busy = true;
            if (hint != null) hint.SetActive(false);
            animator.Play(FlyState, 0, 0f);
            Path(body, leavePath, null);
            yield return Follow(flyOutTime, sitScale, 1f);
            body.gameObject.SetActive(false);
            _busy = false;
        }

        // ------------------------------------------------------------------ bắt

        public void OnPointerDown(PointerEventData eventData)
        {
            if (!_sitting || _busy) return;
            SnackDash.Input.SwipeInput.IgnoreCurrentTouch();
            if (!LuckyLadybug.Shared.Catch()) return;
            _sitting = false;
            StartCoroutine(FlyToJar());
        }

        private IEnumerator FlyToJar()
        {
            _busy = true;
            if (hint != null) hint.SetActive(false);
            SnackDash.Audio.SoundPlayer.Play("CollectCoin");
            SnackDash.Audio.Haptics.Light();
            animator.Play(FlyState, 0, 0f);
            Path(body, catchPath, jar.Target);
            float endScale = jar.Target.rect.width * jar.Target.lossyScale.x / Mathf.Max(1f, body.rect.width * Mathf.Abs(body.lossyScale.x) / sitScale);
            yield return Follow(catchTime, sitScale, endScale);
            body.gameObject.SetActive(false);
            body.localScale = Vector3.one;
            jar.Fill();
            _busy = false;
        }

        // ------------------------------------------------------------------ bay theo điểm

        /// <summary>Gom điểm bay (toạ độ lớp phủ): chỗ đang đứng (nếu có), các điểm đặt sẵn, đích (nếu có).</summary>
        private void Path(RectTransform from, RectTransform[] points, RectTransform to)
        {
            _points.Clear();
            if (from != null) _points.Add(from.anchoredPosition);
            foreach (var point in points) if (point != null) _points.Add(Local(point));
            if (to != null) _points.Add(Local(to));
        }

        private Vector2 Local(RectTransform target)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_canvas, RectTransformUtility.WorldToScreenPoint(null, target.position), null, out var local);
            return local;
        }

        /// <summary>Bay mượt qua mọi điểm (Catmull-Rom, tốc độ đều theo chiều dài đoạn), co giãn từ <paramref name="fromScale"/> tới
        /// <paramref name="toScale"/>, mặt quay theo hướng đang bay.</summary>
        private IEnumerator Follow(float time, float fromScale, float toScale)
        {
            if (_points.Count == 0) yield break;
            int count = _points.Count;
            var lengths = new float[count];
            for (int i = 1; i < count; i++) lengths[i] = lengths[i - 1] + Vector2.Distance(_points[i - 1], _points[i]);
            float total = Mathf.Max(1f, lengths[count - 1]);
            body.anchoredPosition = _points[0];
            float flip = 1f;
            yield return Tween.Run(time, Tween.InOutSine, t =>
            {
                var at = PointAt(t * total, lengths);
                flip = Face(at.x - body.anchoredPosition.x, flip);
                body.anchoredPosition = at;
                float s = Mathf.Lerp(fromScale, toScale, t);
                body.localScale = new Vector3(flip * s, s, 1f);
            });
            body.anchoredPosition = _points[count - 1];
        }

        private Vector2 PointAt(float distance, float[] lengths)
        {
            int count = _points.Count;
            if (count == 1) return _points[0];
            int i = 1;
            while (i < count - 1 && lengths[i] < distance) i++;
            float span = Mathf.Max(0.001f, lengths[i] - lengths[i - 1]);
            float u = Mathf.Clamp01((distance - lengths[i - 1]) / span);
            Vector2 p0 = _points[Mathf.Max(0, i - 2)], p1 = _points[i - 1], p2 = _points[i], p3 = _points[Mathf.Min(count - 1, i + 1)];
            float u2 = u * u, u3 = u2 * u;
            return 0.5f * (2f * p1 + (p2 - p0) * u + (2f * p0 - 5f * p1 + 4f * p2 - p3) * u2 + (3f * p1 - p0 - 3f * p2 + p3) * u3);
        }

        /// <summary>Hệ số lật ngang (1 / -1) để ảnh bay quay mặt theo hướng đi <paramref name="dx"/>; đi gần như thẳng đứng thì giữ <paramref name="current"/>.</summary>
        private float Face(float dx, float current)
        {
            if (Mathf.Abs(dx) <= 2f) return current; // ngưỡng nhỏ: lượn gần thẳng đứng không làm hình lật qua lật lại
            bool left = dx < 0f;
            return left == flyFacesLeft ? 1f : -1f;
        }
    }
}

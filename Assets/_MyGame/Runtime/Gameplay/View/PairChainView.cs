using System;
using UnityEngine;

namespace SnackDash.Presentation
{
    /// <summary>
    /// Xích khay đôi (prefab PairRope) chuyển động như xích thật [user 23/10]: dây verlet nhiều điểm, hai đầu ghim vào khoen trên thành hai
    /// thùng, có trọng lực nên hơi võng ở giữa; thùng rung khi nhận trái thì khoen đi theo thành thùng và xích đung đưa rồi dịu lại. Mỗi mắt
    /// xích nằm giữa hai điểm liền nhau, xoay theo đoạn dây (nghiêng xen kẽ cho dáng móc vào nhau). Toạ độ trong khung của PairRope.
    /// </summary>
    public class PairChainView : MonoBehaviour
    {
        [SerializeField] private Transform leftAnchor, rightAnchor;
        [Tooltip("Mắt xích từ trái sang phải; số điểm của dây = số mắt + 1.")]
        [SerializeField] private Transform[] links = new Transform[0];
        [Tooltip("Mắt xích nghiêng xen kẽ ± chừng này độ quanh hướng đoạn dây.")]
        [SerializeField] private float tilt = 15f;
        [Tooltip("Đầu dây cách tâm khoen chừng này vào phía giữa (chỗ mắt xích đầu móc vào khoen).")]
        [SerializeField] private float anchorInset = 0.08f;
        [Tooltip("Dây dài hơn khoảng cách hai đầu chừng này phần: càng lớn càng võng.")]
        [SerializeField] private float slack = 0.03f;
        [Tooltip("Gia tốc trọng lực (đơn vị / s²), hướng xuống trên màn.")]
        [SerializeField] private float gravity = 12f;
        [Tooltip("Phần vận tốc giữ lại mỗi khung (1 = không cản): nhỏ thì xích dịu nhanh.")]
        [SerializeField, Range(0.8f, 1f)] private float damping = 0.94f;
        [SerializeField] private int iterations = 8;
        [Tooltip("Thùng nhận trái: xích bị hất lên với vận tốc chừng này (đơn vị / s) ở phía thùng đó rồi rơi lại, đung đưa (thùng chỉ rung 0.06 nên tự bám theo thì không thấy).")]
        [SerializeField] private float kickSpeed = 5f;

        [Header("Đứt xích (cặp khay xong)")]
        [Tooltip("Giây từ lúc đứt tới khi mắt xích biến mất.")]
        [SerializeField] private float breakDuration = 0.6f;
        [Tooltip("Mắt xích văng ra hai bên với vận tốc ngang chừng này (đơn vị / s), cộng ngẫu nhiên tới một nửa.")]
        [SerializeField] private float breakSideSpeed = 2.5f;
        [Tooltip("Mắt xích bật lên với vận tốc chừng này (đơn vị / s) rồi rơi theo trọng lực.")]
        [SerializeField] private float breakUpSpeed = 3f;
        [Tooltip("Mắt xích xoay tối đa chừng này độ / s khi văng.")]
        [SerializeField] private float breakSpin = 720f;
        [Tooltip("Khoen thu nhỏ hết trong chừng này giây (khay bay đi CratesView.exitDuration 0.3).")]
        [SerializeField] private float anchorShrink = 0.3f;

        private Vector2 _leftRest, _rightRest, _inset;
        private Vector2[] _points, _previous, _kick;
        private float _segment;
        private Transform _leftWall, _rightWall;
        private Vector3 _leftWallRest, _rightWallRest;
        private Func<Transform> _rightWallSource;
        private bool _broken;
        private float _brokenFor;
        private Vector2[] _flyVelocity;
        private float[] _flySpin;
        private Vector3 _leftAnchorScale, _rightAnchorScale;

        /// <summary>
        /// Cặp khay xong: xích tách khỏi khay (sang <paramref name="keepUnder"/> để không mất cùng khay), đứt ở giữa; mắt xích văng về phía
        /// khay của nó, bật lên, xoay, rơi rồi nhỏ dần; khoen thu nhỏ cùng khay. Xong thì tự huỷ.
        /// </summary>
        public void Break(Transform keepUnder)
        {
            if (_broken) return;
            _broken = true;
            transform.SetParent(keepUnder, true);
            _leftAnchorScale = leftAnchor.localScale;
            _rightAnchorScale = rightAnchor.localScale;
            _flyVelocity = new Vector2[links.Length];
            _flySpin = new float[links.Length];
            for (int i = 0; i < links.Length; i++)
            {
                float side = i < links.Length / 2 ? -1f : 1f;
                _flyVelocity[i] = new Vector2(side * breakSideSpeed * UnityEngine.Random.Range(1f, 1.5f), breakUpSpeed * UnityEngine.Random.Range(0.7f, 1.2f));
                _flySpin[i] = side * breakSpin * UnityEngine.Random.Range(0.5f, 1f);
            }
        }

        /// <summary>Gắn với khối thùng trái (đứng yên trong lúc gắn) và nguồn khối thùng phải (thùng bên kia có thể dựng lại sau).</summary>
        public void Bind(Transform leftWall, Func<Transform> rightWallSource)
        {
            _leftWall = leftWall;
            _leftWallRest = leftWall != null ? leftWall.localPosition : Vector3.zero;
            _rightWallSource = rightWallSource;
            _rightWall = null;
        }

        /// <summary>Thùng một bên vừa nhận trái: hất xích lên, đoạn gần thùng đó nhiều nhất, lệch ngang chút cho khỏi lặp lại y hệt.</summary>
        public void Kick(bool rightSide)
        {
            int last = _points.Length - 1;
            var push = new Vector2(UnityEngine.Random.Range(-0.3f, 0.3f), 1f) * kickSpeed;
            for (int i = 1; i < last; i++)
            {
                float t = i / (float)last;
                _kick[i] += push * (rightSide ? t : 1f - t);
            }
        }

        private void Awake()
        {
            _leftRest = leftAnchor.localPosition;
            _rightRest = rightAnchor.localPosition;
            _inset = (_rightRest - _leftRest).normalized * anchorInset;
            int count = links.Length + 1;
            _points = new Vector2[count];
            _previous = new Vector2[count];
            _kick = new Vector2[count];
            float span = Vector2.Distance(_leftRest + _inset, _rightRest - _inset);
            _segment = span * (1f + slack) / links.Length;
            float sag = span * Mathf.Sqrt(3f * slack / 8f); // độ võng parabol có cùng chiều dài dây
            for (int i = 0; i < count; i++)
            {
                float t = i / (float)links.Length;
                // Bắt đầu đã võng sẵn để không rơi giật lúc hiện.
                _points[i] = Vector2.Lerp(_leftRest + _inset, _rightRest - _inset, t) + Vector2.down * (4f * t * (1f - t) * sag);
                _previous[i] = _points[i];
            }
            Place();
        }

        private void LateUpdate()
        {
            float dt = Mathf.Min(Time.unscaledDeltaTime, 1f / 30f);
            if (_broken) { Fly(Mathf.Min(Time.deltaTime, 0.1f)); return; } // cùng nhịp với khay bay đi (Tween theo Time)
            int last = _points.Length - 1;
            _points[0] = _leftRest + _inset + Shake(_leftWall, _leftWallRest);
            var rightWall = _rightWallSource != null ? _rightWallSource() : null;
            if (rightWall != _rightWall) { _rightWall = rightWall; _rightWallRest = rightWall != null ? rightWall.localPosition : Vector3.zero; }
            _points[last] = _rightRest - _inset + Shake(_rightWall, _rightWallRest);
            for (int i = 1; i < last; i++) { _previous[i] -= _kick[i] * dt; _kick[i] = Vector2.zero; }

            var fall = Vector2.down * gravity * dt * dt;
            for (int i = 1; i < last; i++)
            {
                var velocity = (_points[i] - _previous[i]) * damping;
                _previous[i] = _points[i];
                _points[i] += velocity + fall;
            }
            for (int pass = 0; pass < iterations; pass++)
                for (int i = 0; i < last; i++)
                {
                    var delta = _points[i + 1] - _points[i];
                    float length = delta.magnitude;
                    if (length < 1e-5f) continue;
                    var fix = delta * ((length - _segment) / length);
                    if (i == 0) _points[i + 1] -= fix;
                    else if (i + 1 == last) _points[i] += fix;
                    else { _points[i] += fix * 0.5f; _points[i + 1] -= fix * 0.5f; }
                }
            Place();
        }

        private void Fly(float dt)
        {
            _brokenFor += dt;
            float shrink = 1f - Mathf.Clamp01(_brokenFor / anchorShrink);
            leftAnchor.localScale = _leftAnchorScale * shrink;
            rightAnchor.localScale = _rightAnchorScale * shrink;
            // Nửa sau thời gian thì nhỏ dần về 0.
            float size = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(breakDuration * 0.5f, breakDuration, _brokenFor));
            for (int i = 0; i < links.Length; i++)
            {
                _flyVelocity[i].y -= gravity * dt;
                var p = links[i].localPosition;
                links[i].localPosition = new Vector3(p.x + _flyVelocity[i].x * dt, p.y + _flyVelocity[i].y * dt, p.z);
                links[i].localRotation *= Quaternion.Euler(0f, 0f, _flySpin[i] * dt);
                links[i].localScale = Vector3.one * size;
            }
            if (_brokenFor >= breakDuration) Destroy(gameObject);
        }

        /// <summary>Thùng rung: khối thùng lệch khỏi chỗ nghỉ bao nhiêu thì khoen lệch bấy nhiêu.</summary>
        private static Vector2 Shake(Transform wall, Vector3 rest) => wall != null ? (Vector2)(wall.localPosition - rest) : Vector2.zero;

        private void Place()
        {
            int last = _points.Length - 1;
            SetXY(leftAnchor, _points[0] - _inset);
            SetXY(rightAnchor, _points[last] + _inset);
            for (int i = 0; i < links.Length; i++)
            {
                var a = _points[i];
                var b = _points[i + 1];
                SetXY(links[i], (a + b) * 0.5f);
                float angle = Mathf.Atan2(b.y - a.y, b.x - a.x) * Mathf.Rad2Deg;
                links[i].localRotation = Quaternion.Euler(0f, 0f, angle + (i % 2 == 0 ? tilt : -tilt));
            }
        }

        private static void SetXY(Transform target, Vector2 xy) => target.localPosition = new Vector3(xy.x, xy.y, target.localPosition.z);
    }
}

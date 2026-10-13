using System.Collections;
using System.Collections.Generic;
using SnackDash.Gameplay;
using SnackDash.LevelSystem;
using UnityEngine;

namespace SnackDash.Presentation
{
    /// <summary>
    /// Trái nằm trên bàn (mỗi ô một PillMaster_VO, gốc PillView): nhảy vào theo đợt, nhún khi nằm yên, né sang bên khi thân
    /// rắn đè lên. Nằm trong scene Game, GameBootstrap gọi Bind.
    /// </summary>
    public class PillsView : MonoBehaviour
    {
        private sealed class Pill
        {
            public GameObject go;
            public Vector2Int cell;
            public bool landed, pushed;
            public Vector3 waitPosition;
            public float clearTime;
            public Transform fruit;          // trái đang hiện; nhún còn bóng nằm yên trên bàn
            public float fruitZ, bobTime, bobDuration;
            public Transform shadow;
            public Vector3 shadowScale;
            public Transform question;       // dấu ? của hộp bí ẩn, xoay liên tục
            public Quaternion questionRest;
            public float spinTime, spinDuration;
        }

        [SerializeField] private GameObject pillPrefab;
        [Tooltip("View_Secret_Collectible: hộp ? che trái bí ẩn tới khi bị ăn.")]
        [SerializeField] private GameObject secretPrefab;
        [SerializeField] private float spawnZ = -3f;
        [SerializeField] private float jumpHeight = 2f;
        [SerializeField] private float jumpDuration = 0.2f;

        [Header("Wave (measured on LV2-3)")]
        [SerializeField] private float waveDelay = 0.2f;
        [SerializeField] private float waveSpread = 0.25f;

        [Header("Waiting beside the body (PillView)")]
        [Tooltip("Lệch ngang cạnh đoạn thân dọc, ~1 ô trên màn (video Man_Lose).")]
        [SerializeField] private float sideDistanceHorizontal = 1f;
        [Tooltip("Lệch trên/dưới đoạn thân ngang; nhỏ hơn vì độ cao chờ đã đẩy lên ~0.4 ô.")]
        [SerializeField] private float sideDistanceVertical = 0.65f;
        [SerializeField] private float pushDuration = 0.12f;
        [SerializeField] private float checkInterval = 0.1f;
        [SerializeField] private float minClearTime = 0.3f;
        [SerializeField] private float waitZ = -1.1f;
        [Tooltip("Độ cao trái trên ô (gốc PillController._zOffset -0.5); bóng nằm sát đường.")]
        [SerializeField] private float restZ = -0.5f;

        [Header("Idle (PillView / SecretCollectibleView)")]
        [Tooltip("Độ cao nhún của trái nằm yên (gốc 0.3, chốt 0.45 [user]).")]
        [SerializeField] private float idleHeight = 0.45f;
        [Tooltip("Tỉ lệ bóng khi trái lên cao nhất [user].")]
        [SerializeField, Range(0.3f, 1f)] private float shadowAtTop = 0.75f;
        [Tooltip("Giây cho một lượt lên hoặc xuống (_idleAnimationDuration).")]
        [SerializeField] private float idleDuration = 1.75f;
        [Tooltip("Trễ ngẫu nhiên tối đa trước lượt đầu (_idleAnimationMaxDelay).")]
        [SerializeField] private float idleMaxDelay = 0.3f;
        [Tooltip("Thời lượng lệch ngẫu nhiên ± phần này (_idleAnimationRandomness).")]
        [SerializeField] private float idleRandomness = 0.25f;
        [Tooltip("Giây cho một vòng xoay của dấu ? (SecretCollectibleView._idleAnimationDuration).")]
        [SerializeField] private float spinDuration = 5.5f;
        [Tooltip("Nghỉ giữa hai vòng xoay (_idleAnimationDelay).")]
        [SerializeField] private float spinPause = 0.5f;

        private readonly Dictionary<Vector2Int, Pill> _pills = new Dictionary<Vector2Int, Pill>();
        private readonly List<(Vector2Int cell, PillKind kind)> _pending = new List<(Vector2Int, PillKind)>();
        private readonly Dictionary<Vector2Int, int> _occupied = new Dictionary<Vector2Int, int>(); // ô -> chỉ số đốt thân (-1 là đầu)
        private GameSession _session;
        private float _checkTimer;

        public void Bind(GameSession session)
        {
            _session = session;
            session.PillSpawned += (cell, kind) => _pending.Add((cell, kind));
            session.PillEaten += (cell, kind, segment) => Remove(cell);
            session.PillMagnetized += (cell, kind, column, crate) => Remove(cell);
            // Undo: trái đã ăn hiện lại khi rắn lùi qua [user]; trái của đợt mới do lượt đó sinh ra thì bỏ.
            session.Rewound += () =>
            {
                _pending.Clear();
                foreach (var cell in new List<Vector2Int>(_pills.Keys))
                    if (!session.Pills.ContainsKey(cell)) Remove(cell);
                var path = session.RewindPath;
                float length = 0f;
                for (int i = 1; i < path.Count; i++) length += Vector2.Distance(path[i - 1], path[i]);
                foreach (var pill in session.Pills)
                {
                    if (_pills.ContainsKey(pill.Key)) continue;
                    float along = 0f, reached = 0f;
                    for (int i = 1; i < path.Count; i++)
                    {
                        along += Vector2.Distance(path[i - 1], path[i]);
                        if (Vector2Int.RoundToInt(path[i]) == pill.Key) reached = along;
                    }
                    Spawn(pill.Key, pill.Value, Mathf.Max(0f, length - reached) / GameSession.RewindSpeed, inPlace: reached > 0f);
                }
            };
        }

        private void LateUpdate()
        {
            if (_session == null) return;
            if (_pending.Count > 0) SpawnPending();
            Idle(Time.deltaTime);

            _checkTimer += Time.deltaTime;
            if (_checkTimer < checkInterval) return;
            float elapsed = _checkTimer;
            _checkTimer = 0f;
            RefreshOccupancy();
            foreach (var pill in _pills.Values) CheckBlocked(pill, elapsed);
        }

        /// <summary>Trái sinh cùng frame là một đợt: rơi lần lượt, gần đầu rắn trước (gốc RespawnScheduler; đo ở LV2-3).</summary>
        private void SpawnPending()
        {
            bool wave = _pending.Count > 1;
            var head = _session.HeadPosition;
            float near = float.MaxValue, far = 0f;
            foreach (var (cell, _) in _pending)
            {
                float distance = Vector2.Distance(cell, head);
                near = Mathf.Min(near, distance);
                far = Mathf.Max(far, distance);
            }
            RefreshOccupancy();
            float last = -1f;
            foreach (var (cell, kind) in _pending)
            {
                if (!_session.Pills.ContainsKey(cell)) continue; // bị ăn ngay frame vừa sinh
                float delay = 0f;
                if (wave)
                {
                    float t = far > near ? (Vector2.Distance(cell, head) - near) / (far - near) : 0f;
                    delay = waveDelay + waveSpread * t;
                }
                Spawn(cell, kind, delay);
                last = Mathf.Max(last, delay);
            }
            _pending.Clear();
            if (last >= 0f) _session.LockInput(last + jumpDuration); // khoá vuốt tới khi mọi trái đáp xong [user]
        }

        private void Spawn(Vector2Int cell, PillKind kind, float delay, bool inPlace = false)
        {
            Remove(cell);
            var go = Instantiate(pillPrefab, transform);
            go.name = $"Pill {kind} {cell}";
            PillVisual.SetKind(go, kind);
            var pill = new Pill { go = go, cell = cell };
            if (_session.IsSecret(cell)) ShowSecret(pill);
            else FindFruit(pill, kind);
            _pills[cell] = pill;
            go.SetActive(delay <= 0f);

            // Ô đang bị rắn đè: nhảy thẳng tới chỗ chờ bên cạnh.
            var landing = Rest(cell);
            if (_occupied.ContainsKey(cell))
            {
                pill.pushed = true;
                pill.waitPosition = WaitPosition(cell);
                landing = pill.waitPosition;
            }
            StartCoroutine(JumpIn(pill, landing, delay, inPlace));
        }

        /// <summary>Trái bí ẩn: ẩn trái, hiện hộp ? trong SecreteCollectibleParent (như gốc PillView).</summary>
        private void ShowSecret(Pill pill)
        {
            if (secretPrefab == null) return;
            var fruits = pill.go.transform.Find("Visual/FruitsParent");
            var parent = pill.go.transform.Find("Visual/SecreteCollectibleParent");
            if (fruits == null || parent == null) return;
            fruits.gameObject.SetActive(false);
            var box = Instantiate(secretPrefab, parent, false);
            var burst = box.transform.Find("Feedbacks");
            if (burst != null) burst.gameObject.SetActive(false); // FX lộ trái do SnakeView chạy khi ăn
            pill.question = box.transform.Find("Visual/SecretiCollectibleQuestionMark");
            if (pill.question == null) return;
            pill.questionRest = pill.question.localRotation;
            pill.spinDuration = spinDuration * Random.Range(1f - idleRandomness, 1f + idleRandomness);
            pill.spinTime = -Random.Range(0f, spinPause);
        }

        private void FindFruit(Pill pill, PillKind kind)
        {
            pill.fruit = PillVisual.Shown(pill.go, kind);
            if (pill.fruit == null) return;
            pill.fruitZ = pill.fruit.localPosition.z;
            pill.shadow = pill.fruit.parent.Find("Shadow");
            if (pill.shadow != null) pill.shadowScale = pill.shadow.localScale;
            pill.bobDuration = idleDuration * Random.Range(1f - idleRandomness, 1f + idleRandomness);
            pill.bobTime = -Random.Range(0f, idleMaxDelay);
        }

        /// <summary>Nhún trái nằm yên và xoay dấu ?; một vòng lặp cho mọi trái thay vì mỗi trái một coroutine, cho nhẹ.</summary>
        private void Idle(float dt)
        {
            foreach (var pill in _pills.Values)
            {
                if (pill.fruit != null)
                {
                    float rise = 0f;
                    if (pill.landed && !pill.pushed)
                    {
                        pill.bobTime += dt;
                        // lên rồi xuống, chậm ở đỉnh và đáy
                        if (pill.bobTime > 0f) rise = 0.5f - 0.5f * Mathf.Cos(pill.bobTime / pill.bobDuration * Mathf.PI);
                    }
                    else pill.bobTime = 0f;
                    var p = pill.fruit.localPosition;
                    float scale = pill.fruit.parent.localScale.z; // FruitsParent scale 1.1: đổi về đơn vị bàn
                    pill.fruit.localPosition = new Vector3(p.x, p.y, pill.fruitZ - idleHeight * rise / scale);
                    if (pill.shadow != null) pill.shadow.localScale = pill.shadowScale * Mathf.Lerp(1f, shadowAtTop, rise);
                }
                if (pill.question != null)
                {
                    pill.spinTime += dt;
                    if (pill.spinTime >= pill.spinDuration + spinPause) pill.spinTime -= pill.spinDuration + spinPause;
                    float turn = pill.spinTime > 0f ? Mathf.Min(pill.spinTime / pill.spinDuration, 1f) * 360f : 0f;
                    pill.question.localRotation = pill.questionRest * Quaternion.Euler(0f, turn, 0f); // xoay quanh trục riêng, giữ độ nghiêng 40° về camera
                }
            }
        }

        private IEnumerator JumpIn(Pill pill, Vector3 landing, float delay, bool inPlace)
        {
            var go = pill.go;
            // Undo: trái hiện tại chỗ đuôi nhả ra, không nhảy [user, VFX_hoantac].
            go.transform.localPosition = inPlace ? landing : landing + new Vector3(0f, 0f, spawnZ);
            if (delay > 0f)
            {
                yield return new WaitForSeconds(delay);
                if (go == null) yield break; // bị ăn trước khi kịp hiện
                go.SetActive(true);
            }
            while (SnackDash.UI.LoadingScreen.Busy) yield return null; // trái đầu màn đợi màn hình loading tắt mới nhảy vào
            if (go == null) yield break;
            if (!inPlace) yield return Jump(go, landing + new Vector3(0f, 0f, spawnZ), landing);
            pill.landed = true;
        }

        private IEnumerator Jump(GameObject go, Vector3 from, Vector3 to)
        {
            yield return Tween.Run(jumpDuration, Tween.Linear, t =>
            {
                if (go == null) return;
                float arc = 4f * t * (1f - t) * jumpHeight;
                go.transform.localPosition = Vector3.Lerp(from, to, t) + new Vector3(0f, 0f, -arc);
            });
        }

        private void CheckBlocked(Pill pill, float elapsed)
        {
            if (!pill.landed || pill.go == null) return;
            bool occupied = _occupied.ContainsKey(pill.cell);
            if (!pill.pushed)
            {
                if (!occupied) return;
                pill.pushed = true;
                pill.clearTime = 0f;
                pill.waitPosition = WaitPosition(pill.cell);
                StartCoroutine(Slide(pill));
                return;
            }

            pill.clearTime = occupied ? 0f : pill.clearTime + elapsed;
            if (pill.clearTime < minClearTime) return;
            pill.pushed = false;
            pill.landed = false;
            StartCoroutine(DropBack(pill));
        }

        private IEnumerator Slide(Pill pill)
        {
            var go = pill.go;
            var from = go.transform.localPosition;
            yield return Tween.Run(pushDuration, Tween.OutQuad, t =>
            {
                if (go != null) go.transform.localPosition = Vector3.Lerp(from, pill.waitPosition, t);
            });
        }

        private IEnumerator DropBack(Pill pill)
        {
            yield return Jump(pill.go, pill.go.transform.localPosition, Rest(pill.cell));
            pill.landed = true;
        }

        /// <summary>Chỗ chờ cạnh thân, phía bãi cỏ (không bao giờ lên đường khác); hai phía như nhau thì lên trên / sang phải.</summary>
        private Vector3 WaitPosition(Vector2Int cell)
        {
            Vector2 along = Vector2.right;
            if (_occupied.TryGetValue(cell, out int index))
            {
                var ahead = index <= 0 ? _session.HeadPosition : _session.SegmentPosition(index - 1);
                var behind = _session.SegmentPosition(Mathf.Min(index + 1, _session.Segments.Count - 1));
                along = ahead - behind;
            }
            bool horizontal = Mathf.Abs(along.x) >= Mathf.Abs(along.y);
            var side = horizontal ? Vector2Int.up : Vector2Int.right;
            if (_session.Board.IsWalkable(cell + side) && !_session.Board.IsWalkable(cell - side)) side = -side;
            // Camera nghiêng -20° làm độ cao chờ trông như lên ~0.4 ô: phía trên đặt gần, phía dưới đặt xa hơn.
            const float raisedShift = 0.4f;
            float distance = !horizontal ? sideDistanceHorizontal
                : side.y > 0 ? sideDistanceVertical : sideDistanceVertical + 2f * raisedShift;
            return BoardView.CellToWorld(cell) + (Vector3)(Vector2)side * distance + new Vector3(0f, 0f, waitZ);
        }

        private Vector3 Rest(Vector2Int cell) => BoardView.CellToWorld(cell) + new Vector3(0f, 0f, restZ);

        private void RefreshOccupancy()
        {
            _occupied.Clear();
            _occupied[_session.CurrentCell] = -1;
            for (int i = 0; i < _session.Segments.Count; i++)
                _occupied[Vector2Int.RoundToInt(_session.SegmentPosition(i))] = i;
        }

        private void Remove(Vector2Int cell)
        {
            if (!_pills.TryGetValue(cell, out var pill)) return;
            Destroy(pill.go);
            _pills.Remove(cell);
        }
    }
}

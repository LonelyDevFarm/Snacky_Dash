using System.Collections;
using UnityEngine;

namespace SnackDash.Presentation
{
    /// <summary>
    /// Ống rẽ (prefab Prefabs/InGame/tilescovers/visualoverhaul/TileCov_Path_Turnout_*): hiện ống của hướng A hoặc B (gốc
    /// TurnoutView._positionA / _positionB, ShowPosition). Lúc đổi ống mới bật nảy nhẹ cho thấy ống vừa xoay (tự thêm, gốc chỉ đổi hình).
    /// BoardView gọi theo GameSession.TurnoutChanged.
    /// </summary>
    public class TurnoutView : MonoBehaviour
    {
        [SerializeField] private GameObject positionA, positionB;
        [Tooltip("Ống mới hiện nảy từ cỡ này lên 1 trong Pop Duration giây; 1 thì không nảy.")]
        [SerializeField] private float popFrom = 0.6f;
        [SerializeField] private float popDuration = 0.2f;

        private Coroutine _pop;

        public void Show(bool b, bool animate)
        {
            if (positionA != null) positionA.SetActive(!b);
            if (positionB != null) positionB.SetActive(b);
            var shown = b ? positionB : positionA;
            if (!animate || shown == null || popFrom >= 1f) return;
            if (_pop != null) StopCoroutine(_pop);
            _pop = StartCoroutine(Pop(shown.transform));
        }

        private IEnumerator Pop(Transform part)
        {
            var rest = Vector3.one;
            yield return Tween.Run(popDuration, Tween.OutBack(2f), t => { if (part != null) part.localScale = rest * Mathf.LerpUnclamped(popFrom, 1f, t); });
            _pop = null;
        }
    }
}

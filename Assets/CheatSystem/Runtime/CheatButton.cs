using UnityEngine;
using UnityEngine.EventSystems;

namespace CheatSystem
{
    /// <summary>
    /// Put on any UI element that takes clicks (an existing Button too: its own click still works). Tapping it
    /// <see cref="taps"/> times in a row, each within <see cref="maxGap"/> seconds of the last, opens the cheat panel.
    /// Does nothing outside the Editor and Development Builds.
    /// </summary>
    [DisallowMultipleComponent]
    public class CheatButton : MonoBehaviour, IPointerClickHandler
    {
        [Tooltip("Panel to open. Empty = Resources/CheatPanel.")]
        [SerializeField] private CheatPanel panelPrefab;
        [Tooltip("Taps in a row that open the panel.")]
        [SerializeField, Min(1)] private int taps = 5;
        [Tooltip("Most seconds between two taps of the row.")]
        [SerializeField, Min(0.05f)] private float maxGap = 0.5f;

        private int _count;
        private float _last = float.NegativeInfinity;

        /// <summary>Taps in a row that open the panel (for a CheatButton added from code).</summary>
        public int Taps { get => taps; set => taps = Mathf.Max(1, value); }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (!CheatManager.Enabled) return;
            float now = Time.unscaledTime;
            _count = now - _last <= maxGap ? _count + 1 : 1;
            _last = now;
            if (_count < taps) return;
            _count = 0;
            CheatManager.Open(panelPrefab);
        }
    }
}

using UnityEngine;
using UnityEngine.EventSystems;

namespace SnackDash.UI
{
    /// <summary>
    /// "Hold to see the board": while the pointer is held on this graphic (a popup's dimmed
    /// background), the popup and the dimming fade out; releasing brings them back.
    /// </summary>
    public class HoldToHide : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        public CanvasGroup Target;

        private CanvasGroup _self;

        private void Awake()
        {
            _self = gameObject.GetComponent<CanvasGroup>();
            if (_self == null) _self = gameObject.AddComponent<CanvasGroup>();
        }

        public void OnPointerDown(PointerEventData eventData) => Show(false);
        public void OnPointerUp(PointerEventData eventData) => Show(true);

        private void Show(bool visible)
        {
            float alpha = visible ? 1f : 0f;
            if (Target != null) { Target.alpha = alpha; Target.blocksRaycasts = visible; }
            _self.alpha = alpha; // the overlay keeps receiving the pointer while invisible
        }
    }
}

using System;
using UnityEngine;
using UnityEngine.UI;

namespace SnackDash.UI
{
    /// <summary>
    /// Mốc rương trên thanh WEEKLY ADVENTURE (prefab <c>Prefabs/UI/Popups/WeeklyMissions/WeeklyMilestone</c> và <c>WeeklyGrandMilestone</c>):
    /// rương, nút chạm (hiện bóng nói phần thưởng), dấu tích khi đã đạt. Mốc thường đã đạt chỉ còn dấu tích; giải lớn giữ rương và chữ.
    /// </summary>
    public class WeeklyMilestone : MonoBehaviour
    {
        [SerializeField] private Button button;
        [SerializeField] private Image chest;
        [SerializeField] private GameObject checkMark;
        [Tooltip("Giải lớn: vẫn hiện rương khi đã đạt.")]
        [SerializeField] private bool grand;

        public bool Grand => grand;

        public event Action Clicked;

        private void Awake() => button.onClick.AddListener(() => Clicked?.Invoke());

        public void Show(Sprite chestSprite, bool reached)
        {
            chest.sprite = chestSprite;
            bool hide = reached && !grand;
            button.gameObject.SetActive(!hide);
            chest.gameObject.SetActive(!hide);
            checkMark.SetActive(reached);
        }
    }
}

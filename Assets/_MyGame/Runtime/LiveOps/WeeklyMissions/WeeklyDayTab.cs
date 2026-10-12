using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SnackDash.UI
{
    /// <summary>
    /// Tab một ngày của WEEKLY ADVENTURE (prefab <c>Prefabs/UI/Popups/WeeklyMissions/WeeklyDayTab</c>, gốc: WeeklyMissionDayItem): nền tối / nền
    /// sáng khi chọn, chữ DAY hoặc ổ khoá, số ngày, chấm đỏ khi có nhiệm vụ nhận được.
    /// </summary>
    public class WeeklyDayTab : MonoBehaviour
    {
        [SerializeField] private Button dark;
        [SerializeField] private Button light;
        [SerializeField] private GameObject dayText;
        [SerializeField] private Image lockIcon;
        [SerializeField] private TMP_Text number;
        [SerializeField] private GameObject dot;
        [Tooltip("Độ đậm số ngày chưa chọn: ngày đã mở / chưa mở.")]
        [SerializeField] private float openAlpha = 0.85f;
        [SerializeField] private float lockedAlpha = 0.6f;

        public event Action Clicked;

        private void Awake()
        {
            dark.onClick.AddListener(() => Clicked?.Invoke());
            light.onClick.AddListener(() => Clicked?.Invoke());
        }

        public void Show(int day, bool selected, bool open, bool hasClaimable)
        {
            dark.gameObject.SetActive(!selected);
            light.gameObject.SetActive(selected);
            dayText.SetActive(open);
            lockIcon.gameObject.SetActive(!open);
            if (selected) lockIcon.color = Color.white;
            number.text = day.ToString();
            if (!selected) number.color = new Color(1f, 1f, 1f, open ? openAlpha : lockedAlpha);
            dot.SetActive(!selected && hasClaimable); // ngày đang chọn không có chấm
        }
    }
}

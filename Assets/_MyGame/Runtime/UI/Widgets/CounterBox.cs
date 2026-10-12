using TMPro;
using UnityEngine;

namespace SnackDash.UI
{
    /// <summary>Ô đếm dùng chung (tên ở trên, số ở dưới, vd. "Levels 3/7"): đổi hình / màu chữ trong prefab, script gọi <see cref="SetValue"/>.</summary>
    public class CounterBox : MonoBehaviour
    {
        [SerializeField] private TMP_Text label;
        [SerializeField] private TMP_Text value;

        public void SetLabel(string text) { if (label != null) label.text = text; }

        public void SetValue(string text) => value.text = text;

        /// <summary>Chữ số (để chạy hiệu ứng nảy).</summary>
        public TMP_Text Value => value;
    }
}

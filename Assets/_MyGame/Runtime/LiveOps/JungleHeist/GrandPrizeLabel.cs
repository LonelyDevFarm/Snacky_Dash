using SnackDash.Economy;
using TMPro;
using UnityEngine;

namespace SnackDash.UI
{
    /// <summary>
    /// Chữ "Grand Prize 10 000" của các màn Jungle Heist: {0} trong mẫu là giải lớn của Data/Config/JungleHeistConfig, viết cách
    /// nghìn bằng dấu cách như bản gốc ("10 000").
    /// </summary>
    [RequireComponent(typeof(TMP_Text))]
    public class GrandPrizeLabel : MonoBehaviour
    {
        [Tooltip("Mẫu chữ; {0} là giải lớn.")]
        [TextArea(2, 3)]
        [SerializeField] private string format = "Grand Prize\n<size=140%>{0}</size>";

        private void Awake() =>
            GetComponent<TMP_Text>().text = string.Format(format, JungleHeist.GrandPrize.ToString("#,0", System.Globalization.CultureInfo.InvariantCulture).Replace(',', ' '));
    }
}

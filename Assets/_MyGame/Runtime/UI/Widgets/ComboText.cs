using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SnackDash.UI
{
    /// <summary>
    /// Một lời khen combo (prefab <c>Prefabs/UI/Screens/Game/ComboText</c>): chữ to 2 lớp (nền + mặt vàng chuyển cam) và dòng "+N" / "TOTAL +N" với
    /// đồng xu bên phải. Nghiêng lên phải như video. <see cref="ComboView"/> sinh ra mỗi lần khen.
    /// </summary>
    public class ComboText : MonoBehaviour
    {
        [SerializeField] private TMP_Text titleBack, titleFront;
        [SerializeField] private TMP_Text valueBack, valueFront;
        [SerializeField] private RectTransform coin;
        [Tooltip("Cỡ chữ thường / bậc cao nhất.")]
        [SerializeField] private float titleSize = 125f, bigTitleSize = 150f;

        public Sprite CoinSprite => coin.GetComponent<Image>().sprite;

        public void Show(string word, string value, bool big)
        {
            float size = big ? bigTitleSize : titleSize;
            titleBack.text = titleFront.text = word;
            titleBack.fontSize = titleFront.fontSize = size;
            valueBack.text = valueFront.text = value;
            float width = valueFront.GetPreferredValues(value).x;
            coin.anchoredPosition = new Vector2(width / 2f + 40f, coin.anchoredPosition.y);
        }
    }
}

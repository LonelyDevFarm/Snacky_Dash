using System;
using System.Collections;
using SnackDash.Core;
using UnityEngine;

namespace SnackDash.UI
{
    /// <summary>Những gì trận thắng đã trả (đã vào ví) và màn thắng sẽ hiện.</summary>
    public struct VictoryReward
    {
        public int baseCoins, comboCoins;
        public bool onFire;     // chuỗi thắng đang cháy: đống xu bốc lửa, xu nhân đôi
        public int level;       // màn vừa thắng (thanh NEW ITEM)
        public Action doubled;  // x2: trả tổng thêm lần nữa
    }

    /// <summary>
    /// Mở màn thắng (<see cref="VictoryPopup"/>) sau khi thùng cuối xong: đo trên LV2-3 (10 fps), khoảng 1.3 giây sau tia sáng của thùng
    /// cuối thì nền đen hiện cùng tiêu đề.
    /// </summary>
    public class VictoryView : MonoBehaviour
    {
        [SerializeField] private float appearDelay = 1.3f;

        /// <param name="extraDelay">Cộng thêm khi thùng cuối xong muộn hơn thường lệ.</param>
        public void Show(Action onContinue, float extraDelay = 0f, UnlockPopups.Entry? unlocked = null, VictoryReward? reward = null) => StartCoroutine(Run(onContinue, extraDelay, unlocked, reward));

        private IEnumerator Run(Action onContinue, float extraDelay, UnlockPopups.Entry? unlocked, VictoryReward? reward)
        {
            yield return new WaitForSeconds(appearDelay + extraDelay);
            var popup = Services.Get<UIManager>().Show<VictoryPopup>();
            if (popup != null) popup.Play(onContinue, unlocked, reward);
        }
    }
}

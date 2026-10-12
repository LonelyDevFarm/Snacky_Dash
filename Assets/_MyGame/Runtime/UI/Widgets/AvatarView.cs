using UnityEngine;
using UnityEngine.UI;

namespace SnackDash.UI
{
    /// <summary>
    /// Ảnh đại diện một người chơi trong khung: 10 ảnh đại diện và 2 kiểu khung (thường / của mình) gắn sẵn trong prefab
    /// <c>Widgets/Avatar</c> — thay ảnh ở đó là đổi khắp các màn. Script khác sinh avatar từ prefab này rồi gọi <see cref="Set"/>.
    /// </summary>
    public class AvatarView : MonoBehaviour
    {
        [SerializeField] private Image picture;
        [SerializeField] private Image frame;
        [Tooltip("Các ảnh đại diện (TXT_Avatars_Icons_0..9).")]
        [SerializeField] private Sprite[] pictures = new Sprite[0];
        [SerializeField] private Sprite frameNormal;
        [Tooltip("Khung của chính người chơi (viền cam).")]
        [SerializeField] private Sprite framePlayer;

        /// <summary>Ảnh số <paramref name="index"/> (quay vòng trong danh sách), khung của mình nếu <paramref name="player"/>.</summary>
        public void Set(int index, bool player)
        {
            if (pictures.Length > 0) picture.sprite = pictures[Mathf.Abs(index) % pictures.Length];
            frame.sprite = player ? framePlayer : frameNormal;
        }
    }
}

using SnackDash.Economy;
using UnityEngine;

namespace SnackDash.Core
{
    /// <summary>
    /// Nơi đọc / ghi dữ liệu lưu của game. Hiện bên trong vẫn là PlayerPrefs (giữ tiến độ đang có; đổi sang file JSON khi
    /// phát hành — UNITY_WORK_RULES mục 10). Có nút xoá dữ liệu để test: chuột phải vào component trên prefab Managers,
    /// hoặc menu SnackDash/Test, hoặc tab Other của bảng cheat.
    /// Lưu ý: nhiều chỗ cũ vẫn gọi PlayerPrefs trực tiếp; sẽ chuyển dần về đây khi sửa từng phần.
    /// </summary>
    public class SaveManager : ManagerBase
    {
        /// <summary>Kho lưu dùng chung (các hệ thống logic nhận nó qua constructor).</summary>
        public IStore Store { get; private set; }

        public override void Init()
        {
            Store = new PlayerPrefsStore();
            Services.Register(this);
        }

        /// <summary>Như cài mới: Level 1, 1800 xu, 5 tim, mọi hướng dẫn và quà hiện lại.</summary>
        [ContextMenu("Xoá toàn bộ dữ liệu (như cài mới)")]
        public void ResetAllData() => ResetAll();

        /// <summary>Chỉ xoá những thứ hiện một lần (thẻ giới thiệu booster, ép dùng lần đầu, quà mở khoá); giữ level và xu.</summary>
        [ContextMenu("Xoá hướng dẫn (giữ level)")]
        public void ResetTutorialData() => ResetTutorials();

        public static void ResetAll()
        {
            PlayerPrefs.DeleteAll();
            PlayerPrefs.Save();
            Debug.Log("[Save] Đã xoá toàn bộ dữ liệu: lần mở tới như cài mới.");
        }

        public static void ResetTutorials()
        {
            foreach (var booster in new[] { "shovel", "boomerang", "undo", "magnet" })
            {
                PlayerPrefs.DeleteKey("booster_" + booster + "_tutorial");
                PlayerPrefs.DeleteKey("booster_" + booster + "_gift");
                PlayerPrefs.DeleteKey("booster_intro_" + booster.ToUpperInvariant());
            }
            PlayerPrefs.DeleteKey("home_streak_shown");
            // Lucky Ladybug: lời giới thiệu, luật chơi, bàn tay chỉ vào con bọ đầu tiên.
            foreach (var key in new[] { "lb_badge_ftue_seen", "lb_rules_seen", "lb_hand_seen" }) PlayerPrefs.DeleteKey(key);
            PlayerPrefs.Save();
            Debug.Log("[Save] Hướng dẫn và quà mở khoá sẽ hiện lại; level, xu, tim giữ nguyên.");
        }
    }
}

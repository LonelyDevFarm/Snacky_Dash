using System;

namespace SnackDash.Gameplay
{
    /// <summary>
    /// Booster Rắn bóng ma (phần sáng tạo, bản gốc không có): rắn đi xuyên qua thân mình được một số lần [user 20/10]. Một lần là lúc đầu
    /// chạm thân sau khi đang không chạm (đi dọc thân nhiều ô vẫn là một lần), không tính theo lượt vuốt vì nhiều màn điểm dừng sát nhau.
    /// Không xuyên vật chặn; vẫn không quay ngược vào đốt sau đầu. Hết khi đã dùng đủ số lần và đầu ra khỏi thân, để không bị tính cắn oan.
    /// Số lần do người gọi đưa vào (BoosterConfig); số lượng booster do người gọi lo.
    /// </summary>
    public sealed partial class GameSession
    {
        /// <summary>Bật (true) / tắt (false) bóng ma.</summary>
        public event Action<bool> GhostChanged;

        /// <summary>Vừa xuyên qua thân một lần (view loé sáng).</summary>
        public event Action GhostPassed;

        private bool _ghostOn, _ghostTouching;
        private int _ghostPassesLeft;

        public bool GhostActive => _ghostOn;

        public bool UseGhost(int passes)
        {
            if (!IsIdle || _ghostOn || passes <= 0) return false;
            _ghostOn = true;
            _ghostPassesLeft = passes;
            _ghostTouching = HeadOnBody();
            ForgetUndo();
            GhostChanged?.Invoke(true);
            return true;
        }

        /// <summary>Mỗi ô đầu vừa vào (thay cho kiểm tra cắn): đếm lần chạm mới, hết lượt và đầu đã ra khỏi thân thì tắt.</summary>
        private void GhostTouched(bool touching)
        {
            if (touching && !_ghostTouching)
            {
                _ghostPassesLeft--;
                GhostPassed?.Invoke();
            }
            _ghostTouching = touching;
            if (touching || _ghostPassesLeft > 0) return;
            _ghostOn = false;
            ForgetUndo(); // vừa đi xuyên thân: hoàn tác chờ lượt vuốt mới
            GhostChanged?.Invoke(false);
        }
    }
}

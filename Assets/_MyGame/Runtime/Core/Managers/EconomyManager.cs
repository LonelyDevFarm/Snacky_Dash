using SnackDash.Economy;

namespace SnackDash.Core
{
    /// <summary>
    /// Ví của người chơi: xu, tim, chuỗi thắng, kho booster (luật nằm ở <see cref="PlayerEconomy"/>, có test). Khi mở game,
    /// nếu lần trước đóng app giữa màn thì trừ một tim (chỉ bản build, không phải khi dừng Play trong Editor).
    /// </summary>
    public class EconomyManager : ManagerBase
    {
        public PlayerEconomy Economy { get; private set; }

        public override void Init()
        {
            Economy = PlayerEconomy.Shared;
#if !UNITY_EDITOR
            Economy.ChargeUncleanExit(); // đóng app giữa màn: mất tim đó [user]
#endif
            Services.Register(this);
        }
    }
}

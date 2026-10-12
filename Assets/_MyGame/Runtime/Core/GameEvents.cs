namespace SnackDash.Core
{
    // Các tin đăng lên EventBus. Thêm tin mới: thêm một struct ở đây, nơi gửi gọi EventBus.Publish, nơi nhận Subscribe.

    /// <summary>Một màn bắt đầu (GameBootstrap).</summary>
    public readonly struct LevelStartedEvent
    {
        public readonly int Level;
        public LevelStartedEvent(int level) { Level = level; }
    }

    /// <summary>Thắng một màn (GameBootstrap, sau khi đã cộng xu).</summary>
    public readonly struct LevelWonEvent
    {
        public readonly int Level;
        public LevelWonEvent(int level) { Level = level; }
    }

    /// <summary>Thua một màn: GAME OVER hoặc bỏ màn giữa chừng (sau khi đã trừ tim).</summary>
    public readonly struct LevelLostEvent
    {
        public readonly int Level;
        public LevelLostEvent(int level) { Level = level; }
    }
}

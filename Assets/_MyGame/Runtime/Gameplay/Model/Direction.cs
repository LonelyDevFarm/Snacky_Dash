using UnityEngine;

namespace SnackDash.Gameplay
{
    /// <summary>Bốn hướng đi của rắn trên lưới ô; dùng chung cho input, BoardGraph và GameSession.</summary>
    public enum Direction
    {
        Up,
        Down,
        Left,
        Right,
    }

    /// <summary>Đổi hướng sang bước ô / vector, lấy hướng ngược.</summary>
    public static class DirectionExtensions
    {
        public static readonly Direction[] All = { Direction.Up, Direction.Down, Direction.Left, Direction.Right };

        public static Vector2Int ToCell(this Direction direction)
        {
            switch (direction)
            {
                case Direction.Up: return Vector2Int.up;
                case Direction.Down: return Vector2Int.down;
                case Direction.Left: return Vector2Int.left;
                default: return Vector2Int.right;
            }
        }

        public static Vector2 ToVector(this Direction direction) => direction.ToCell();

        public static Direction Opposite(this Direction direction)
        {
            switch (direction)
            {
                case Direction.Up: return Direction.Down;
                case Direction.Down: return Direction.Up;
                case Direction.Left: return Direction.Right;
                default: return Direction.Left;
            }
        }

        public static bool IsHorizontal(this Direction direction) => direction == Direction.Left || direction == Direction.Right;
    }
}

using System;
using System.Collections.Generic;
using UnityEngine;

namespace SnackDash.LevelSystem
{
    /// <summary>Loại trái. Giá trị khớp enum PillKind gốc.</summary>
    public enum PillKind
    {
        Strawberry = 0,
        Blueberry = 1,
        Banana = 2,
        Pear = 3,
        Apple = 4,
        Carrot = 5,
        Eggplant = 6,
    }

    [Serializable]
    public struct CrateDefinition
    {
        public PillKind kind;
        public int requiredCount;
    }

    [Serializable]
    public class CrateColumnDefinition
    {
        public List<CrateDefinition> crates = new List<CrateDefinition>();
        public bool closedAtStart;
        public bool isExtra;
    }

    [Serializable]
    public struct PillCount
    {
        public PillKind kind;
        public int count;
    }

    /// <summary>Một đợt rải trái; trái đặt theo lớp Tiled spawn{index + 1}.</summary>
    [Serializable]
    public class SpawnBatchDefinition
    {
        public List<PillCount> pills = new List<PillCount>();
    }

    /// <summary>
    /// Dữ liệu một màn: map Tiled cùng phần gốc giữ trong LevelDataSO (cột khay, đợt rải trái, thời gian...).
    /// Asset ở Data/Levels/Definitions, sinh từ asset gốc; GameBootstrap nạp qua LevelList.
    /// </summary>
    [CreateAssetMenu(menuName = "SnackDash/Level Definition")]
    public class LevelDefinition : ScriptableObject
    {
        public TextAsset map;
        public List<CrateColumnDefinition> columns = new List<CrateColumnDefinition>();
        public List<SpawnBatchDefinition> batches = new List<SpawnBatchDefinition>();
        [Tooltip("Giới hạn thời gian (giây), 0 = không giới hạn (gốc: LevelDataSO._timeLimitSeconds).")]
        public float timeLimitSeconds;
        [Tooltip("Tốc độ rắn riêng, 0 = mặc định (gốc: LevelDataSO.speedPlayer).")]
        public float speedOverride;
        [Tooltip("Số khay phải xong để một hàng đá vỡ (gốc: destructibleWallHealth).")]
        public int wallHealth;
        [Tooltip("Độ khó: 0 thường, 2 khó, 4 siêu khó; thưởng 20 / 30 / 40 xu (gốc: LevelDataSO).")]
        public int difficulty;
        [Tooltip("Hàng đá có máu riêng, tìm theo một ô (gốc: destructibleWallGroupHealths).")]
        public List<WallGroupHealth> wallGroupHealths = new List<WallGroupHealth>();
        [Tooltip("Mọi ô dưới rèm và nhóm của nó (gốc: curtainCells).")]
        public List<CurtainCell> curtainCells = new List<CurtainCell>();
        [Tooltip("Mỗi rèm: trái phải ăn để mở và chỗ đặt bộ đếm (gốc: curtainGroups).")]
        public List<CurtainGroupDefinition> curtainGroups = new List<CurtainGroupDefinition>();
        [Tooltip("Số khay phải xong để khối nâng hạ lại (gốc: risingBlockDeliveriesToLower).")]
        public int risingBlockDeliveriesToLower = 3;
    }

    [Serializable]
    public struct CurtainCell
    {
        public Vector2Int cell;
        public int groupId;
    }

    [Serializable]
    public struct CurtainGroupDefinition
    {
        public int groupId;
        [Tooltip("Bộ đếm đặt giữa hộp ô này (gốc: counterPlacementMin/MaxCell).")]
        public Vector2Int counterMin, counterMax;
        public PillKind kind;
        public int count;
    }

    [Serializable]
    public struct WallGroupHealth
    {
        public Vector2Int seedCell;
        public int health;
    }
}

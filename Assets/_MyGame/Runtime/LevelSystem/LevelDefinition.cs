using System;
using System.Collections.Generic;
using UnityEngine;

namespace SnackDash.LevelSystem
{
    /// <summary>Fruit kinds. Values match the original PillKind enum.</summary>
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

    /// <summary>One respawn wave. Its pills are placed on the matching Tiled layer spawn{index + 1}.</summary>
    [Serializable]
    public class SpawnBatchDefinition
    {
        public List<PillCount> pills = new List<PillCount>();
    }

    /// <summary>
    /// A playable level: the Tiled board plus the data the original kept in LevelDataSO
    /// (crate columns, respawn waves, timer). Generated from the original assets.
    /// </summary>
    [CreateAssetMenu(menuName = "SnackDash/Level Definition")]
    public class LevelDefinition : ScriptableObject
    {
        public TextAsset map;
        public List<CrateColumnDefinition> columns = new List<CrateColumnDefinition>();
        public List<SpawnBatchDefinition> batches = new List<SpawnBatchDefinition>();
        [Tooltip("Original LevelDataSO._timeLimitSeconds; 0 = untimed.")]
        public float timeLimitSeconds;
        [Tooltip("Original LevelDataSO.speedPlayer; 0 = use the default speed.")]
        public float speedOverride;
        [Tooltip("Original destructibleWallHealth: crates to complete before a rock row breaks.")]
        public int wallHealth;
        [Tooltip("Original destructibleWallGroupHealths: rock rows with their own health (found by a cell).")]
        public List<WallGroupHealth> wallGroupHealths = new List<WallGroupHealth>();
    }

    [Serializable]
    public struct WallGroupHealth
    {
        public Vector2Int seedCell;
        public int health;
    }
}

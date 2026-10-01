using System;
using System.Collections.Generic;
using UnityEngine;

namespace SnackDash.LevelSystem
{
    /// <summary>One tileset entry. Field set mirrors the original TileDefinitionDatabase.Entry.</summary>
    [Serializable]
    public class TileDefinition
    {
        public string name;
        public int tiledId;
        public TileType tileType;
        public TileCategory category;
        public GameObject coverPrefab;
        public float coverZRotation;
        public bool spawnOverlayTunnelBoosterTile;
        public int deliverPointIndex;
        public int tunnelDirection;
        public int tunnelIndex;
        public bool arrowIsFixed;
        public bool arrowIsUnique;
        public int arrowGroupId;
        public int arrowDirection;
    }

    /// <summary>
    /// Maps local Tiled tile ids (raw GID with flip bits stripped, minus firstgid) to tile semantics
    /// and cover prefabs. Generated from the original TileDefinitionDatabaseVO asset.
    /// </summary>
    [CreateAssetMenu(menuName = "SnackDash/Tile Definition Database")]
    public class TileDefinitionDatabase : ScriptableObject
    {
        [SerializeField] private List<TileDefinition> entries = new List<TileDefinition>();

        private Dictionary<int, TileDefinition> _byId;

        public IReadOnlyList<TileDefinition> Entries => entries;

        public bool TryGet(int localTileId, out TileDefinition definition)
        {
            if (_byId == null)
            {
                _byId = new Dictionary<int, TileDefinition>(entries.Count);
                foreach (var entry in entries)
                    _byId[entry.tiledId] = entry;
            }
            return _byId.TryGetValue(localTileId, out definition);
        }

        public void SetEntries(List<TileDefinition> newEntries)
        {
            entries = newEntries;
            _byId = null;
        }

        private void OnValidate() => _byId = null;
    }
}

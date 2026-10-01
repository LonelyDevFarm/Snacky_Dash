using System;
using UnityEngine;

namespace SnackDash.LevelSystem
{
    /// <summary>
    /// A parsed Tiled orthogonal map (right-down render order). Cells use board coordinates with
    /// (0, 0) at the bottom-left, so Tiled row 0 becomes the top board row.
    /// </summary>
    public sealed class TiledMap
    {
        public const uint FlipMask = 0xF0000000u;

        public int Width { get; }
        public int Height { get; }
        public int FirstGid { get; }

        private readonly TiledLayerJson[] _layers;

        private TiledMap(TiledMapJson json)
        {
            Width = json.width;
            Height = json.height;
            FirstGid = json.tilesets != null && json.tilesets.Length > 0 ? json.tilesets[0].firstgid : 1;
            _layers = json.layers ?? Array.Empty<TiledLayerJson>();
        }

        public static TiledMap Parse(string json)
        {
            var data = JsonUtility.FromJson<TiledMapJson>(json);
            if (data == null || data.width <= 0 || data.height <= 0)
                throw new FormatException("Not a Tiled map: missing width/height.");
            return new TiledMap(data);
        }

        public bool HasLayer(string name) => FindLayer(name) != null;

        /// <summary>Local tile id at a board cell of a layer, or -1 when empty or the layer is absent.</summary>
        public int GetLocalTileId(string layerName, Vector2Int cell)
        {
            var layer = FindLayer(layerName);
            if (layer?.data == null || cell.x < 0 || cell.y < 0 || cell.x >= Width || cell.y >= Height)
                return -1;
            int tiledRow = Height - 1 - cell.y;
            return ToLocalTileId(layer.data[tiledRow * Width + cell.x], FirstGid);
        }

        /// <summary>Tiled GID -> tileset-local id: strip the flip/rotation flags, subtract firstgid.</summary>
        public static int ToLocalTileId(long rawGid, int firstGid)
        {
            uint gid = (uint)rawGid & ~FlipMask;
            return gid == 0 ? -1 : (int)gid - firstGid;
        }

        public string[] LayerNames()
        {
            var names = new string[_layers.Length];
            for (int i = 0; i < _layers.Length; i++) names[i] = _layers[i].name;
            return names;
        }

        private TiledLayerJson FindLayer(string name)
        {
            foreach (var layer in _layers)
                if (layer.name == name) return layer;
            return null;
        }

        [Serializable]
        private class TiledMapJson
        {
            public int width;
            public int height;
            public TiledTilesetJson[] tilesets;
            public TiledLayerJson[] layers;
        }

        [Serializable]
        private class TiledTilesetJson
        {
            public int firstgid;
            public string source;
        }

        [Serializable]
        private class TiledLayerJson
        {
            public string name;
            public string type;
            public long[] data;
        }
    }
}

using System;
using UnityEngine;

namespace SnackDash.LevelSystem
{
    /// <summary>
    /// Map Tiled (JSON trong LevelDefinition.map) đã đọc. Ô dùng tọa độ bàn, (0, 0) ở góc dưới trái,
    /// nên hàng 0 của Tiled là hàng trên cùng. GameBootstrap gọi Parse, BoardGraph và BoardView đọc.
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

        /// <summary>Id tile cục bộ tại ô, hoặc -1 khi ô trống hay không có lớp đó.</summary>
        public int GetLocalTileId(string layerName, Vector2Int cell)
        {
            var layer = FindLayer(layerName);
            if (layer?.data == null || cell.x < 0 || cell.y < 0 || cell.x >= Width || cell.y >= Height)
                return -1;
            int tiledRow = Height - 1 - cell.y;
            return ToLocalTileId(layer.data[tiledRow * Width + cell.x], FirstGid);
        }

        /// <summary>GID Tiled -> id cục bộ: bỏ bit lật/xoay, trừ firstgid.</summary>
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

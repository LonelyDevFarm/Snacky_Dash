using NUnit.Framework;
using SnackDash.LevelSystem;
using UnityEditor;
using UnityEngine;

namespace SnackDash.Tests
{
    public class TiledMapTests
    {
        [Test]
        public void LocalTileId_StripsFlipFlagsAndFirstGid()
        {
            Assert.AreEqual(287, TiledMap.ToLocalTileId(288, 1));
            Assert.AreEqual(387, TiledMap.ToLocalTileId(388 | 0x80000000L, 1));
            Assert.AreEqual(-1, TiledMap.ToLocalTileId(0, 1));
        }

        [Test]
        public void Cells_AreBottomUp_SoTiledRowZeroIsTheTopRow()
        {
            // 2x2 map, Tiled rows top to bottom: [1, 2] / [3, 4]
            var map = TiledMap.Parse("{\"width\":2,\"height\":2,\"tilesets\":[{\"firstgid\":1}]," +
                                     "\"layers\":[{\"name\":\"map\",\"data\":[1,2,3,4]}]}");
            Assert.AreEqual(0, map.GetLocalTileId("map", new Vector2Int(0, 1)));
            Assert.AreEqual(1, map.GetLocalTileId("map", new Vector2Int(1, 1)));
            Assert.AreEqual(2, map.GetLocalTileId("map", new Vector2Int(0, 0)));
            Assert.AreEqual(-1, map.GetLocalTileId("missing", Vector2Int.zero));
        }

        [Test]
        public void Level2_HasOnePlayerStartAndOneDeliveryEndPoint()
        {
            var json = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/_MyGame/Data/Levels/SourceJson/11002_v3_nt_VO.json");
            var map = TiledMap.Parse(json.text);
            int starts = 0, deliveries = 0;
            for (int y = 0; y < map.Height; y++)
            for (int x = 0; x < map.Width; x++)
            {
                if (map.GetLocalTileId("spawn", new Vector2Int(x, y)) == 287) starts++;
                if (map.GetLocalTileId("map", new Vector2Int(x, y)) == 387) deliveries++;
            }
            Assert.AreEqual(15, map.Width);
            Assert.AreEqual(19, map.Height);
            Assert.AreEqual(1, starts);
            Assert.AreEqual(1, deliveries);
        }
    }
}

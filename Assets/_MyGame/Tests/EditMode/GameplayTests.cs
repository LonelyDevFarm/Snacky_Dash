using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SnackDash.Gameplay;
using SnackDash.LevelSystem;
using UnityEditor;
using UnityEngine;

namespace SnackDash.Tests
{
    /// <summary>
    /// Scenario tests for the level data and the core rules. Extend these scenarios when a rule
    /// changes instead of adding one test per case.
    /// </summary>
    public class GameplayTests
    {
        private const string DatabasePath = "Assets/_MyGame/Data/Levels/TileDefinitionDatabase.asset";
        private const string LevelFolder = "Assets/_MyGame/Data/Levels/Definitions/";

        // 5x2 board (raw GIDs = local id + 1):
        //   top row:    grass x4, delivery end point (387)
        //   bottom row: Stop (294), road RL (350) x3, crate stop 0 (295)
        // Player starts bottom-left; wave 1 places two strawberries (marker 371) on the road.
        private const string TinyMap =
            "{\"width\":5,\"height\":2,\"tilesets\":[{\"firstgid\":1}],\"layers\":[" +
            "{\"name\":\"map\",\"data\":[343,343,343,343,388, 295,351,351,351,296]}," +
            "{\"name\":\"spawn\",\"data\":[0,0,0,0,0, 288,0,0,0,0]}," +
            "{\"name\":\"spawn1\",\"data\":[0,0,0,0,0, 0,372,372,0,0]}]}";

        private TileDefinitionDatabase _database;

        [SetUp]
        public void LoadDatabase() => _database = AssetDatabase.LoadAssetAtPath<TileDefinitionDatabase>(DatabasePath);

        [Test]
        public void TiledMap_DecodesGidsAndFlipsRows()
        {
            Assert.AreEqual(287, TiledMap.ToLocalTileId(288, 1));
            Assert.AreEqual(387, TiledMap.ToLocalTileId(388 | 0x80000000L, 1), "flip flags are stripped");
            Assert.AreEqual(-1, TiledMap.ToLocalTileId(0, 1));

            var map = TiledMap.Parse(TinyMap);
            Assert.AreEqual(387, map.GetLocalTileId("map", new Vector2Int(4, 1)), "Tiled row 0 is the top board row");
            Assert.AreEqual(294, map.GetLocalTileId("map", new Vector2Int(0, 0)));
            Assert.AreEqual(-1, map.GetLocalTileId("missing", Vector2Int.zero));
        }

        [Test]
        public void TinyBoard_EatTrailDeliverWithLock()
        {
            var map = TiledMap.Parse(TinyMap);
            var board = new BoardGraph(map, _database);
            Assert.AreEqual(new Vector2Int(0, 0), board.PlayerStart);
            Assert.AreEqual(0, board.CrateColumnAt(new Vector2Int(4, 0)));
            Assert.AreEqual(new Vector2Int(4, 1), board.CrateAnchors[0]);
            Assert.IsFalse(board.CanMove(new Vector2Int(1, 0), Direction.Up), "straight road must not open sideways");

            // Column: strawberry x1, then strawberry x3; only 2 strawberries exist.
            var level = Level((PillKind.Strawberry, 2), (PillKind.Strawberry, 1), (PillKind.Strawberry, 3));
            var session = new GameSession(board, map, level);
            session.Begin();
            Assert.AreEqual(2, session.Pills.Count);

            // Run to the crate stop eating both pills; the tail trails one cell apart.
            Assert.IsTrue(session.RequestMove(Direction.Right));
            session.Tick(0.2f);
            Assert.AreEqual(new Vector2Int(4, 0), session.CurrentCell);
            Assert.AreEqual(2, session.Segments.Count);
            Assert.AreEqual(new Vector2(3, 0), session.SegmentPosition(0));
            Assert.AreEqual(new Vector2(2, 0), session.SegmentPosition(1));

            // The first fruit fills crate 1, then delivery pauses for the completion effect.
            Assert.IsTrue(session.IsDelivering);
            session.Tick(0f); // first fruit leaves on the next tick
            Assert.AreEqual(1, session.Columns[0].CurrentIndex);
            Assert.AreEqual(1, session.Segments.Count);
            session.Tick(GameSession.CrateCompletePause * 0.5f);
            Assert.AreEqual(1, session.Segments.Count, "no delivery during the completion pause");

            // Swipes during a delivery are ignored, not buffered [tested on the original].
            Assert.IsFalse(session.RequestMove(Direction.Left), "must not leave while delivering");
            for (int i = 0; i < 40 && session.IsDelivering; i++) session.Tick(GameSession.DeliveryInterval);
            Assert.AreEqual(0, session.Segments.Count);
            Assert.AreEqual(1, session.Columns[0].Filled);
            Assert.IsFalse(session.IsMoving, "the ignored swipe must not fire after the delivery");
            Assert.IsTrue(session.RequestMove(Direction.Left), "head-only snake may turn back");
        }

        // "P" board, 3 wide x 12 high: a 3x3 loop on top whose bottom-middle Stop (1,9) joins a stem
        // down to the start Stop (1,0). Strawberries on the stem (1,1..8) and at (0,9).
        private const string LoopMap =
            "{\"width\":3,\"height\":12,\"tilesets\":[{\"firstgid\":1}],\"layers\":[" +
            "{\"name\":\"map\",\"data\":[332,351,332, 353,343,353, 332,295,332," +
            " 343,353,343, 343,353,343, 343,353,343, 343,353,343, 343,353,343, 343,353,343, 343,353,343, 343,353,343," +
            " 343,295,343]}," +
            "{\"name\":\"spawn\",\"data\":[0,0,0, 0,0,0, 0,0,0, 0,0,0, 0,0,0, 0,0,0, 0,0,0, 0,0,0, 0,0,0, 0,0,0, 0,0,0, 0,288,0]}," +
            "{\"name\":\"spawn1\",\"data\":[0,0,0, 0,0,0, 372,0,0, 0,372,0, 0,372,0, 0,372,0, 0,372,0, 0,372,0, 0,372,0, 0,372,0, 0,372,0, 0,0,0]}]}";

        [Test]
        public void LoopBoard_BiteThenSnipAndPlayOn()
        {
            var map = TiledMap.Parse(LoopMap);
            var level = Level((PillKind.Strawberry, 9), (PillKind.Banana, 9), (PillKind.Strawberry, 9)); // top crate refuses strawberries
            var session = new GameSession(new BoardGraph(map, _database), map, level);
            session.Begin();

            Assert.IsTrue(session.RequestMove(Direction.Up));
            session.Tick(1f);
            Assert.AreEqual(new Vector2Int(1, 9), session.CurrentCell);
            Assert.AreEqual(8, session.Segments.Count);

            // Round the loop (eating the 9th fruit) and back into the junction the tail still covers.
            Assert.IsTrue(session.RequestMove(Direction.Left));
            session.Tick(1f);
            Assert.IsTrue(session.IsStuck, "NO WAY OUT");
            Assert.AreEqual(new Vector2Int(1, 9), session.CurrentCell);
            Assert.AreEqual(1, session.SnipKeep, "keeps 9 / 5 = 1 segment from the head");
            Assert.AreEqual(8, session.SnipCount);
            Assert.IsFalse(session.RequestMove(Direction.Down), "no moves while stuck");

            // Snip: the cut fruit goes into the strawberry crate below the banana one, not the board.
            Assert.IsTrue(session.Snip());
            Assert.IsFalse(session.IsStuck);
            Assert.AreEqual(1, session.Segments.Count);
            Assert.AreEqual(0, session.Pills.Count);
            Assert.AreEqual(0, session.Columns[0].CurrentIndex, "a crate filled below the top waits its turn");
            Assert.AreEqual(8, session.Columns[0].FilledIn(1));
            Assert.IsTrue(session.RequestMove(Direction.Down));
        }

        [Test]
        public void Level2_MatchesFootage()
        {
            var level = Load("11002_v3_nt_VO");
            var map = TiledMap.Parse(level.map.text);

            // Spawn markers 371.. map to kinds in order of first appearance in the waves.
            var ids = SpawnIdMap.Build(level.batches);
            for (int b = 0; b < level.batches.Count; b++)
            {
                var expected = new Dictionary<PillKind, int>();
                foreach (var pill in level.batches[b].pills) expected[pill.kind] = pill.count;
                var actual = new Dictionary<PillKind, int>();
                for (int y = 0; y < map.Height; y++)
                for (int x = 0; x < map.Width; x++)
                    if (ids.TryGetValue(map.GetLocalTileId("spawn" + (b + 1), new Vector2Int(x, y)), out var kind))
                        actual[kind] = actual.TryGetValue(kind, out var n) ? n + 1 : 1;
                CollectionAssert.AreEquivalent(expected, actual, $"wave {b + 1}");
            }

            // Video LV2-3: swipe right -> along the bottom, up the right side (auto-turn) -> top crate stop.
            // Extra swipes on the way: only the latest fresh one (Left) is applied there.
            var session = new GameSession(new BoardGraph(map, _database), map, level);
            session.Begin();
            Assert.AreEqual(new Vector2Int(7, 1), session.CurrentCell);
            Assert.IsTrue(session.RequestMove(Direction.Right));
            session.Tick(0.25f);
            Assert.AreEqual(Direction.Up, session.Heading, "turned at the bottom-right corner");
            session.RequestMove(Direction.Down);
            session.RequestMove(Direction.Left);
            session.RequestMove(Direction.Right, fingerHeld: true); // further flick of a held finger: ignored
            for (int i = 0; i < 180 && session.CurrentCell != new Vector2Int(7, 16); i++) session.Tick(1f / 60f);
            Assert.AreEqual(9, session.Segments.Count, "all nine bananas on the right side eaten");
            Assert.IsTrue(session.IsMoving);
            Assert.AreEqual(Direction.Left, session.Heading);

            // ...round the left side back to the bottom stop, arriving from the left:
            // the tail is behind the head there, so turning back left is refused.
            for (int i = 0; i < 180 && session.IsMoving; i++) session.Tick(1f / 60f);
            Assert.AreEqual(new Vector2Int(7, 1), session.CurrentCell);
            Assert.IsFalse(session.RequestMove(Direction.Left), "cannot reverse into the tail");
            Assert.IsTrue(session.RequestMove(Direction.Up));
        }

        /// <summary>Every imported level (cohort ab-vo-co, Levels 1-13).</summary>
        private static IEnumerable<string> ImportedLevels() =>
            System.IO.Directory.GetFiles(LevelFolder, "*.asset").Select(System.IO.Path.GetFileNameWithoutExtension);

        [TestCaseSource(nameof(ImportedLevels))]
        public void Level_IsWinnable(string name)
        {
            // 11019 (Level 7) introduces a booster (likely Boomerang: BoosterData tutorial threshold 36 =
            // its 36 fruit): the crate loop is unreachable without it [user]. Re-enable with boosters.
            if (name == "11019_v4_nt_VO") Assert.Ignore("booster tutorial level: needs the booster");
            var level = Load(name);
            var map = TiledMap.Parse(level.map.text);
            var session = new GameSession(new BoardGraph(map, _database), map, level);
            session.SetSeed(1);
            session.Begin();
            if (!map.HasLayer("spawn1")) AssertRandomLayout(session, level.batches[0]);
            var random = new System.Random(1);
            int snips = 0;
            for (int move = 0; move < 3000 && !session.IsWon; move++)
            {
                // Biting the tail: snip and play on; the snipped fruit must stay winnable.
                if (session.IsStuck && session.Snip()) snips++;
                // Crates a snip filled complete before the snake may move again.
                for (int i = 0; i < 3600 && session.IsDelivering; i++) session.Tick(1f / 60f);
                if (!session.RequestMove((Direction)random.Next(4))) continue;
                // Long crate combos (1 s pause per filled crate) can take several seconds.
                for (int i = 0; i < 3600 && (session.IsMoving || session.IsDelivering); i++) session.Tick(1f / 60f);
            }
            Assert.IsTrue(session.IsWon, $"not won after {snips} snips");
            TestContext.WriteLine($"{name}: won, {snips} tail bites");
        }

        /// <summary>
        /// Random layout rules [user, Level 12 footage]: the whole wave is placed; segments are filled
        /// whole (at most one part way), each from one end without gaps; each kind is one block per segment.
        /// </summary>
        private static void AssertRandomLayout(GameSession session, SpawnBatchDefinition wave)
        {
            int expected = 0;
            foreach (var pill in wave.pills) expected += pill.count;
            Assert.AreEqual(expected, session.Pills.Count, "whole first wave placed");
            var slots = new HashSet<Vector2Int>(session.Board.PillSlotGroups.SelectMany(segment => segment));
            Assert.IsTrue(session.Pills.Keys.All(slots.Contains), "every fruit on a slot (none dumped next to the snake)");

            int partial = 0;
            foreach (var segment in session.Board.PillSlotGroups)
            {
                var filled = segment.Select(cell => session.Pills.ContainsKey(cell)).ToList();
                int first = filled.IndexOf(true), last = filled.LastIndexOf(true);
                if (first < 0) continue;
                Assert.IsTrue(first == 0 || last == segment.Count - 1, "filled from one end of the segment");
                Assert.IsTrue(filled.Skip(first).Take(last - first + 1).All(f => f), "no gap inside a segment");
                if (last - first + 1 < segment.Count) partial++;

                var seen = new HashSet<PillKind>();
                PillKind? previous = null;
                for (int i = first; i <= last; i++)
                {
                    var kind = session.Pills[segment[i]];
                    if (kind != previous) Assert.IsTrue(seen.Add(kind), $"{kind} comes back within a segment");
                    previous = kind;
                }
            }
            Assert.LessOrEqual(partial, 1, "segments are filled whole, at most one part way");
        }

        private static LevelDefinition Load(string name) => AssetDatabase.LoadAssetAtPath<LevelDefinition>(LevelFolder + name + ".asset");

        /// <summary>One wave and one crate column.</summary>
        private static LevelDefinition Level((PillKind kind, int count) wave, params (PillKind kind, int count)[] crates)
        {
            var level = ScriptableObject.CreateInstance<LevelDefinition>();
            var column = new CrateColumnDefinition();
            foreach (var crate in crates) column.crates.Add(new CrateDefinition { kind = crate.kind, requiredCount = crate.count });
            level.columns = new List<CrateColumnDefinition> { column };
            level.batches = new List<SpawnBatchDefinition>
            {
                new SpawnBatchDefinition { pills = { new PillCount { kind = wave.kind, count = wave.count } } }
            };
            return level;
        }
    }
}

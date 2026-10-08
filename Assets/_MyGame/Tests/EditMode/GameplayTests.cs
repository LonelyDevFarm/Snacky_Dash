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

            // An effect playing (snake drop, fruit jumping in) locks swipes, not even buffered, until its time is up.
            session.LockInput(0.3f);
            Assert.IsFalse(session.RequestMove(Direction.Right), "locked while an effect plays");
            session.Tick(0.31f);
            Assert.IsFalse(session.InputLocked);
            // A forced booster tutorial holds swipes until the booster is used.
            session.SwipesHeld = true;
            Assert.IsFalse(session.RequestMove(Direction.Right), "held by a booster tutorial");
            session.SwipesHeld = false;

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
            Assert.IsFalse(session.CanUndo, "no undo right after a snip: it waits for the next swipe");
            Assert.AreEqual(1, session.Segments.Count);
            Assert.AreEqual(0, session.Pills.Count);
            Assert.AreEqual(0, session.Columns[0].CurrentIndex, "a crate filled below the top waits its turn");
            Assert.AreEqual(8, session.Columns[0].FilledIn(1));
            Assert.IsTrue(session.RequestMove(Direction.Down));
        }

        [Test]
        public void Level2_SpawnsAndSwipes()
        {
            var level = Load("11002_v4_ftue_nt_VO"); // Level 2 in v1.15
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

            // Swipe right: along the bottom, up the right side (auto-turn) to the top crate stop (rules
            // from video LV2-3, v1.14.1; v1.15 has a smaller, symmetric Level 2). Extra swipes on the way:
            // only the latest fresh one (Left) is applied there.
            var board = new BoardGraph(map, _database);
            var session = new GameSession(board, map, level);
            session.Begin();
            var start = board.PlayerStart;
            var crateStop = board.CrateStops.Values.First();
            Assert.AreEqual(start, session.CurrentCell);
            // Tutorial: a message up blocks swipes; the forced first swipe takes only its way [user].
            session.InputBlocked = true;
            Assert.IsFalse(session.RequestMove(Direction.Right), "no swipe under a tutorial message");
            session.InputBlocked = false;
            session.ForcedSwipe = Direction.Right;
            Assert.IsFalse(session.RequestMove(Direction.Left), "only the shown swipe");
            Assert.IsTrue(session.RequestMove(Direction.Right));
            Assert.IsNull(session.ForcedSwipe, "done after the first swipe");
            session.Tick(0.25f);
            Assert.AreEqual(Direction.Up, session.Heading, "turned at the bottom-right corner");
            session.RequestMove(Direction.Down);
            session.RequestMove(Direction.Left);
            session.RequestMove(Direction.Right, fingerHeld: true); // further flick of a held finger: ignored
            for (int i = 0; i < 180 && session.CurrentCell != crateStop; i++) session.Tick(1f / 60f);
            Assert.AreEqual(crateStop, session.CurrentCell);
            Assert.Greater(session.Segments.Count, 0, "fruit on the right side eaten");
            Assert.IsTrue(session.IsMoving);
            Assert.AreEqual(Direction.Left, session.Heading);

            // ...round the left side back to the start, arriving from the left:
            // the tail is behind the head there, so turning back left is refused.
            for (int i = 0; i < 180 && session.IsMoving; i++) session.Tick(1f / 60f);
            Assert.AreEqual(start, session.CurrentCell);
            Assert.IsFalse(session.RequestMove(Direction.Left), "cannot reverse into the tail");
            Assert.IsTrue(session.RequestMove(Direction.Up));
        }

        /// <summary>Coins, lives and the win streak as the user set them (FINDINGS 07/10).</summary>
        [Test]
        public void Economy_CoinsLivesStreak()
        {
            var now = new System.DateTime(2026, 10, 7, 12, 0, 0, System.DateTimeKind.Utc);
            var eco = new SnackDash.Economy.PlayerEconomy(new SnackDash.Economy.MemoryStore(), () => now);

            // Coins: 1800 to start, 20 / 30 / 40 for a win, spending never below zero.
            Assert.AreEqual(1800, eco.Coins);
            Assert.AreEqual(20, eco.Win(5, 0));
            Assert.AreEqual(30, eco.Win(6, 2));
            Assert.AreEqual(40, eco.Win(7, 4));
            Assert.AreEqual(1890, eco.Coins);
            Assert.IsTrue(eco.TrySpend(1800));
            Assert.IsFalse(eco.TrySpend(100), "not enough coins");

            // Lives: a loss costs one, one comes back every 30 minutes, never above 5.
            Assert.AreEqual(5, eco.Lives);
            eco.Lose(); eco.Lose();
            Assert.AreEqual(3, eco.Lives);
            Assert.AreEqual(30, (int)eco.NextLifeIn.TotalMinutes);
            now = now.AddMinutes(31);
            Assert.AreEqual(4, eco.Lives);
            now = now.AddHours(5);
            Assert.AreEqual(5, eco.Lives, "back to full, not above");
            eco.AddLife();
            Assert.AreEqual(5, eco.Lives);
            for (int i = 0; i < 5; i++) eco.Lose();
            Assert.IsFalse(eco.CanPlay, "no lives left");
            eco.AddInfiniteLives(System.TimeSpan.FromMinutes(30));
            Assert.IsTrue(eco.CanPlay, "infinite lives");
            eco.Lose();
            Assert.AreEqual(5, eco.Lives, "no life lost while infinite, full when it ends");

            // Closing the app during a level costs its life on the next start.
            now = now.AddHours(3);
            eco.LevelInProgress = true;
            eco.ChargeUncleanExit();
            Assert.AreEqual(4, eco.Lives);

            // Streak: from Level 23, 5 wins (a loss in between does not reset) set it on fire, x2 coins;
            // a loss on fire resets it.
            eco.Win(22, 0);
            Assert.AreEqual(0, eco.StreakWins, "no streak before Level 23");
            eco.Win(23, 0); eco.Win(24, 0); eco.Lose(); eco.Win(25, 0); eco.Win(26, 0);
            Assert.IsFalse(eco.StreakOnFire);
            eco.Win(27, 0);
            Assert.IsTrue(eco.StreakOnFire);
            Assert.AreEqual(60, eco.Win(28, 2), "double while on fire");
            eco.Lose();
            Assert.IsFalse(eco.StreakOnFire);
            Assert.AreEqual(0, eco.StreakWins);

            // Shop packs: coins, infinite lives and boosters.
            int coins = eco.Coins;
            SnackDash.Economy.ShopCatalog.Bundles[1].Grant(eco); // Medium: 7500, 2h, shovel + boomerang x2
            Assert.AreEqual(coins + 7500, eco.Coins);
            Assert.IsTrue(eco.InfiniteLives);
            Assert.AreEqual(SnackDash.Economy.PlayerEconomy.StartBoosterStock + 2, eco.Boosters("booster_shovel"));
            Assert.AreEqual(SnackDash.Economy.PlayerEconomy.StartBoosterStock, eco.Boosters("booster_undo"));
        }

        [Test]
        public void Boosters_UndoMagnetShovel()
        {
            var level = Load("11012_v5_nt_VO");
            var map = TiledMap.Parse(level.map.text);
            var session = new GameSession(new BoardGraph(map, _database), map, level);
            session.SetSeed(2);
            session.Begin();
            var random = new System.Random(3);
            void Run() { for (int i = 0; i < 3600 && (session.IsMoving || session.IsDelivering); i++) session.Tick(1f / 60f); }

            // Undo: a swipe that eats fruit is rewound, fruit back on the board. Only the latest swipe is
            // remembered: never twice in a row, again after the next swipe [user].
            for (int m = 0; m < 50; m++)
            {
                int before = session.Segments.Count;
                if (session.RequestMove((Direction)random.Next(4))) Run();
                if (session.Segments.Count > before) break;
            }
            int pills = session.Pills.Count, segments = session.Segments.Count;
            Assert.IsTrue(session.Undo());
            Assert.AreEqual(pills + segments, session.Pills.Count + session.Segments.Count, "eaten fruit back on the board");
            // Views run back along the whole swipe, from the restored head, dropping each eaten fruit [VFX_hoantac].
            Assert.AreEqual(session.HeadPosition, session.RewindPath[0], "rewind ends where the swipe began");
            Assert.AreEqual(segments - session.Segments.Count, session.RewindDropped.Count, "eaten fruit drops off the tail");

            // Through a tunnel the rewind goes back through the tube as well [user] (Level 14).
            var tunnelLevel = Load("6072_v4_ftue_nt_VO");
            var tunnelMap = TiledMap.Parse(tunnelLevel.map.text);
            var tunnelSession = new GameSession(new BoardGraph(tunnelMap, _database), tunnelMap, tunnelLevel);
            tunnelSession.SetSeed(2);
            tunnelSession.Begin();
            bool underground = false;
            for (int m = 0; m < 200 && !underground; m++)
            {
                if (!tunnelSession.RequestMove((Direction)random.Next(4))) continue;
                bool through = false;
                for (int i = 0; i < 3600 && (tunnelSession.IsMoving || tunnelSession.IsDelivering); i++)
                {
                    tunnelSession.Tick(1f / 60f);
                    through |= tunnelSession.HeadInTunnel;
                }
                if (!through || !tunnelSession.CanUndo) continue;
                var head = tunnelSession.HeadPosition;
                Assert.IsTrue(tunnelSession.Undo());
                for (int i = 0; i < 600 && tunnelSession.IsRewinding; i++) tunnelSession.Tick(1f / 60f);
                underground = System.Linq.Enumerable.Contains(tunnelSession.RewindUnderground, true);
                if (underground) Assert.AreEqual(head, tunnelSession.RewindPath[tunnelSession.RewindPath.Count - 1]);
            }
            Assert.IsTrue(underground, "a swipe through the tube rewinds through it");
            Assert.IsFalse(session.CanUndo, "not twice in a row");
            Assert.IsTrue(session.IsRewinding && !session.RequestMove(session.Heading) && !session.RequestMove(session.Heading.Opposite()), "no swipe while it runs back [user]");
            for (int i = 0; i < 600 && session.IsRewinding; i++) session.Tick(1f / 60f);
            for (int m = 0; m < 50 && !session.CanUndo; m++)
                if (session.RequestMove((Direction)random.Next(4))) Run();
            Assert.IsTrue(session.CanUndo, "again after the next swipe");

            // Magnet: pulls fruit near the head into crates for five swipes, then goes [user].
            int pulled = 0;
            session.PillMagnetized += (c, k, col, crate) => pulled++;
            Assert.IsTrue(session.UseMagnet());
            for (int m = 0; m < 200 && session.MagnetActive; m++)
            {
                if (session.RequestMove((Direction)random.Next(4))) Run();
                session.Tick(0.1f);
            }
            Assert.IsFalse(session.MagnetActive);
            Assert.Greater(pulled, 0);

            // Shovel: only to another stop; the head comes up there and waits, the body follows out.
            var targets = session.ShovelTargets();
            Assert.IsTrue(targets.TrueForAll(c => session.Board.IsStop(c)));
            var to = targets[targets.Count - 1];
            Assert.IsTrue(session.UseShovel(to));
            Run();
            Assert.AreEqual(to, session.CurrentCell);
            Assert.IsFalse(session.IsMoving, "waits where it came up");
        }

        /// <summary>Every imported level (cohort ab-vo-co of v1.15, LevelDataImporter.LevelCount levels).</summary>
        private static IEnumerable<string> ImportedLevels() =>
            System.IO.Directory.GetFiles(LevelFolder, "*.asset").Select(System.IO.Path.GetFileNameWithoutExtension);

        [TestCaseSource(nameof(ImportedLevels))]
        public void Level_IsWinnable(string name)
        {
            // 11019 (Level 7) is the Boomerang tutorial: the crate loop is unreachable without it [user].
            bool boomerangLevel = name.EndsWith("11019_v4_nt_VO");
            int boomerangs = 0;
            var level = Load(name);
            var list = AssetDatabase.LoadAssetAtPath<LevelList>("Assets/_MyGame/Data/Levels/LevelList.asset");
            if (list != null && list.skipped.Contains(level)) Assert.Ignore("skipped in play for now (mechanic not built)");
            var map = TiledMap.Parse(level.map.text);
            var session = new GameSession(new BoardGraph(map, _database), map, level);
            session.SetSeed(1);
            session.Begin();
            if (!map.HasLayer("spawn1")) AssertRandomLayout(session, level.batches[0]);
            if (session.ExtraColumn >= 0)
            {
                // Extra crate: locked and empty, then bought at the start; no crate is lost and the level stays winnable.
                Assert.IsTrue(session.ExtraLocked);
                Assert.AreEqual(0, session.Columns[session.ExtraColumn].Crates.Count);
                int crates = session.Columns.Sum(c => c.Crates.Count);
                Assert.IsTrue(session.UnlockExtraColumn());
                Assert.IsFalse(session.ExtraLocked);
                Assert.AreEqual(crates, session.Columns.Sum(c => c.Crates.Count), "the crates are shared out, none lost");
                Assert.Greater(session.Columns[session.ExtraColumn].Crates.Count, 0);
                if (name.EndsWith("11070_v4_ftue_nt_VO")) // Level 30, as on the user's screenshots: new column yellow, orange, red...
                {
                    CollectionAssert.AreEqual(new[] { 2, 5, 0, 4, 5, 0 }, session.Columns[session.ExtraColumn].Crates.Select(c => (int)c.kind).ToArray());
                    CollectionAssert.AreEqual(new[] { 2, 0, 4, 5, 5 }, session.Columns[0].Crates.Select(c => (int)c.kind).ToArray(), "the old column: yellow, red, green...");
                }
            }
            foreach (var gate in session.Board.OneWayGates)
            {
                var way = gate.Value;
                Assert.IsTrue(session.Board.CanMove(gate.Key, way), "a gate lets the snake through its way");
                Assert.IsFalse(session.Board.CanMove(gate.Key + way.ToCell(), way.Opposite()), "a gate is a wall the other way");
            }
            if (!map.HasLayer("spawn1"))
                Assert.IsFalse(session.Pills.Keys.Any(cell => BoardGraph.HasRoadObject(map, cell)
                        || DirectionExtensions.All.Any(d => BoardGraph.HasRoadObject(map, cell + d.ToCell()))),
                    "no fruit on or next to an object on the road (bridge, gate...)");
            int shakes = 0;
            session.ArrowNotAllowed += cell => shakes++;
            var random = new System.Random(1);
            int snips = 0;
            for (int move = 0; move < 3000 && !session.IsWon; move++)
            {
                // Biting the tail: snip and play on; the snipped fruit must stay winnable.
                if (session.IsStuck && session.Snip()) snips++;
                // Crates a snip filled complete before the snake may move again.
                for (int i = 0; i < 3600 && session.IsDelivering; i++) session.Tick(1f / 60f);
                // The booster tutorial level: the crate is out of reach, the boomerang feeds it.
                if (boomerangLevel && session.Pills.Count == 0)
                    for (int c = 0; c < session.Columns.Count; c++)
                        if (session.UseBoomerang(c))
                        {
                            boomerangs++;
                            for (int i = 0; i < 3600 && session.IsDelivering; i++) session.Tick(1f / 60f);
                        }
                if (!session.RequestMove((Direction)random.Next(4))) continue;
                // Long crate combos (1 s pause per filled crate) can take several seconds.
                for (int i = 0; i < 3600 && (session.IsMoving || session.IsDelivering); i++)
                {
                    session.Tick(1f / 60f);
                    // Every shown segment lies on a road: never across the grass between tunnel mouths.
                    for (int k = 0; k < session.Segments.Count; k++)
                        if (!session.SegmentInTunnel(k))
                            Assert.IsTrue(session.Board.IsWalkable(Vector2Int.RoundToInt(session.SegmentPosition(k))),
                                $"segment {k} off the road at {session.SegmentPosition(k)}");
                }
                Assert.IsFalse(session.Board.IsBridge(session.CurrentCell), "the snake never stops on a bridge");
                Assert.IsFalse(session.Board.TunnelMouths.ContainsKey(session.CurrentCell), "the snake never stops at a tunnel mouth");
            }
            Assert.IsTrue(session.IsWon, $"not won after {snips} snips");
            if (boomerangLevel) Assert.AreEqual(1, boomerangs, "one free boomerang wins the tutorial level");
            // Running past (or stopping beside) a closed gate or arrow shakes it, every time [user].
            if (session.Board.OneWayGates.Count > 0) Assert.Greater(shakes, 0, "closed gates shake as the snake goes by");
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
            foreach (var bridge in session.Board.Bridges)
            {
                // On the deck: 2 cells each way along it; under it: 1 cell each way along the road below.
                var along = bridge.Value ? Vector2Int.right : Vector2Int.up;
                var across = bridge.Value ? Vector2Int.up : Vector2Int.right;
                for (int i = -2; i <= 2; i++) Assert.IsFalse(session.Pills.ContainsKey(bridge.Key + along * i), "no fruit on a bridge");
                Assert.IsFalse(session.Pills.ContainsKey(bridge.Key + across) || session.Pills.ContainsKey(bridge.Key - across), "no fruit under a bridge");
            }

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

        // Assets are named LevelNN_<map>; a bare map name also finds its file.
        private static LevelDefinition Load(string name) => AssetDatabase.LoadAssetAtPath<LevelDefinition>(
            System.IO.Directory.GetFiles(LevelFolder, "*" + name + ".asset")[0].Replace('\\', '/'));

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

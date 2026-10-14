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

            // Cheats: LOSE bites where the snake stands (the usual NO WAY OUT); WIN ends the level even while moving.
            session = new GameSession(new BoardGraph(map, _database), map, level);
            session.Begin();
            Assert.IsFalse(session.CheatBite(), "no tail yet");
            Assert.IsTrue(session.RequestMove(Direction.Up));
            session.Tick(1f);
            int bitten = -1;
            session.TailBitten += keep => bitten = keep;
            Assert.IsTrue(session.CheatBite());
            Assert.IsTrue(session.IsStuck);
            Assert.AreEqual(1, bitten, "8 segments / 5 keeps 1, as a real bite");
            Assert.IsFalse(session.CheatWin(), "not while stuck");
            Assert.IsTrue(session.Snip());
            Assert.IsTrue(session.RequestMove(Direction.Left));
            bool won = false;
            session.Won += () => won = true;
            Assert.IsTrue(session.CheatWin());
            Assert.IsTrue(won && session.IsWon);
            Assert.IsFalse(session.CheatWin(), "only once");
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

            // Cheats set coins, streak and boosters without counting for the missions.
            int counted = 0;
            eco.Goal += (goal, amount) => counted += amount;
            eco.SetCoins(123);
            Assert.AreEqual(123, eco.Coins);
            eco.SetStreak(true);
            Assert.IsTrue(eco.StreakOnFire);
            eco.SetStreak(false);
            Assert.AreEqual(0, eco.StreakWins);
            eco.SetBoosters("booster_shovel", 0, countUse: false);
            Assert.AreEqual(0, eco.Boosters("booster_shovel"));
            Assert.AreEqual(0, counted, "no mission progress from cheats");
        }

        [Test]
        public void Events_WeeklyMissionsAndSnackyPass()
        {
            var now = new System.DateTime(2026, 10, 8, 9, 0, 0, System.DateTimeKind.Utc);
            var store = new SnackDash.Economy.MemoryStore();
            var eco = new SnackDash.Economy.PlayerEconomy(store, () => now);
            var wm = new SnackDash.Economy.WeeklyMissions(store, () => now, eco);

            // Opens at Level 15; the first day counts as a login.
            wm.StartIfUnlocked(14);
            Assert.IsFalse(wm.Started);
            wm.StartIfUnlocked(15);
            Assert.AreEqual(1, wm.Day);
            Assert.IsTrue(wm.Claimable(1, 0), "Login 1 Day!");
            Assert.AreEqual("Win 5 Levels In A Row!", SnackDash.Economy.WeeklyMissions.MissionAt(1, 1).Text);

            // Wins in a row start over after a loss; a done mission stays done.
            for (int i = 0; i < 4; i++) eco.Win(20, 0);
            eco.Lose();
            Assert.AreEqual(0, wm.Progress(1, 1));
            for (int i = 0; i < 5; i++) eco.Win(20, 2);
            Assert.IsTrue(wm.Done(1, 1));
            eco.Lose();
            Assert.IsTrue(wm.Done(1, 1), "done stays done");
            Assert.AreEqual(8, wm.Progress(1, 2), "Win 8 Levels! (9 wins, shown up to 8)");
            Assert.AreEqual(2, wm.Progress(2, 2), "hard wins count for a later day too");

            // Boosters used count by kind; buying does not.
            eco.AddBoosters("booster_shovel", 3);
            eco.SetBoosters("booster_shovel", 2);
            Assert.IsTrue(wm.Done(1, 3), "Use 1 Shovel!");
            Assert.AreEqual(1, wm.Progress(2, 3), "Use 2 Boosters! counts any booster");

            // A later day cannot be claimed yet; claiming gives the reward and tokens; milestones on the way.
            Assert.IsTrue(wm.Done(2, 2));
            Assert.AreEqual(0, wm.Claim(2, 2).Count, "Unlock at Day 2!");
            Assert.IsFalse(wm.Claimed(2, 2));
            int coins = eco.Coins;
            wm.Claim(1, 3); // 50 tokens + 50 coins
            Assert.AreEqual(coins + 50, eco.Coins);
            wm.Claim(1, 0); // 20 tokens + infinite lives 15 min
            Assert.IsTrue(eco.InfiniteLives);
            var reached = wm.Claim(1, 1); // 50 -> 120
            Assert.AreEqual(0, reached.Count);
            reached = wm.Claim(1, 2); // 20 -> 140
            Assert.AreEqual(140, wm.Points);
            Assert.AreEqual(0, reached.Count);
            Assert.AreEqual(2, wm.ClaimableCount, "Earn 250 Coins! (wins and the shovel reward) and day 2 Win 2 Hard Levels!");
            reached = wm.Claim(1, 4); // 20 -> 160: first milestone (150)
            CollectionAssert.AreEqual(new[] { 0 }, reached);
            Assert.AreEqual(1, wm.MilestonesReached);

            // Day 2 opens the next day; a new week starts after seven days.
            now = now.AddDays(1);
            wm.Login();
            Assert.AreEqual(2, wm.Day);
            Assert.AreEqual(2, wm.Progress(2, 0), "Login 2 Days!");
            wm.Claim(2, 2); // Win 2 Hard Levels! can be claimed now
            Assert.IsTrue(wm.Claimed(2, 2));
            now = now.AddDays(7);
            wm.StartIfUnlocked(30);
            Assert.AreEqual(1, wm.Day);
            Assert.AreEqual(0, wm.Points, "new week");
            Assert.IsFalse(wm.Claimed(2, 2));

            // Snacky Pass (Level 25): normal 1 ticket, hard 2, super hard 3; stage k needs k - 1 more than stage k - 1.
            store = new SnackDash.Economy.MemoryStore();
            eco = new SnackDash.Economy.PlayerEconomy(store, () => now);
            var sp = new SnackDash.Economy.SeasonPass(store, () => now, eco);
            sp.StartIfUnlocked(24);
            Assert.IsFalse(sp.Started);
            sp.StartIfUnlocked(25);
            Assert.AreEqual(1, sp.Stage);
            Assert.IsTrue(sp.Claimable(1, false), "stage 1's free reward from the start");
            Assert.IsFalse(sp.Claimable(1, true), "premium needs the pass");
            Assert.AreEqual((0, 1), sp.Progress);
            eco.Win(25, 0);
            Assert.AreEqual(2, sp.Stage, "1 ticket opens stage 2");
            eco.Win(26, 2);
            Assert.AreEqual(3, sp.Tickets);
            Assert.AreEqual(3, sp.Stage, "3 tickets open stage 3");
            eco.Win(27, 4);
            Assert.AreEqual(6, sp.Tickets);
            Assert.AreEqual(4, sp.Stage);
            Assert.AreEqual((0, 4), sp.Progress);
            Assert.AreEqual(4, sp.ClaimableCount);

            // Claiming; the active pass opens premium and raises the lives to 8 for the season.
            sp.Claim(1, false);
            Assert.IsTrue(eco.InfiniteLives, "stage 1: 10 minutes of infinite lives");
            sp.Activate();
            Assert.AreEqual(8, eco.LifeCap);
            Assert.AreEqual(8, eco.Lives);
            int before = eco.Boosters("booster_boomerang");
            Assert.IsTrue(sp.Claim(2, true));
            Assert.AreEqual(before + 1, eco.Boosters("booster_boomerang"));
            Assert.IsFalse(sp.Claim(2, true), "once");
            Assert.IsFalse(sp.Claim(5, false), "stage 5 is not open yet");

            // The premium lane adds up to the offer: 3000 coins, x10 of each booster, 6 hours.
            int paidCoins = 0, minutes = 0; var boosters = new int[4];
            foreach (var r in SnackDash.Economy.SeasonPassData.Premium)
            {
                paidCoins += r.reward.coins; minutes += r.reward.infiniteMinutes;
                for (int b = 0; b < 4; b++) boosters[b] += r.reward.Boosters[b];
            }
            Assert.AreEqual(3000, paidCoins);
            Assert.AreEqual(360, minutes);
            CollectionAssert.AreEqual(new[] { 10, 10, 10, 10 }, boosters);

            // A new season after seven days: back to nothing and 5 lives.
            now = now.AddDays(7);
            sp.StartIfUnlocked(30);
            Assert.AreEqual(0, sp.Tickets);
            Assert.IsFalse(sp.Premium);
            Assert.AreEqual(5, eco.LifeCap);
            Assert.IsFalse(sp.Claimed(1, false));

            // Jungle Heist (Level 35): join, 7 wins in a row finish it (the share is paid once); a loss fails it; then wait for the next cycle.
            var jh = new SnackDash.Economy.JungleHeist(store, () => now, eco);
            jh.StartIfUnlocked(34);
            Assert.IsFalse(jh.Started);
            jh.StartIfUnlocked(35);
            Assert.IsTrue(jh.CanJoin);
            jh.Join();
            for (int i = 0; i < 3; i++) eco.Win(40 + i, 0);
            Assert.AreEqual(3, jh.Step);
            Assert.AreEqual(SnackDash.Economy.JungleHeist.SurvivorsPerStep[3], jh.Survivors);
            Assert.IsTrue(jh.HasNews, "Home opens the river for wins not shown yet");
            jh.SeenStep = 3;
            Assert.IsFalse(jh.HasNews);
            eco.Lose();
            Assert.AreEqual(SnackDash.Economy.JungleHeist.State.Lost, jh.Current, "one lost level fails the challenge");
            Assert.IsTrue(jh.HasNews, "and for the failed run, once");
            jh.ResultSeen = true;
            Assert.IsFalse(jh.HasNews);
            Assert.IsFalse(jh.CanJoin, "and the next run waits for the next cycle");
            now = now.AddHours(SnackDash.Economy.JungleHeist.CycleHours);
            jh.StartIfUnlocked(40);
            Assert.IsTrue(jh.CanJoin);
            jh.Join();
            for (int i = 0; i < SnackDash.Economy.JungleHeist.Steps; i++) eco.Win(50 + i, 0);
            Assert.AreEqual(SnackDash.Economy.JungleHeist.State.Won, jh.Current);
            Assert.IsFalse(jh.ResultSeen, "a new cycle starts unseen");
            int wallet = eco.Coins;
            Assert.IsTrue(jh.ClaimPrize());
            jh.SeenStep = SnackDash.Economy.JungleHeist.Steps;
            Assert.IsFalse(jh.HasNews, "the prize taken, nothing left to open for");
            Assert.AreEqual(wallet + SnackDash.Economy.JungleHeist.Share, eco.Coins);
            Assert.IsFalse(jh.ClaimPrize(), "once");

            // EventBus: the game-flow news (level won / lost) reach whoever listens, and stop once unsubscribed.
            int heard = 0;
            System.Action<SnackDash.Core.LevelWonEvent> onWon = e => heard += e.Level;
            SnackDash.Core.EventBus.Subscribe(onWon);
            SnackDash.Core.EventBus.Publish(new SnackDash.Core.LevelWonEvent(7));
            SnackDash.Core.EventBus.Publish(new SnackDash.Core.LevelLostEvent(9));
            SnackDash.Core.EventBus.Unsubscribe(onWon);
            SnackDash.Core.EventBus.Publish(new SnackDash.Core.LevelWonEvent(5));
            Assert.AreEqual(7, heard, "only the won news while subscribed");
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

            // Magnet: pulls fruit near the head into crates while it moves, for five swipes, then goes [user].
            int pulled = 0;
            session.PillMagnetized += (c, k, col, crate) => pulled++;
            Assert.IsTrue(session.UseMagnet());
            for (int i = 0; i < 20; i++) session.Tick(0.1f);
            Assert.AreEqual(0, pulled, "standing still (just switched on, or after the shovel) pulls nothing: only moving does [user]");
            var dig = session.ShovelTargets();
            if (dig.Count > 0 && session.UseShovel(dig[0]))
            {
                Run();
                for (int i = 0; i < 20; i++) session.Tick(0.1f);
                Assert.AreEqual(0, pulled, "the shovel trip and standing where it came up pull nothing either [user]");
            }
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
                if (session.Board.IsCovered(gate.Key) || session.Board.IsCovered(gate.Key + way.ToCell())) continue; // under a curtain: shut until it opens
                Assert.IsTrue(session.Board.CanMove(gate.Key, way), "a gate lets the snake through its way");
                Assert.IsFalse(session.Board.CanMove(gate.Key + way.ToCell(), way.Opposite()), "a gate is a wall the other way");
            }
            // A rock under a bridge (ldf_below) shuts the road below, not the deck.
            foreach (var bridge in session.Board.Bridges)
            {
                if (!session.Board.IsClosedWall(bridge.Key)) continue;
                var deck = bridge.Value ? Direction.Right : Direction.Up;
                var below = bridge.Value ? Direction.Up : Direction.Right;
                Assert.IsTrue(session.Board.CanMove(bridge.Key, deck) || session.Board.CanMove(bridge.Key, deck.Opposite()), "the deck stays open over a rock");
                Assert.IsFalse(session.Board.CanMove(bridge.Key, below) || session.Board.CanMove(bridge.Key, below.Opposite()), "the rock shuts the road under the bridge");
            }
            // A rising block that is down takes fruit like a road [user].
            bool RoadObject(Vector2Int cell) => BoardGraph.HasRoadObject(map, cell) && !session.Board.RisingBlocks.Contains(cell);
            if (!map.HasLayer("spawn1"))
                Assert.IsFalse(session.Pills.Keys.Any(cell => RoadObject(cell)
                        || DirectionExtensions.All.Any(d => RoadObject(cell + d.ToCell()))),
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

using System.Collections.Generic;
using SnackDash.LevelSystem;
using UnityEngine;

namespace SnackDash.Gameplay
{
    /// <summary>
    /// Khay đôi (phần sáng tạo, bản gốc không có; chỉ màn sau 366) [user 23/10]: hai chồng khay cùng số tầng đứng chung một điểm giao, nối
    /// dây theo từng tầng. Dừng ở điểm giao thì mỗi trái bay vào khay cùng màu của tầng đang trên cùng; khay đầy trước đứng chờ (không nhận
    /// thêm) tới khi khay bạn cũng đầy, rồi cả hai cùng xong (tường đá / khối nâng đếm 2 khay). Booster coi cả tầng là một khay: boomerang,
    /// Khay thần kỳ lấp cả hai. Cột thứ hai của cặp ghi <c>pairedWithPrevious</c>; điểm giao trên bản đồ thuộc cột đầu. Không đi cùng khay
    /// đóng / mở và khay phụ.
    /// </summary>
    public sealed partial class GameSession
    {
        private readonly Dictionary<int, int> _partner = new Dictionary<int, int>();

        /// <summary>Cột khay ghép với <paramref name="column"/>, hoặc -1.</summary>
        public int PartnerOf(int column) => _partner.TryGetValue(column, out int partner) ? partner : -1;

        /// <summary>Cột đầu của cặp (có điểm giao trên bản đồ); cột thường trả chính nó.</summary>
        public int PairLead(int column) => PartnerOf(column) is int partner && partner >= 0 && partner < column ? partner : column;

        private void BuildPairs(LevelDefinition level)
        {
            for (int column = 1; column < level.columns.Count; column++)
            {
                if (!level.columns[column].pairedWithPrevious) continue;
                _partner[column] = column - 1;
                _partner[column - 1] = column;
                if (Columns[column].Crates.Count != Columns[column - 1].Crates.Count)
                    Debug.LogError($"Khay đôi cột {column - 1}-{column}: số tầng khác nhau ({Columns[column - 1].Crates.Count} / {Columns[column].Crates.Count}).");
                // Cặp chỉ có một điểm giao (của cột đầu); điểm giao riêng của cột sau sẽ giao được vào cặp mà không đứng cạnh nó.
                if (_board.CrateStops.ContainsKey(column))
                    Debug.LogError($"Khay đôi cột {column - 1}-{column}: bản đồ còn điểm giao của cột {column}, cặp chỉ dùng điểm giao cột {column - 1}.");
            }
        }

        /// <summary>Cả hai khay của tầng trên cùng đã đầy (chờ xong cùng nhau).</summary>
        private bool PairFull(int lead) =>
            !Columns[lead].IsComplete && Columns[lead].IsFull(Columns[lead].CurrentIndex) && Columns[PartnerOf(lead)].IsFull(Columns[lead].CurrentIndex);

        /// <summary>Khay của <paramref name="column"/> ở tầng trên cùng còn nhận trái rắn đang mang.</summary>
        private bool PairCrateAccepts(int column)
        {
            var stack = Columns[column];
            return !stack.IsComplete && !stack.IsFull(stack.CurrentIndex) && _segments.Contains(stack.Current.kind);
        }

        private bool CanDeliverToPair(int column)
        {
            int lead = PairLead(column);
            return PairCrateAccepts(lead) || PairCrateAccepts(PartnerOf(lead));
        }

        /// <summary>
        /// Khay sẽ nhận trái kế: khay còn chỗ có đốt cùng màu gần đuôi nhất (giao thường) hoặc gần đầu nhất (boomerang).
        /// </summary>
        private int PairTarget(int lead, bool fromHead, out int segment)
        {
            int best = -1;
            segment = -1;
            int tier = Columns[lead].CurrentIndex;
            foreach (int column in new[] { lead, PartnerOf(lead) })
            {
                var stack = Columns[column];
                if (stack.IsFull(tier)) continue;
                var kind = stack.Crates[tier].kind;
                int index = fromHead ? _segments.IndexOf(kind) : _segments.LastIndexOf(kind);
                if (index < 0) continue;
                if (best < 0 || (fromHead ? index < segment : index > segment)) { best = column; segment = index; }
            }
            return best;
        }

        /// <summary>Một trái vào cặp khay (giao thường hoặc boomerang); cả hai đầy thì cùng xong. Trả về còn giao tiếp không.</summary>
        private bool DeliverPair(int lead)
        {
            if (Columns[lead].IsComplete) return false;
            int tier = Columns[lead].CurrentIndex;
            if (PairFull(lead)) return CompletePair(lead);
            if (!_deliverFromTail) return false;
            if (_boomerang && _boomerangLeft <= 0) return false;

            int column = PairTarget(lead, _boomerang, out int index);
            if (column < 0) return false;
            var stack = Columns[column];
            var kind = stack.Crates[tier].kind;
            _segments.RemoveAt(index);
            stack.Add(tier);
            _deliveredThisRun++;
            if (_boomerang)
            {
                _boomerangLeft--;
                SegmentSnipped?.Invoke(index, kind, column, tier);
                SegmentRemoved?.Invoke(index);
                if (PairFull(lead))
                {
                    bool more = CompletePair(lead);
                    _deliveryTimer = BoomerangInterval - BoomerangCrateGap;
                    return more && _boomerangLeft > 0;
                }
                return _boomerangLeft > 0;
            }
            _deliveredSinceSwipe.Add(kind);
            SegmentDelivered?.Invoke(index, kind, column);
            if (PairFull(lead)) return CompletePair(lead);
            return true;
        }

        /// <summary>Hai khay của tầng cùng xong: hai lần "xong khay" (tường đá, khối nâng đếm 2).</summary>
        private bool CompletePair(int lead)
        {
            CompleteCurrent(lead);
            return CompleteCurrent(PartnerOf(lead));
        }

        /// <summary>Boomerang vào cặp: đốt gần đầu nhất cho cả hai khay của tầng; tầng dưới chỉ khi tầng này sẽ đầy cả hai.</summary>
        private List<(int segment, int crate)> PlanPairBoomerang(int lead)
        {
            var plan = new List<(int segment, int crate)>();
            var taken = new HashSet<int>();
            var stacks = new[] { Columns[lead], Columns[PartnerOf(lead)] };
            for (int crate = stacks[0].CurrentIndex; crate < stacks[0].Crates.Count; crate++)
            {
                int left = 0;
                foreach (var stack in stacks)
                {
                    int room = stack.Crates[crate].requiredCount - stack.FilledIn(crate);
                    var kind = stack.Crates[crate].kind;
                    for (int i = 0; i < _segments.Count && room > 0; i++)
                    {
                        if (_segments[i] != kind || taken.Contains(i)) continue;
                        taken.Add(i);
                        plan.Add((i, crate));
                        room--;
                    }
                    left += room;
                }
                if (left > 0) break;
            }
            return plan;
        }

        /// <summary>Khay thần kỳ chọn được cặp: tầng trên cùng chưa đầy cả hai, không dưới rèm.</summary>
        private bool MagicPair(int lead) => !Columns[lead].IsComplete && !PairFull(lead) && !IsColumnCovered(lead) && !IsColumnCovered(PartnerOf(lead));

        /// <summary>
        /// Trái Khay thần kỳ lấy cho cả tầng của cặp, kèm khay nhận: hai màu khác nhau thì lập riêng từng khay; cùng màu thì lập một lần
        /// cho tổng chỗ trống (không lấy trùng một trái), phần đầu vào khay đầu.
        /// </summary>
        private List<(MagicCratePlanner.Pick pick, int column)> PlanMagicPair(int lead)
        {
            var result = new List<(MagicCratePlanner.Pick, int)>();
            if (!IsIdle || !MagicPair(lead)) return result;
            int partner = PartnerOf(lead);
            int tier = Columns[lead].CurrentIndex;
            int needLead = Columns[lead].Crates[tier].requiredCount - Columns[lead].FilledIn(tier);
            int needPartner = Columns[partner].Crates[tier].requiredCount - Columns[partner].FilledIn(tier);
            var kindLead = Columns[lead].Crates[tier].kind;
            var kindPartner = Columns[partner].Crates[tier].kind;
            if (kindLead == kindPartner)
            {
                var picks = PlanMagicFor(kindLead, needLead + needPartner);
                for (int i = 0; i < picks.Count; i++) result.Add((picks[i], i < needLead ? lead : partner));
                return result;
            }
            foreach (var pick in PlanMagicFor(kindLead, needLead)) result.Add((pick, lead));
            foreach (var pick in PlanMagicFor(kindPartner, needPartner)) result.Add((pick, partner));
            return result;
        }

        private List<MagicCratePlanner.Pick> PlanMagicFor(PillKind kind, int need)
        {
            if (need <= 0) return new List<MagicCratePlanner.Pick>();
            var ahead = new List<int>();
            for (int wave = BatchIndex + 1; wave < _level.batches.Count; wave++) ahead.Add(AheadAvailable(wave, kind));
            return MagicCratePlanner.Plan(kind, need, HeadPosition, _pills, _secret, ahead, BatchIndex + 1, _segments);
        }

        /// <summary>Khay thần kỳ trên cặp: như một khay, trái bàn / đợt sau bay vào khay của nó, trái trên đuôi đi đường boomerang.</summary>
        private bool UseMagicPair(int lead)
        {
            var plan = PlanMagicPair(lead);
            if (plan.Count == 0) return false;
            int tier = Columns[lead].CurrentIndex;
            ForgetUndo();
            var tail = new List<int>();
            foreach (var (pick, _) in plan)
                if (pick.source == MagicCratePlanner.Source.Tail) tail.Add(pick.segment);
            int early = plan.Count - tail.Count;
            MagicCrateUsed?.Invoke(lead, early);

            foreach (var (pick, column) in plan)
            {
                if (pick.source == MagicCratePlanner.Source.Tail) continue;
                var kind = Columns[column].Crates[tier].kind;
                Columns[column].Add(tier);
                CountCurtainFruit(kind);
                if (pick.source == MagicCratePlanner.Source.Board)
                {
                    _pills.Remove(pick.cell);
                    PillMagnetized?.Invoke(pick.cell, kind, column, tier);
                }
                else
                {
                    var cell = TakeAhead(pick.wave, kind);
                    FruitTakenAhead?.Invoke(cell, kind, column, tier);
                }
            }
            _deliveredThisRun += early;
            if (_pills.Count == 0) SpawnNextBatch();
            EnsureShutterProgress();
            if (tail.Count > 0)
            {
                BoomerangThrown?.Invoke(lead, tail);
                _deliveryColumn = lead;
                _deliverFromTail = true;
                _boomerang = true;
                _boomerangLeft = tail.Count;
                _deliveryTimer = BoomerangInterval - early * MagicCrateInterval;
                return true;
            }
            StartSettlingFullCrates(early * MagicCrateInterval);
            return true;
        }
    }
}

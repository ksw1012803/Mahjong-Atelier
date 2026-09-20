using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace MahjongAtelier.Core
{
    /// <summary>
    /// 플레이어 v4.2 — IsAgari 디버그 강화.
    /// </summary>
    public sealed class MahjongPlayer
    {
        public int Index { get; }
        public PlayerType Type { get; set; }
        public SeatWind Seat { get; set; }

        public bool IsBot => Type == PlayerType.Bot;
        public bool IsHuman => Type == PlayerType.Human;
        public bool IsDealer => Seat == SeatWind.East;

        public List<TileData> HandTiles { get; } = new List<TileData>();
        public List<TileData> DiscardPile { get; } = new List<TileData>();
        public List<Meld> CalledMelds { get; } = new List<Meld>();
        public List<CalledMeld> CalledMeldsExt { get; } = new List<CalledMeld>();

        public bool IsRiichi { get; private set; }
        public int RiichiTurn { get; private set; } = -1;
        public int RiichiDiscardIndex { get; private set; } = -1;
        public bool IsIppatsuChance { get; private set; }
        public List<TileKind> RiichiWaits { get; private set; } = new List<TileKind>();

        /// <summary>P0만 디버그 로그 활성화 (소음 방지).</summary>
        public static bool DebugAgariForUser = true;

        public MahjongPlayer(int index, PlayerType type = PlayerType.Human)
        {
            Index = index;
            Type = type;
            Seat = (SeatWind)index;
        }

        public void AddTile(TileData tile)
        {
            HandTiles.Add(tile);
            GameEvents.RaiseHandChanged(Index);
        }

        public bool RemoveTile(TileData tile)
        {
            bool removed = HandTiles.Remove(tile);
            if (removed) GameEvents.RaiseHandChanged(Index);
            return removed;
        }

        public void DiscardTile(TileData tile)
        {
            if (RemoveTile(tile))
            {
                DiscardPile.Add(tile);
                GameEvents.RaiseTileDiscarded(Index, tile);
            }
        }

        public void AddCalledMeld(CalledMeld meld)
        {
            CalledMeldsExt.Add(meld);
            CalledMelds.Add(meld.ToMeld());
            GameEvents.RaiseHandChanged(Index);
        }

        public bool UpgradePonToKan(TileKind kind, TileData addedTile)
        {
            for (int i = 0; i < CalledMeldsExt.Count; i++)
            {
                var m = CalledMeldsExt[i];
                if (m.CallType == CallType.Pon && m.BaseKind == kind)
                {
                    var newTiles = new List<TileData>(m.Tiles) { addedTile };
                    var upgraded = new CalledMeld(
                        MeldType.Quad, kind, newTiles,
                        addedTile, m.CalledFromPlayerIndex,
                        CallType.Shouminkan);
                    CalledMeldsExt[i] = upgraded;
                    CalledMelds[i] = upgraded.ToMeld();
                    GameEvents.RaiseHandChanged(Index);
                    return true;
                }
            }
            return false;
        }

        public void DeclareRiichi(int currentTurn, List<TileKind> waitsAfterDiscard)
        {
            IsRiichi = true;
            RiichiTurn = currentTurn;
            IsIppatsuChance = true;
            RiichiDiscardIndex = DiscardPile.Count - 1;
            RiichiWaits = new List<TileKind>(waitsAfterDiscard);
        }

        public void CancelIppatsu() => IsIppatsuChance = false;

        public bool HasNonAnkanCalled()
        {
            foreach (var m in CalledMeldsExt)
                if (m.CallType != CallType.Ankan) return true;
            return false;
        }

        public TileCounter ToCounter() => new TileCounter(HandTiles);

        private List<TileData> BuildAllTiles()
        {
            var all = new List<TileData>(HandTiles);
            foreach (var meld in CalledMeldsExt)
            {
                foreach (var t in meld.Tiles)
                    all.Add(t);
            }
            return all;
        }

        /// <summary>
        /// 화료 판정. 디버그 로그 추가.
        /// </summary>
        public bool IsAgari()
        {
            bool result;
            if (CalledMeldsExt.Count == 0)
            {
                result = HandAnalyzer.IsAgari(ToCounter());
                if (DebugAgariForUser && Index == 0)
                {
                    Debug.Log($"[IsAgari P0] 손패 {HandTiles.Count}장, 후로 없음, 결과: {result}, 손패: {HandTilesString()}");
                }
                return result;
            }

            var allTiles = BuildAllTiles();
            if (DebugAgariForUser && Index == 0)
            {
                Debug.Log($"[IsAgari P0] 손패 {HandTiles.Count}장 + 후로 {CalledMeldsExt.Count}개 = 총 {allTiles.Count}장. 14장 검사");
            }

            if (allTiles.Count != 14)
            {
                if (DebugAgariForUser && Index == 0)
                    Debug.Log($"[IsAgari P0] 총 장수 != 14 → false");
                return false;
            }

            result = HandAnalyzer.IsAgari(new TileCounter(allTiles));
            if (DebugAgariForUser && Index == 0)
                Debug.Log($"[IsAgari P0] HandAnalyzer.IsAgari 결과: {result}");
            return result;
        }

        private string HandTilesString()
        {
            var parts = new List<string>();
            foreach (var t in HandTiles) parts.Add(t.ToString());
            return string.Join(" ", parts);
        }

        public bool IsValidAgariTile(TileKind tile)
        {
            if (!IsRiichi) return true;
            return RiichiWaits.Contains(tile);
        }

        public bool IsTenpai() => TenpaiAnalyzer.IsTenpai(new TileCounter(BuildAllTiles()));

        public List<TileKind> GetWaitingTiles() =>
            TenpaiAnalyzer.GetWaitingTiles(new TileCounter(BuildAllTiles()));

        public int CountAkaDora()
        {
            int count = HandTiles.Count(t => t.IsRedFive);
            foreach (var m in CalledMeldsExt) count += m.CountAkaDora();
            return count;
        }

        public void Reset()
        {
            HandTiles.Clear();
            DiscardPile.Clear();
            CalledMelds.Clear();
            CalledMeldsExt.Clear();
            IsRiichi = false;
            RiichiTurn = -1;
            RiichiDiscardIndex = -1;
            IsIppatsuChance = false;
            RiichiWaits.Clear();
        }

        public override string ToString() => $"P{Index}({Seat}, {Type})";
    }
}
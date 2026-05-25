using System.Collections.Generic;
using System.Linq;

namespace MahjongAtelier.Core
{
    /// <summary>
    /// 플레이어 상태.
    /// 손패, 버린패, 후로(추후), 리치 상태 등을 관리합니다.
    /// </summary>
    public sealed class MahjongPlayer
    {
        public int Index { get; }

        /// <summary>손패 (정렬된 상태로 유지하지 않음 — UI 측에서 자유 정렬 가능).</summary>
        public List<TileData> HandTiles { get; } = new List<TileData>();

        /// <summary>버린 패 (河). 순서대로.</summary>
        public List<TileData> DiscardPile { get; } = new List<TileData>();

        /// <summary>리치 선언 여부.</summary>
        public bool IsRiichi { get; private set; }

        /// <summary>리치 선언 시점의 턴 수 (일발 판정용).</summary>
        public int RiichiTurn { get; private set; } = -1;

        public MahjongPlayer(int index)
        {
            Index = index;
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

        public void DeclareRiichi(int currentTurn)
        {
            IsRiichi = true;
            RiichiTurn = currentTurn;
        }

        /// <summary>손패의 종류별 카운터를 만들어 반환. 분석에 사용.</summary>
        public TileCounter ToCounter() => new TileCounter(HandTiles);

        /// <summary>화료 여부 (현재 손패 기준).</summary>
        public bool IsAgari() => HandAnalyzer.IsAgari(ToCounter());

        /// <summary>텐파이 여부 (13장일 때).</summary>
        public bool IsTenpai() => TenpaiAnalyzer.IsTenpai(ToCounter());

        /// <summary>대기패 목록.</summary>
        public List<TileKind> GetWaitingTiles() =>
            TenpaiAnalyzer.GetWaitingTiles(ToCounter());

        /// <summary>아카도라 보유 개수.</summary>
        public int CountAkaDora() => HandTiles.Count(t => t.IsRedFive);

        public void Reset()
        {
            HandTiles.Clear();
            DiscardPile.Clear();
            IsRiichi = false;
            RiichiTurn = -1;
        }
    }
}

using System.Collections.Generic;

namespace MahjongAtelier.Core
{
    /// <summary>
    /// 리치 선언 가능성 판정 + 리치 후 버림 가능 패 분석.
    /// 
    /// 리치 조건:
    ///   1) 멘젠 (후로 없음, 안깡은 OK)
    ///   2) 14장 손패 + 텐파이 (어떤 패를 버리면 텐파이)
    ///   3) 점수 1000점 이상
    ///   4) 패산 남은 수 ≥ 4 (리치 후 최소 1순은 더 진행 가능)
    ///   5) 이미 리치 상태 아님
    /// </summary>
    public static class RiichiAnalyzer
    {
        public const int RiichiDepositAmount = 1000;
        public const int MinWallRemaining = 4;

        /// <summary>
        /// 리치 선언이 가능한지 + 어떤 패를 버려야 텐파이가 유지되는지 분석.
        /// </summary>
        public static RiichiAnalysis Analyze(
            MahjongPlayer player, int playerPoints, int wallRemaining)
        {
            var result = new RiichiAnalysis();

            // 조건들 검사
            if (player.IsRiichi)
            {
                result.Reason = "이미 리치 선언함";
                return result;
            }

            // 멘젠 체크 (현재 후로 미구현이라 항상 true지만 미래 대비)
            if (player.HasNonAnkanCalled())
            {
                result.Reason = "후로하면 리치 불가";
                return result;
            }

            if (playerPoints < RiichiDepositAmount)
            {
                result.Reason = "1000점 미만 (공탁금 부족)";
                return result;
            }

            if (wallRemaining < MinWallRemaining)
            {
                result.Reason = "패산 부족 (최소 4장 필요)";
                return result;
            }

            if (player.HandTiles.Count != 14)
            {
                result.Reason = "쯔모한 직후에만 리치 가능";
                return result;
            }

            // 텐파이 가능한 버림 후보 검출
            var counter = player.ToCounter();
            var tenpaiDiscards = TenpaiAnalyzer.GetTenpaiDiscards(counter);

            if (tenpaiDiscards.Count == 0)
            {
                result.Reason = "텐파이가 아님";
                return result;
            }

            // 손패의 실제 TileData 중에서 텐파이 가능한 패들의 인스턴스 매핑
            result.CanDeclareRiichi = true;
            result.AvailableDiscards = MapKindsToTileData(player.HandTiles, tenpaiDiscards);
            result.Reason = "리치 가능";
            return result;
        }

        /// <summary>
        /// TileKind 후보를 실제 TileData 인스턴스로 매핑.
        /// 같은 종류가 2장 있어서 후보면, 둘 다 후보로 포함.
        /// </summary>
        private static List<TileData> MapKindsToTileData(
            List<TileData> hand,
            List<(TileKind discard, List<TileKind> waits)> tenpaiDiscards)
        {
            var allowedKinds = new HashSet<TileKind>();
            foreach (var (discard, _) in tenpaiDiscards)
                allowedKinds.Add(discard);

            var result = new List<TileData>();
            foreach (var tile in hand)
            {
                if (allowedKinds.Contains(tile.Kind))
                    result.Add(tile);
            }
            return result;
        }
    }

    /// <summary>리치 분석 결과.</summary>
    public sealed class RiichiAnalysis
    {
        public bool CanDeclareRiichi { get; set; }
        public string Reason { get; set; }

        /// <summary>리치 선언 시 버릴 수 있는 패 (텐파이 유지). 비어있으면 리치 불가.</summary>
        public List<TileData> AvailableDiscards { get; set; } = new List<TileData>();

        /// <summary>특정 패가 리치 선언 시 버려도 되는 패인지.</summary>
        public bool IsValidDiscard(TileData tile)
        {
            foreach (var t in AvailableDiscards)
                if (t == tile) return true;
            return false;
        }
    }
}

using System.Collections.Generic;

namespace MahjongAtelier.Core
{
    /// <summary>
    /// 후리텐(振聴) 판정.
    /// 
    /// 후리텐 = 자기가 텐파이 상태인데 자기 버림패에 자기 대기패가 있는 상태.
    /// 후리텐 동안 론 불가 (쯔모는 가능).
    /// 
    /// 종류:
    ///   - 일반 후리텐: 손에 든 패만 보고 판정
    ///   - 리치 후리텐: 리치 시점의 대기패가 자기 버림패에 있는 경우 영구 후리텐
    ///   - 임시 후리텐: 텐파이 시 누가 대기패 버렸는데 안 잡음 → 자기 다음 쯔모까지 후리텐
    /// 
    /// 본 구현은 일반 후리텐 + 리치 후리텐만. 임시 후리텐은 단순화.
    /// </summary>
    public static class FuritenAnalyzer
    {
        /// <summary>
        /// 플레이어가 후리텐 상태인지 판정.
        /// </summary>
        /// <param name="player">검사할 플레이어</param>
        /// <param name="currentWaits">현재 대기패 목록 (텐파이일 때만 의미 있음, 비텐파이면 빈 리스트)</param>
        public static bool IsFuriten(MahjongPlayer player, List<TileKind> currentWaits)
        {
            if (currentWaits == null || currentWaits.Count == 0) return false;

            // 리치 상태: 리치 시점의 대기패가 기준
            // (리치 후엔 손패가 바뀌지 않으므로 currentWaits = RiichiWaits 와 동일하지만 명시적으로)
            var checkWaits = player.IsRiichi && player.RiichiWaits != null && player.RiichiWaits.Count > 0
                ? player.RiichiWaits
                : currentWaits;

            // 자기 버림패에 대기패 종류가 하나라도 있으면 후리텐
            var discardKinds = new HashSet<TileKind>();
            foreach (var t in player.DiscardPile)
                discardKinds.Add(t.Kind);

            foreach (var wait in checkWaits)
            {
                if (discardKinds.Contains(wait)) return true;
            }
            return false;
        }
    }
}

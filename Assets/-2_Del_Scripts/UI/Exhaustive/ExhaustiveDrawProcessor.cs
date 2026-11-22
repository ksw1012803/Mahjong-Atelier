using System.Collections.Generic;

namespace MahjongAtelier.Core
{
    /// <summary>
    /// 유국 처리 — 텐파이/노텐 판정 + 노텐벌 정산 + 친 연장 판단.
    /// 
    /// 일본 표준 노텐벌(罰符): 노텐 → 텐파이 합계 3000점 이동.
    /// - 텐파이 1명: 1명이 3000점 받음, 노텐 3명이 각 1000점 지불
    /// - 텐파이 2명: 각 1500점 받음, 노텐 2명이 각 1500점 지불
    /// - 텐파이 3명: 각 1000점 받음, 노텐 1명이 3000점 지불
    /// - 0명 또는 4명: 이동 없음
    /// 
    /// 친 연장: 동가(東家)가 텐파이면 다음 판도 같은 친.
    /// </summary>
    public static class ExhaustiveDrawProcessor
    {
        public const int NotenPenaltyTotal = 3000;

        /// <summary>
        /// 유국 처리. 각 플레이어의 텐파이/노텐 판정 후 점수 이동 계산.
        /// </summary>
        public static ExhaustiveDrawResult Process(
            IReadOnlyList<MahjongPlayer> players,
            int dealerIndex)
        {
            var result = new ExhaustiveDrawResult();

            // 1) 각 플레이어 텐파이 판정
            var tenpaiIndices = new List<int>();
            var notenIndices = new List<int>();

            for (int i = 0; i < players.Count; i++)
            {
                var p = players[i];

                // 리치한 플레이어는 자동 텐파이
                bool isTenpai = p.IsRiichi || p.IsTenpai();

                var status = new ExhaustiveDrawResult.PlayerStatus
                {
                    PlayerIndex = i,
                    Seat = p.Seat,
                    IsTenpai = isTenpai,
                    Waits = isTenpai ? p.GetWaitingTiles() : new List<TileKind>(),
                    PointChange = 0
                };
                result.Statuses.Add(status);

                if (isTenpai) tenpaiIndices.Add(i);
                else notenIndices.Add(i);
            }

            // 2) 노텐벌 계산
            ApplyNotenPenalty(result, tenpaiIndices, notenIndices, players.Count);

            // 3) 친 연장 판정 — 친이 텐파이면 연장
            if (dealerIndex >= 0 && dealerIndex < result.Statuses.Count)
            {
                result.IsDealerRepeat = result.Statuses[dealerIndex].IsTenpai;
            }

            return result;
        }

        private static void ApplyNotenPenalty(
            ExhaustiveDrawResult result,
            List<int> tenpaiIndices,
            List<int> notenIndices,
            int totalPlayers)
        {
            int tenpaiCount = tenpaiIndices.Count;
            int notenCount = notenIndices.Count;

            // 모두 텐파이 또는 모두 노텐이면 이동 없음
            if (tenpaiCount == 0 || notenCount == 0)
            {
                result.TotalNotenPenalty = 0;
                return;
            }

            // 텐파이 1명당 받는 점수, 노텐 1명당 내는 점수
            int gainPerTenpai = NotenPenaltyTotal / tenpaiCount;
            int payPerNoten = NotenPenaltyTotal / notenCount;

            foreach (int idx in tenpaiIndices)
                result.Statuses[idx].PointChange = gainPerTenpai;

            foreach (int idx in notenIndices)
                result.Statuses[idx].PointChange = -payPerNoten;

            result.TotalNotenPenalty = NotenPenaltyTotal;
        }
    }
}

using System.Collections.Generic;

namespace MahjongAtelier.Core
{
    /// <summary>
    /// 유국(流局) 결과 데이터. UI 표시 및 점수 변동 기록용.
    /// 
    /// 일반 유국 = 패산 소진까지 화료자가 안 나온 경우.
    /// 텐파이/노텐을 판정하고 노텐벌 정산.
    /// </summary>
    public sealed class ExhaustiveDrawResult
    {
        /// <summary>각 플레이어의 텐파이 정보.</summary>
        public List<PlayerStatus> Statuses { get; } = new List<PlayerStatus>();

        /// <summary>친(東家)이 연장(連荘)했는지. true면 다음 판에서도 같은 친.</summary>
        public bool IsDealerRepeat { get; set; }

        /// <summary>노텐벌 총액 (이동된 점수). 디버그/UI용.</summary>
        public int TotalNotenPenalty { get; set; }

        public sealed class PlayerStatus
        {
            public int PlayerIndex;
            public SeatWind Seat;
            public bool IsTenpai;
            public List<TileKind> Waits;       // 텐파이 시 대기패
            public int PointChange;             // 노텐벌로 인한 점수 변동 (+가 받음, -가 지불)

            public override string ToString()
            {
                string status = IsTenpai ? "텐파이" : "노텐";
                string change = PointChange == 0 ? "" : $" ({PointChange:+#;-#;0})";
                return $"P{PlayerIndex}({Seat}): {status}{change}";
            }
        }
    }
}

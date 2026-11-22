namespace MahjongAtelier.UI
{
    /// <summary>
    /// 화면상의 자리 위치. 사용자 기준 상대 위치.
    /// 
    /// Self: 화면 아래 (사용자 자신)
    /// ShimoCha (下家): 화면 오른쪽 (사용자 다음 차례 — 시계 반대 방향)
    /// Toimen (對面): 화면 위 (사용자 맞은편)
    /// KamiCha (上家): 화면 왼쪽 (사용자 이전 차례)
    /// 
    /// 일본 마작에서 턴은 시계 반대 방향 진행:
    ///   East(자기) → South(시모, 우) → West(토이멘, 위) → North(카미, 좌) → East
    /// </summary>
    public enum SeatPosition
    {
        Self = 0,       // 화면 아래 (사용자)
        ShimoCha = 1,   // 화면 오른쪽 (下家)
        Toimen = 2,     // 화면 위 (對面)
        KamiCha = 3     // 화면 왼쪽 (上家)
    }

    public static class SeatPositionHelper
    {
        /// <summary>
        /// 사용자(P0) 기준 다른 플레이어의 화면 위치.
        /// 4인용에서 사용자는 항상 SeatPosition.Self.
        /// </summary>
        public static SeatPosition GetPositionFor(int playerIndex, int userIndex, int totalPlayers)
        {
            if (totalPlayers <= 1) return SeatPosition.Self;
            int rel = (playerIndex - userIndex + totalPlayers) % totalPlayers;
            return (SeatPosition)rel;
        }

        /// <summary>해당 위치의 타일이 화면에서 회전해야 하는 각도(도).</summary>
        public static float GetTileRotation(SeatPosition pos)
        {
            switch (pos)
            {
                case SeatPosition.Self: return 0f;
                case SeatPosition.ShimoCha: return -90f;   // 오른쪽: 시계방향 90도
                case SeatPosition.Toimen: return 180f;
                case SeatPosition.KamiCha: return 90f;     // 왼쪽: 반시계방향 90도
                default: return 0f;
            }
        }
    }
}

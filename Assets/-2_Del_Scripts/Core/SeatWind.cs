namespace MahjongAtelier.Core
{
    /// <summary>
    /// 좌석풍. 자(子)/친(親) 결정 및 자풍 부여에 사용.
    /// 동(東)이 친(親, 처음 차례).
    /// 
    /// 4인용에서 한 국이 끝나면 친이 다음 자리로 이동 (연장 또는 새 친):
    ///   동가 → 남가 → 서가 → 북가
    /// 
    /// 본 구현은 단순화: 매 국마다 친이 다음 자리로 이동 (연장 미구현).
    /// </summary>
    public enum SeatWind
    {
        East = 0,
        South = 1,
        West = 2,
        North = 3
    }

    public static class SeatWindExtensions
    {
        /// <summary>좌석풍 → 풍패 TileKind 변환.</summary>
        public static TileKind ToTileKind(this SeatWind seat)
        {
            return seat switch
            {
                SeatWind.East => TileKind.East,
                SeatWind.South => TileKind.South,
                SeatWind.West => TileKind.West,
                SeatWind.North => TileKind.North,
                _ => TileKind.East
            };
        }

        /// <summary>친(親) 여부 = 동가인지.</summary>
        public static bool IsDealer(this SeatWind seat) => seat == SeatWind.East;
    }
}

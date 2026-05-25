namespace MahjongAtelier.Core
{
    /// <summary>
    /// 마작 패의 종류 (수트).
    /// enum 값은 정렬 순서를 결정합니다. 표준 일본 마작 순서: 만 → 통 → 삭 → 풍 → 삼원.
    /// 자패(풍/삼원) 내부 순서는 number 필드로 처리:
    ///   - Wind:   1=東, 2=南, 3=西, 4=北
    ///   - Dragon: 1=白, 2=發, 3=中
    /// </summary>
    public enum TileSuit
    {
        Man = 0,    // 만수패 (萬子, 1m~9m)
        Pin = 1,    // 통수패 (筒子, 1p~9p)  ※ 기존 Tong을 Pin으로 변경 (국제 표기 통일)
        Sou = 2,    // 삭수패 (索子, 1s~9s)
        Wind = 3,   // 풍패   (風牌, 東南西北)
        Dragon = 4  // 삼원패 (三元牌, 白發中)
    }

    public static class TileSuitExtensions
    {
        /// <summary>수트가 수패(數牌)인지 여부. 치(順子) 구성 가능 여부 판단에 사용.</summary>
        public static bool IsNumber(this TileSuit suit) =>
            suit == TileSuit.Man || suit == TileSuit.Pin || suit == TileSuit.Sou;

        /// <summary>수트가 자패(字牌)인지 여부.</summary>
        public static bool IsHonor(this TileSuit suit) =>
            suit == TileSuit.Wind || suit == TileSuit.Dragon;
    }
}

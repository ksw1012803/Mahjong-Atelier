namespace MahjongAtelier.Core
{
    /// <summary>
    /// 탄야오 (断幺九): 모든 패가 2~8의 수패. 자패, 1, 9 일체 사용 안 함. 1판.
    /// 후로해도 성립 (구이단/喰い断 허용 룰 기준).
    /// </summary>
    public sealed class Tanyao : IYaku
    {
        public string Name => "断幺九";
        public string NameKr => "탄야오";
        public bool IsYakuman => false;

        public bool IsApplicable(YakuContext ctx, HandDecomposition decomp)
        {
            // 칠대자도 가능, 표준형도 가능. 단순히 손패 카운터에서 요구패가 없는지 확인.
            var hand = ctx.Hand;

            // 1, 9 수패 + 모든 자패 = 0이어야 함
            // 인덱스: 0(1m), 8(9m), 9(1p), 17(9p), 18(1s), 26(9s), 27~33(자패)
            if (hand[0] > 0 || hand[8] > 0) return false;
            if (hand[9] > 0 || hand[17] > 0) return false;
            if (hand[18] > 0 || hand[26] > 0) return false;
            for (int i = 27; i < 34; i++)
                if (hand[i] > 0) return false;

            return true;
        }

        public int GetHan(YakuContext ctx, HandDecomposition decomp) => 1;
        public int GetHanCalled(YakuContext ctx, HandDecomposition decomp) => 1;
    }
}

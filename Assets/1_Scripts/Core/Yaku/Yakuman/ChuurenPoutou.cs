namespace MahjongAtelier.Core
{
    /// <summary>
    /// 구련보등 (九蓮宝燈): 멘젠 한 수트로 1112345678999 + 그 수트의 임의 1장.
    /// 역만. 순정구련(처음부터 1112345678999 형태에 마지막 1장 화료)은 더블 역만 룰도 있지만 본 구현은 단순.
    /// </summary>
    public sealed class ChuurenPoutou : IYaku
    {
        public string Name => "九蓮宝燈";
        public string NameKr => "구련보등";
        public bool IsYakuman => true;

        public bool IsApplicable(YakuContext ctx, HandDecomposition decomp)
        {
            if (!ctx.IsMenzen) return false;
            if (decomp.Form != AgariForm.Standard) return false;

            // 한 수트만 사용?
            if (!DecompositionHelpers.IsSingleNumberSuit(ctx, decomp, out TileSuit suit))
                return false;

            // 손패 카운터에서 그 수트의 분포가 1112345678999 + 1장이어야 함
            // 즉 [3,1,1,1,1,1,1,1,3] + 임의 위치에 +1
            int start = TileCounter.SuitStartIndex(suit);

            // 수트 외에 다른 패가 있으면 실패
            for (int i = 0; i < TileCounter.Size; i++)
            {
                if (i >= start && i < start + 9) continue;
                if (ctx.Hand[i] > 0) return false;
            }

            // 9개 슬롯의 카운트
            int[] cnt = new int[9];
            for (int i = 0; i < 9; i++) cnt[i] = ctx.Hand[start + i];

            // 기본 형태 [3,1,1,1,1,1,1,1,3]에서 한 위치만 +1 되어야 함
            int[] basePat = { 3, 1, 1, 1, 1, 1, 1, 1, 3 };
            int diffPlusCount = 0;
            for (int i = 0; i < 9; i++)
            {
                int diff = cnt[i] - basePat[i];
                if (diff < 0) return false;
                if (diff > 1) return false;
                if (diff == 1) diffPlusCount++;
            }
            return diffPlusCount == 1;
        }

        public int GetHan(YakuContext ctx, HandDecomposition decomp) => 1;
        public int GetHanCalled(YakuContext ctx, HandDecomposition decomp) => 0;
    }
}

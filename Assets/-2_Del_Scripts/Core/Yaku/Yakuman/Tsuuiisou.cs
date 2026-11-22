namespace MahjongAtelier.Core
{
    /// <summary>
    /// 자일색 (字一色): 모든 패가 자패. 역만.
    /// </summary>
    public sealed class Tsuuiisou : IYaku
    {
        public string Name => "字一色";
        public string NameKr => "자일색";
        public bool IsYakuman => true;

        public bool IsApplicable(YakuContext ctx, HandDecomposition decomp)
        {
            // 칠대자 + 자일색도 가능
            if (decomp.Form == AgariForm.Chiitoitsu)
            {
                foreach (var p in decomp.PairsForChiitoitsu)
                    if (!p.BaseKind.IsHonor) return false;
                return true;
            }
            if (decomp.Form != AgariForm.Standard) return false;
            foreach (var m in DecompositionHelpers.AllMeldsWithPair(ctx, decomp))
            {
                if (!m.BaseKind.IsHonor) return false;
            }
            return true;
        }

        public int GetHan(YakuContext ctx, HandDecomposition decomp) => 1;
        public int GetHanCalled(YakuContext ctx, HandDecomposition decomp) => 1;
    }

    /// <summary>
    /// 청노두 (清老頭): 모든 패가 노두패(1·9 수패). 역만.
    /// 슌츠 불가능 → 필연적으로 코츠/머리만의 구성.
    /// </summary>
    public sealed class Chinroutou : IYaku
    {
        public string Name => "清老頭";
        public string NameKr => "청노두";
        public bool IsYakuman => true;

        public bool IsApplicable(YakuContext ctx, HandDecomposition decomp)
        {
            if (decomp.Form != AgariForm.Standard) return false;
            foreach (var m in DecompositionHelpers.AllMeldsWithPair(ctx, decomp))
            {
                if (!m.BaseKind.IsTerminal) return false;
            }
            return true;
        }

        public int GetHan(YakuContext ctx, HandDecomposition decomp) => 1;
        public int GetHanCalled(YakuContext ctx, HandDecomposition decomp) => 1;
    }

    /// <summary>
    /// 녹일색 (緑一色): 23468s + 발(發)만으로 구성. 역만.
    /// </summary>
    public sealed class Ryuuiisou : IYaku
    {
        public string Name => "緑一色";
        public string NameKr => "녹일색";
        public bool IsYakuman => true;

        // 허용 인덱스: 19(2s), 20(3s), 21(4s), 23(6s), 25(8s), 32(發)
        private static readonly System.Collections.Generic.HashSet<int> AllowedIndices
            = new System.Collections.Generic.HashSet<int> { 19, 20, 21, 23, 25, 32 };

        public bool IsApplicable(YakuContext ctx, HandDecomposition decomp)
        {
            // 손패 카운터에서 허용 인덱스 외에 패가 있으면 실패
            for (int i = 0; i < TileCounter.Size; i++)
            {
                if (ctx.Hand[i] > 0 && !AllowedIndices.Contains(i))
                    return false;
            }
            return true;
        }

        public int GetHan(YakuContext ctx, HandDecomposition decomp) => 1;
        public int GetHanCalled(YakuContext ctx, HandDecomposition decomp) => 1;
    }
}

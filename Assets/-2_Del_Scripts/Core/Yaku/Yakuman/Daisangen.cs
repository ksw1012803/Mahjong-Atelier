namespace MahjongAtelier.Core
{
    /// <summary>
    /// 대삼원 (大三元): 백/발/중 모두 코츠. 역만.
    /// </summary>
    public sealed class Daisangen : IYaku
    {
        public string Name => "大三元";
        public string NameKr => "대삼원";
        public bool IsYakuman => true;

        public bool IsApplicable(YakuContext ctx, HandDecomposition decomp)
        {
            if (decomp.Form != AgariForm.Standard) return false;

            int dragonTripletCount = 0;
            foreach (var m in DecompositionHelpers.AllMelds(ctx, decomp))
            {
                if (m.IsTripletOrQuad && m.BaseKind.Suit == TileSuit.Dragon)
                    dragonTripletCount++;
            }
            return dragonTripletCount == 3;
        }

        public int GetHan(YakuContext ctx, HandDecomposition decomp) => 1;
        public int GetHanCalled(YakuContext ctx, HandDecomposition decomp) => 1;
    }

    /// <summary>
    /// 소사희 (小四喜): 풍패 코츠 3개 + 풍패 머리. 역만.
    /// </summary>
    public sealed class Shousuushi : IYaku
    {
        public string Name => "小四喜";
        public string NameKr => "소사희";
        public bool IsYakuman => true;

        public bool IsApplicable(YakuContext ctx, HandDecomposition decomp)
        {
            if (decomp.Form != AgariForm.Standard) return false;
            if (decomp.Pair.BaseKind.Suit != TileSuit.Wind) return false;

            int windTripletCount = 0;
            foreach (var m in DecompositionHelpers.AllMelds(ctx, decomp))
            {
                if (m.IsTripletOrQuad && m.BaseKind.Suit == TileSuit.Wind)
                    windTripletCount++;
            }
            return windTripletCount == 3;
        }

        public int GetHan(YakuContext ctx, HandDecomposition decomp) => 1;
        public int GetHanCalled(YakuContext ctx, HandDecomposition decomp) => 1;
    }

    /// <summary>
    /// 대사희 (大四喜): 풍패 코츠 4개. 더블 역만.
    /// </summary>
    public sealed class Daisuushi : IYaku
    {
        public string Name => "大四喜";
        public string NameKr => "대사희";
        public bool IsYakuman => true;

        public bool IsApplicable(YakuContext ctx, HandDecomposition decomp)
        {
            if (decomp.Form != AgariForm.Standard) return false;

            int windTripletCount = 0;
            foreach (var m in DecompositionHelpers.AllMelds(ctx, decomp))
            {
                if (m.IsTripletOrQuad && m.BaseKind.Suit == TileSuit.Wind)
                    windTripletCount++;
            }
            return windTripletCount == 4;
        }

        public int GetHan(YakuContext ctx, HandDecomposition decomp) => 2; // 더블 역만
        public int GetHanCalled(YakuContext ctx, HandDecomposition decomp) => 2;
    }
}

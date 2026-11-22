namespace MahjongAtelier.Core
{
    /// <summary>
    /// 사암각 (四暗刻): 안각(안깡 포함) 4개. 역만.
    /// 화료 시 탕키 대기였으면 더블 역만 룰도 있지만 본 구현은 단순 역만.
    /// </summary>
    public sealed class Suuankou : IYaku
    {
        public string Name => "四暗刻";
        public string NameKr => "사암각";
        public bool IsYakuman => true;

        public bool IsApplicable(YakuContext ctx, HandDecomposition decomp)
        {
            // 후로(안깡 제외)가 있으면 안각 4개 불가
            foreach (var m in ctx.CalledMelds)
            {
                if (!(m.IsQuad && m.IsConcealed)) return false;
            }

            return DecompositionHelpers.CountConcealedTriplets(ctx, decomp) == 4;
        }

        public int GetHan(YakuContext ctx, HandDecomposition decomp) => 1;
        public int GetHanCalled(YakuContext ctx, HandDecomposition decomp) => 1;
    }

    /// <summary>
    /// 사강자 (四槓子): 깡 4개. 역만.
    /// </summary>
    public sealed class Suukantsu : IYaku
    {
        public string Name => "四槓子";
        public string NameKr => "사강자";
        public bool IsYakuman => true;

        public bool IsApplicable(YakuContext ctx, HandDecomposition decomp)
        {
            return DecompositionHelpers.CountQuads(ctx, decomp) == 4;
        }

        public int GetHan(YakuContext ctx, HandDecomposition decomp) => 1;
        public int GetHanCalled(YakuContext ctx, HandDecomposition decomp) => 1;
    }
}

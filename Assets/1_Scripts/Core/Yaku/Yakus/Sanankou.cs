namespace MahjongAtelier.Core
{
    /// <summary>
    /// 삼암각 (三暗刻): 안각(안깡 포함) 3개. 2판.
    /// 후로해도 성립 (다른 1개는 후로일 수 있음). 멘젠/후로 동일 점수.
    /// </summary>
    public sealed class Sanankou : IYaku
    {
        public string Name => "三暗刻";
        public string NameKr => "삼암각";
        public bool IsYakuman => false;

        public bool IsApplicable(YakuContext ctx, HandDecomposition decomp)
        {
            return DecompositionHelpers.CountConcealedTriplets(ctx, decomp) == 3;
        }

        public int GetHan(YakuContext ctx, HandDecomposition decomp) => 2;
        public int GetHanCalled(YakuContext ctx, HandDecomposition decomp) => 2;
    }
}

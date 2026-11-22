namespace MahjongAtelier.Core
{
    /// <summary>
    /// 멘젠 쯔모 (門前清自摸和): 후로 없이 쯔모로 화료. 1판.
    /// </summary>
    public sealed class MenzenTsumo : IYaku
    {
        public string Name => "門前清自摸和";
        public string NameKr => "멘젠 쯔모";
        public bool IsYakuman => false;

        public bool IsApplicable(YakuContext ctx, HandDecomposition decomp)
        {
            return ctx.IsTsumo && ctx.IsMenzen;
        }

        public int GetHan(YakuContext ctx, HandDecomposition decomp) => 1;
        public int GetHanCalled(YakuContext ctx, HandDecomposition decomp) => 0;
    }
}

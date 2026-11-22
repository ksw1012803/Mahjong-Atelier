namespace MahjongAtelier.Core
{
    /// <summary>
    /// 칠대자 (七対子): 7쌍. 2판. 멘젠 한정.
    /// 일본 표준 룰에선 25부 고정.
    /// </summary>
    public sealed class Chiitoitsu : IYaku
    {
        public string Name => "七対子";
        public string NameKr => "칠대자";
        public bool IsYakuman => false;

        public bool IsApplicable(YakuContext ctx, HandDecomposition decomp)
        {
            if (!ctx.IsMenzen) return false;
            return decomp.Form == AgariForm.Chiitoitsu;
        }

        public int GetHan(YakuContext ctx, HandDecomposition decomp) => 2;
        public int GetHanCalled(YakuContext ctx, HandDecomposition decomp) => 0;
    }
}

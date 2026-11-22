namespace MahjongAtelier.Core
{
    /// <summary>
    /// 이배구 (二盃口): 일배구 두 조. 멘젠 한정. 3판.
    /// 칠대자와 동시 성립 불가 (배타).
    /// </summary>
    public sealed class Ryanpeikou : IYaku
    {
        public string Name => "二盃口";
        public string NameKr => "이배구";
        public bool IsYakuman => false;

        public bool IsApplicable(YakuContext ctx, HandDecomposition decomp)
        {
            if (!ctx.IsMenzen) return false;
            if (decomp.Form != AgariForm.Standard) return false;

            // 같은 슌츠가 정확히 2쌍 있어야 함
            int sameSequencePairs = Iipeikou.CountSameSequencePairs(decomp);
            return sameSequencePairs == 2;
        }

        public int GetHan(YakuContext ctx, HandDecomposition decomp) => 3;
        public int GetHanCalled(YakuContext ctx, HandDecomposition decomp) => 0;
    }
}

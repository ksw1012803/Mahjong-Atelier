namespace MahjongAtelier.Core
{
    /// <summary>
    /// 리치(立直): 멘젠 텐파이에서 1000점 공탁 후 선언. 1판.
    /// 후로하면 성립 불가. 더블 리치는 별도 역.
    /// </summary>
    public sealed class Riichi : IYaku
    {
        public string Name => "立直";
        public string NameKr => "리치";
        public bool IsYakuman => false;

        public bool IsApplicable(YakuContext ctx, HandDecomposition decomp)
        {
            // 더블 리치와 동시 성립 안 되게: 더블 리치면 그쪽이 우선
            return ctx.IsRiichi && !ctx.IsDoubleRiichi && ctx.IsMenzen;
        }

        public int GetHan(YakuContext ctx, HandDecomposition decomp) => 1;
        public int GetHanCalled(YakuContext ctx, HandDecomposition decomp) => 0;
    }

    /// <summary>
    /// 더블 리치(両立直/W立直): 첫 순에 리치. 2판.
    /// </summary>
    public sealed class DoubleRiichi : IYaku
    {
        public string Name => "両立直";
        public string NameKr => "더블 리치";
        public bool IsYakuman => false;

        public bool IsApplicable(YakuContext ctx, HandDecomposition decomp)
        {
            return ctx.IsDoubleRiichi && ctx.IsMenzen;
        }

        public int GetHan(YakuContext ctx, HandDecomposition decomp) => 2;
        public int GetHanCalled(YakuContext ctx, HandDecomposition decomp) => 0;
    }

    /// <summary>
    /// 일발(一発): 리치 선언 후 1순 이내 화료 (그동안 후로 발생 없어야).
    /// </summary>
    public sealed class Ippatsu : IYaku
    {
        public string Name => "一発";
        public string NameKr => "일발";
        public bool IsYakuman => false;

        public bool IsApplicable(YakuContext ctx, HandDecomposition decomp)
        {
            return ctx.IsIppatsu && (ctx.IsRiichi || ctx.IsDoubleRiichi) && ctx.IsMenzen;
        }

        public int GetHan(YakuContext ctx, HandDecomposition decomp) => 1;
        public int GetHanCalled(YakuContext ctx, HandDecomposition decomp) => 0;
    }
}

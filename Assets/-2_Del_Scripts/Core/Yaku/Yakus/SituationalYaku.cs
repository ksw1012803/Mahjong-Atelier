namespace MahjongAtelier.Core
{
    /// <summary>하이테이 라오웨 (海底摸月): 마지막 패로 쯔모 화료. 1판.</summary>
    public sealed class Haitei : IYaku
    {
        public string Name => "海底摸月";
        public string NameKr => "해저로월";
        public bool IsYakuman => false;

        public bool IsApplicable(YakuContext ctx, HandDecomposition decomp)
            => ctx.IsHaitei && ctx.IsTsumo;

        public int GetHan(YakuContext ctx, HandDecomposition decomp) => 1;
        public int GetHanCalled(YakuContext ctx, HandDecomposition decomp) => 1;
    }

    /// <summary>호우테이 라오위 (河底撈魚): 마지막 버림패로 론. 1판.</summary>
    public sealed class Houtei : IYaku
    {
        public string Name => "河底撈魚";
        public string NameKr => "하저로어";
        public bool IsYakuman => false;

        public bool IsApplicable(YakuContext ctx, HandDecomposition decomp)
            => ctx.IsHoutei && !ctx.IsTsumo;

        public int GetHan(YakuContext ctx, HandDecomposition decomp) => 1;
        public int GetHanCalled(YakuContext ctx, HandDecomposition decomp) => 1;
    }

    /// <summary>린샨카이호 (嶺上開花): 깡 후 보충패로 화료. 1판.</summary>
    public sealed class Rinshan : IYaku
    {
        public string Name => "嶺上開花";
        public string NameKr => "영상개화";
        public bool IsYakuman => false;

        public bool IsApplicable(YakuContext ctx, HandDecomposition decomp)
            => ctx.IsRinshan && ctx.IsTsumo;

        public int GetHan(YakuContext ctx, HandDecomposition decomp) => 1;
        public int GetHanCalled(YakuContext ctx, HandDecomposition decomp) => 1;
    }

    /// <summary>창깡 (槍槓): 다른 사람의 가깡(添槓) 패를 론. 1판.</summary>
    public sealed class Chankan : IYaku
    {
        public string Name => "槍槓";
        public string NameKr => "창깡";
        public bool IsYakuman => false;

        public bool IsApplicable(YakuContext ctx, HandDecomposition decomp)
            => ctx.IsChankan && !ctx.IsTsumo;

        public int GetHan(YakuContext ctx, HandDecomposition decomp) => 1;
        public int GetHanCalled(YakuContext ctx, HandDecomposition decomp) => 1;
    }
}

namespace MahjongAtelier.Core
{
    /// <summary>
    /// 천화 (天和): 친(親)이 배패 직후 14장으로 화료. 역만.
    /// 게임 매니저에서 IsTenhou 플래그를 세팅해줘야 함.
    /// </summary>
    public sealed class Tenhou : IYaku
    {
        public string Name => "天和";
        public string NameKr => "천화";
        public bool IsYakuman => true;

        public bool IsApplicable(YakuContext ctx, HandDecomposition decomp)
        {
            return ctx.IsTenhou;
        }

        public int GetHan(YakuContext ctx, HandDecomposition decomp) => 1;
        public int GetHanCalled(YakuContext ctx, HandDecomposition decomp) => 0;
    }

    /// <summary>
    /// 지화 (地和): 자(子)가 첫 쯔모로 화료 (그동안 누구도 후로 안 함). 역만.
    /// </summary>
    public sealed class Chiihou : IYaku
    {
        public string Name => "地和";
        public string NameKr => "지화";
        public bool IsYakuman => true;

        public bool IsApplicable(YakuContext ctx, HandDecomposition decomp)
        {
            return ctx.IsChiihou;
        }

        public int GetHan(YakuContext ctx, HandDecomposition decomp) => 1;
        public int GetHanCalled(YakuContext ctx, HandDecomposition decomp) => 0;
    }
}

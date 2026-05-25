namespace MahjongAtelier.Core
{
    /// <summary>
    /// 국사무쌍 (国士無双): 13요구패 모두 + 그 중 하나가 중복. 역만.
    /// 13면 대기 (단순히 어느 1종이든 짝이 되는 형태)는 더블 역만 룰도 있음.
    /// 본 구현은 단순 역만 처리.
    /// </summary>
    public sealed class Kokushi : IYaku
    {
        public string Name => "国士無双";
        public string NameKr => "국사무쌍";
        public bool IsYakuman => true;

        public bool IsApplicable(YakuContext ctx, HandDecomposition decomp)
        {
            return decomp.Form == AgariForm.Kokushi;
        }

        public int GetHan(YakuContext ctx, HandDecomposition decomp) => 1; // 역만 배수
        public int GetHanCalled(YakuContext ctx, HandDecomposition decomp) => 1;
    }
}

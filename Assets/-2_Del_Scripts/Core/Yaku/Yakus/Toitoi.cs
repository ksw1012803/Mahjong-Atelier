namespace MahjongAtelier.Core
{
    /// <summary>
    /// 대대화 (対々和/トイトイ): 모든 면자가 코츠 또는 깡즈. 2판.
    /// 후로해도 동일 점수.
    /// </summary>
    public sealed class Toitoi : IYaku
    {
        public string Name => "対々和";
        public string NameKr => "대대화";
        public bool IsYakuman => false;

        public bool IsApplicable(YakuContext ctx, HandDecomposition decomp)
        {
            if (decomp.Form != AgariForm.Standard) return false;

            foreach (var m in DecompositionHelpers.AllMelds(ctx, decomp))
            {
                if (!m.IsTripletOrQuad) return false;
            }
            return true;
        }

        public int GetHan(YakuContext ctx, HandDecomposition decomp) => 2;
        public int GetHanCalled(YakuContext ctx, HandDecomposition decomp) => 2;
    }
}

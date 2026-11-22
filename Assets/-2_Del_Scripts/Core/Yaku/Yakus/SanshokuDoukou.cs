namespace MahjongAtelier.Core
{
    /// <summary>
    /// 삼색동각 (三色同刻): 만/통/삭 같은 숫자의 코츠. 2판. 후로 동일.
    /// 예: 555m 555p 555s + ...
    /// </summary>
    public sealed class SanshokuDoukou : IYaku
    {
        public string Name => "三色同刻";
        public string NameKr => "삼색동각";
        public bool IsYakuman => false;

        public bool IsApplicable(YakuContext ctx, HandDecomposition decomp)
        {
            if (decomp.Form != AgariForm.Standard) return false;

            // 숫자별로 어떤 수트에서 코츠가 있는지
            var byNumber = new System.Collections.Generic.Dictionary<int, bool[]>();
            foreach (var m in DecompositionHelpers.TripletsAndQuads(ctx, decomp))
            {
                if (!m.BaseKind.Suit.IsNumber()) continue;
                int n = m.BaseKind.Number;
                if (!byNumber.ContainsKey(n)) byNumber[n] = new bool[3];
                byNumber[n][(int)m.BaseKind.Suit] = true;
            }
            foreach (var entry in byNumber.Values)
            {
                if (entry[0] && entry[1] && entry[2]) return true;
            }
            return false;
        }

        public int GetHan(YakuContext ctx, HandDecomposition decomp) => 2;
        public int GetHanCalled(YakuContext ctx, HandDecomposition decomp) => 2;
    }

    /// <summary>
    /// 삼강자 (三槓子): 깡 3개. 2판. 후로 동일.
    /// </summary>
    public sealed class Sankantsu : IYaku
    {
        public string Name => "三槓子";
        public string NameKr => "삼강자";
        public bool IsYakuman => false;

        public bool IsApplicable(YakuContext ctx, HandDecomposition decomp)
        {
            return DecompositionHelpers.CountQuads(ctx, decomp) == 3;
        }

        public int GetHan(YakuContext ctx, HandDecomposition decomp) => 2;
        public int GetHanCalled(YakuContext ctx, HandDecomposition decomp) => 2;
    }
}

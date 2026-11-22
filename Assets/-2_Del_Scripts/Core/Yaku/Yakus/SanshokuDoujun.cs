using System.Collections.Generic;

namespace MahjongAtelier.Core
{
    /// <summary>
    /// 삼색동순 (三色同順): 만/통/삭 같은 숫자의 슌츠. 멘젠 2판 / 후로 1판.
    /// 예: 234m 234p 234s + ...
    /// </summary>
    public sealed class SanshokuDoujun : IYaku
    {
        public string Name => "三色同順";
        public string NameKr => "삼색동순";
        public bool IsYakuman => false;

        public bool IsApplicable(YakuContext ctx, HandDecomposition decomp)
        {
            if (decomp.Form != AgariForm.Standard) return false;

            var seqs = DecompositionHelpers.Sequences(ctx, decomp);
            if (seqs.Count < 3) return false;

            // 같은 시작 숫자의 슌츠를 수트별로 모음
            // 시작 숫자(1~7)당 boolean[3] (Man/Pin/Sou)
            var bySuitNumber = new Dictionary<int, bool[]>();
            foreach (var m in seqs)
            {
                int n = m.BaseKind.Number;
                if (!bySuitNumber.ContainsKey(n))
                    bySuitNumber[n] = new bool[3];

                int suitIdx = (int)m.BaseKind.Suit;
                if (suitIdx >= 0 && suitIdx < 3)
                    bySuitNumber[n][suitIdx] = true;
            }

            foreach (var entry in bySuitNumber.Values)
            {
                if (entry[0] && entry[1] && entry[2]) return true;
            }
            return false;
        }

        public int GetHan(YakuContext ctx, HandDecomposition decomp) => 2;
        public int GetHanCalled(YakuContext ctx, HandDecomposition decomp) => 1;
    }
}

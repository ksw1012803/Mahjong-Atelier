using System.Collections.Generic;

namespace MahjongAtelier.Core
{
    /// <summary>
    /// 일기통관 (一気通貫): 같은 수트의 123+456+789. 멘젠 2판 / 후로 1판.
    /// 약칭 잇츠(一通).
    /// </summary>
    public sealed class Ittsu : IYaku
    {
        public string Name => "一気通貫";
        public string NameKr => "일기통관";
        public bool IsYakuman => false;

        public bool IsApplicable(YakuContext ctx, HandDecomposition decomp)
        {
            if (decomp.Form != AgariForm.Standard) return false;

            var seqs = DecompositionHelpers.Sequences(ctx, decomp);
            if (seqs.Count < 3) return false;

            // 수트별로 시작 숫자 집합 만들기
            var startsBySuit = new Dictionary<TileSuit, HashSet<int>>();
            foreach (var m in seqs)
            {
                var suit = m.BaseKind.Suit;
                if (!startsBySuit.ContainsKey(suit))
                    startsBySuit[suit] = new HashSet<int>();
                startsBySuit[suit].Add(m.BaseKind.Number);
            }

            foreach (var entry in startsBySuit.Values)
            {
                if (entry.Contains(1) && entry.Contains(4) && entry.Contains(7))
                    return true;
            }
            return false;
        }

        public int GetHan(YakuContext ctx, HandDecomposition decomp) => 2;
        public int GetHanCalled(YakuContext ctx, HandDecomposition decomp) => 1;
    }
}

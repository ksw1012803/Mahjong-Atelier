namespace MahjongAtelier.Core
{
    /// <summary>
    /// 이페코 (一盃口): 같은 슌츠 2조. 1판. 멘젠 한정.
    /// 예: 234m 234m + ...
    /// </summary>
    public sealed class Iipeikou : IYaku
    {
        public string Name => "一盃口";
        public string NameKr => "이페코";
        public bool IsYakuman => false;

        public bool IsApplicable(YakuContext ctx, HandDecomposition decomp)
        {
            if (!ctx.IsMenzen) return false;
            if (decomp.Form != AgariForm.Standard) return false;

            // 슌츠끼리 비교해서 동일한 쌍이 정확히 1쌍 있는지
            // (2쌍이면 양 이페코[2판]이고, 이페코[1판]가 아님)
            int sameSequencePairs = CountSameSequencePairs(decomp);
            return sameSequencePairs == 1;
        }

        public static int CountSameSequencePairs(HandDecomposition decomp)
        {
            if (decomp.Form != AgariForm.Standard || decomp.Melds == null) return 0;

            var sequences = new System.Collections.Generic.List<TileKind>();
            foreach (var m in decomp.Melds)
                if (m.IsSequence) sequences.Add(m.BaseKind);

            // 같은 슌츠를 쌍으로 셈
            sequences.Sort();
            int pairs = 0;
            int i = 0;
            while (i < sequences.Count - 1)
            {
                if (sequences[i] == sequences[i + 1])
                {
                    pairs++;
                    i += 2; // 매칭된 두 개는 건너뜀
                }
                else
                {
                    i++;
                }
            }
            return pairs;
        }

        public int GetHan(YakuContext ctx, HandDecomposition decomp) => 1;
        public int GetHanCalled(YakuContext ctx, HandDecomposition decomp) => 0;
    }
}

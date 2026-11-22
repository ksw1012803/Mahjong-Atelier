namespace MahjongAtelier.Core
{
    /// <summary>
    /// 챤타 (混全帯幺九): 모든 면자와 머리에 요구패(1·9·자패)가 포함됨. 멘젠 2판 / 후로 1판.
    /// 자패가 하나라도 있어야 챤타. 자패 없으면 준챤타.
    /// </summary>
    public sealed class Chanta : IYaku
    {
        public string Name => "混全帯幺九";
        public string NameKr => "챤타";
        public bool IsYakuman => false;

        public bool IsApplicable(YakuContext ctx, HandDecomposition decomp)
        {
            if (decomp.Form != AgariForm.Standard) return false;

            bool hasHonor = false;

            foreach (var m in DecompositionHelpers.AllMeldsWithPair(ctx, decomp))
            {
                if (!m.HasTerminalOrHonor) return false;
                if (m.BaseKind.IsHonor) hasHonor = true;
            }

            // 자패 없으면 준챤타 영역 → 챤타 아님
            if (!hasHonor) return false;

            // 슌츠가 하나도 없으면 혼노두 영역인데, 그래도 챤타 조건은 만족.
            // 단, 일본 표준 룰에선 혼노두와 챤타가 동시에 성립할 때 둘 다 인정하지 않고 혼노두만 인정하는 변종이 있음.
            // 본 구현에선 양쪽 다 부여 (작혼 룰)
            return true;
        }

        public int GetHan(YakuContext ctx, HandDecomposition decomp) => 2;
        public int GetHanCalled(YakuContext ctx, HandDecomposition decomp) => 1;
    }

    /// <summary>
    /// 준챤타 (純全帯幺九): 모든 면자/머리가 노두패(1·9)를 포함. 자패 없음. 멘젠 3판 / 후로 2판.
    /// </summary>
    public sealed class Junchan : IYaku
    {
        public string Name => "純全帯幺九";
        public string NameKr => "준챤타";
        public bool IsYakuman => false;

        public bool IsApplicable(YakuContext ctx, HandDecomposition decomp)
        {
            if (decomp.Form != AgariForm.Standard) return false;

            foreach (var m in DecompositionHelpers.AllMeldsWithPair(ctx, decomp))
            {
                // 자패가 하나라도 있으면 준챤타 아님
                if (m.BaseKind.IsHonor) return false;
                if (!m.HasTerminalOrHonor) return false;
            }
            return true;
        }

        public int GetHan(YakuContext ctx, HandDecomposition decomp) => 3;
        public int GetHanCalled(YakuContext ctx, HandDecomposition decomp) => 2;
    }

    /// <summary>
    /// 혼노두 (混老頭): 모든 패가 요구패(1·9·자패). 2판.
    /// 슌츠 불가능하므로 필연적으로 대대화 동반 또는 칠대자 동반.
    /// </summary>
    public sealed class Honroutou : IYaku
    {
        public string Name => "混老頭";
        public string NameKr => "혼노두";
        public bool IsYakuman => false;

        public bool IsApplicable(YakuContext ctx, HandDecomposition decomp)
        {
            // 칠대자 또는 표준형 둘 다 가능
            if (decomp.Form == AgariForm.Standard)
            {
                foreach (var m in DecompositionHelpers.AllMeldsWithPair(ctx, decomp))
                {
                    if (!m.AllTerminalOrHonor) return false;
                }
                return true;
            }
            if (decomp.Form == AgariForm.Chiitoitsu)
            {
                foreach (var p in decomp.PairsForChiitoitsu)
                {
                    if (!p.BaseKind.IsTerminalOrHonor) return false;
                }
                return true;
            }
            return false;
        }

        public int GetHan(YakuContext ctx, HandDecomposition decomp) => 2;
        public int GetHanCalled(YakuContext ctx, HandDecomposition decomp) => 2;
    }
}

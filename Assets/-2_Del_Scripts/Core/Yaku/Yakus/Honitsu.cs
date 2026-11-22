namespace MahjongAtelier.Core
{
    /// <summary>
    /// 혼일색 (混一色): 한 수트 + 자패. 멘젠 3판 / 후로 2판.
    /// </summary>
    public sealed class Honitsu : IYaku
    {
        public string Name => "混一色";
        public string NameKr => "혼일색";
        public bool IsYakuman => false;

        public bool IsApplicable(YakuContext ctx, HandDecomposition decomp)
        {
            // 칠대자도 가능 (그 경우 한 수트 + 자패의 쌍들)
            if (decomp.Form == AgariForm.Chiitoitsu)
            {
                bool foundNumber = false;
                bool hasHonor = false;
                TileSuit numSuit = TileSuit.Man;
                foreach (var p in decomp.PairsForChiitoitsu)
                {
                    if (p.BaseKind.Suit.IsHonor()) { hasHonor = true; continue; }
                    if (!foundNumber) { numSuit = p.BaseKind.Suit; foundNumber = true; }
                    else if (p.BaseKind.Suit != numSuit) return false;
                }
                return foundNumber && hasHonor;
            }

            if (decomp.Form != AgariForm.Standard) return false;

            return DecompositionHelpers.IsHonitsu(ctx, decomp, out _, out bool hasHonors) && hasHonors;
        }

        public int GetHan(YakuContext ctx, HandDecomposition decomp) => 3;
        public int GetHanCalled(YakuContext ctx, HandDecomposition decomp) => 2;
    }

    /// <summary>
    /// 청일색 (清一色): 한 수트만. 멘젠 6판 / 후로 5판. (가장 비싼 일반 역)
    /// </summary>
    public sealed class Chinitsu : IYaku
    {
        public string Name => "清一色";
        public string NameKr => "청일색";
        public bool IsYakuman => false;

        public bool IsApplicable(YakuContext ctx, HandDecomposition decomp)
        {
            // 칠대자도 가능
            if (decomp.Form == AgariForm.Chiitoitsu)
            {
                bool foundNumber = false;
                TileSuit suit = TileSuit.Man;
                foreach (var p in decomp.PairsForChiitoitsu)
                {
                    if (p.BaseKind.Suit.IsHonor()) return false;
                    if (!foundNumber) { suit = p.BaseKind.Suit; foundNumber = true; }
                    else if (p.BaseKind.Suit != suit) return false;
                }
                return foundNumber;
            }

            if (decomp.Form != AgariForm.Standard) return false;
            return DecompositionHelpers.IsSingleNumberSuit(ctx, decomp, out _);
        }

        public int GetHan(YakuContext ctx, HandDecomposition decomp) => 6;
        public int GetHanCalled(YakuContext ctx, HandDecomposition decomp) => 5;
    }
}

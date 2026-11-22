namespace MahjongAtelier.Core
{
    /// <summary>
    /// 핀후 (平和): 1판. 멘젠 한정.
    /// 조건:
    ///   1) 표준형 (4슌츠 + 머리)
    ///   2) 모든 면자가 슌츠 (코츠/깡즈 없음)
    ///   3) 머리가 역패가 아님 (자풍/장풍/삼원패 아님)
    ///   4) 화료 대기가 량멘 대기
    ///   5) 멘젠 (후로하면 성립 불가)
    /// </summary>
    public sealed class Pinfu : IYaku
    {
        public string Name => "平和";
        public string NameKr => "핀후";
        public bool IsYakuman => false;

        public bool IsApplicable(YakuContext ctx, HandDecomposition decomp)
        {
            if (!ctx.IsMenzen) return false;
            if (decomp.Form != AgariForm.Standard) return false;

            // 1) 모든 면자가 슌츠
            foreach (var meld in decomp.Melds)
            {
                if (!meld.IsSequence) return false;
            }

            // 2) 머리가 역패 아님
            if (IsValuedPair(decomp.Pair.BaseKind, ctx)) return false;

            // 3) 량멘 대기
            var wait = WaitTypeAnalyzer.DetectWaitType(decomp);
            if (wait != WaitType.Ryanmen) return false;

            return true;
        }

        /// <summary>역패 머리 여부: 자풍, 장풍, 삼원패.</summary>
        private static bool IsValuedPair(TileKind kind, YakuContext ctx)
        {
            if (kind.Suit == TileSuit.Dragon) return true;
            if (kind == ctx.SeatWind) return true;
            if (kind == ctx.RoundWind) return true;
            return false;
        }

        public int GetHan(YakuContext ctx, HandDecomposition decomp) => 1;
        public int GetHanCalled(YakuContext ctx, HandDecomposition decomp) => 0;
    }
}

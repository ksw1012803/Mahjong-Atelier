namespace MahjongAtelier.Core
{
    /// <summary>
    /// 대기 형태. 부수(符) 계산과 핀후 판정에 사용.
    /// </summary>
    public enum WaitType
    {
        /// <summary>량멘(両面): 23 → 1/4 (가장 일반적, 핀후 가능).</summary>
        Ryanmen,

        /// <summary>칸챤(嵌張): 1_3 → 2.</summary>
        Kanchan,

        /// <summary>펜챤(辺張): 12 → 3 또는 89 → 7.</summary>
        Penchan,

        /// <summary>샨퐁(双碰): 두 쌍 중 하나가 코츠가 됨.</summary>
        Shanpon,

        /// <summary>탕키(単騎): 머리 대기.</summary>
        Tanki
    }

    public static class WaitTypeAnalyzer
    {
        /// <summary>
        /// 분해 결과로부터 화료 시의 대기 형태를 추출.
        /// 같은 손패라도 분해 방식에 따라 다를 수 있으므로, 분해마다 계산.
        /// </summary>
        public static WaitType DetectWaitType(HandDecomposition decomp)
        {
            if (decomp.WinningIsPair) return WaitType.Tanki;

            if (decomp.WinningMeldIndex < 0 || decomp.Melds == null)
                return WaitType.Tanki; // 안전 폴백

            var meld = decomp.Melds[decomp.WinningMeldIndex];

            if (meld.IsTripletOrQuad)
                return WaitType.Shanpon;

            if (meld.IsSequence)
            {
                int baseN = meld.BaseKind.Number;
                int winN = decomp.WinningTile.Number;

                // 슌츠 [n, n+1, n+2]에 winningTile이 어디 위치에 있었는지로 판단
                if (winN == baseN + 1)
                    return WaitType.Kanchan; // 가운데 패가 마지막
                if (baseN == 1 && winN == 3)
                    return WaitType.Penchan; // 123에서 3을 기다림
                if (baseN == 7 && winN == 7)
                    return WaitType.Penchan; // 789에서 7을 기다림
                return WaitType.Ryanmen;
            }

            return WaitType.Tanki;
        }
    }
}

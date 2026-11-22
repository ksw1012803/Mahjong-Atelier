namespace MahjongAtelier.Core
{
    /// <summary>
    /// 부수(符) 계산.
    /// 
    /// 부수는 마작 점수의 "단위 크기"를 결정합니다.
    /// 같은 1판이어도 30부와 40부는 점수가 다릅니다.
    /// 
    /// 계산 흐름:
    ///   1) 특수 케이스 처리 (핀후 쯔모/론, 칠대자)
    ///   2) 기본 20부
    ///   3) 화료 방식 (멘젠 론 / 쯔모)
    ///   4) 머리 보너스 (역패)
    ///   5) 대기 보너스 (칸챤/펜챤/탕키)
    ///   6) 각 면자별 점수
    ///   7) 10단위 올림
    /// </summary>
    public static class FuCalculator
    {
        public const int Base = 20;

        /// <summary>
        /// 부수 계산. 핀후/칠대자 등 특수형 자동 처리.
        /// </summary>
        public static int Calculate(YakuContext ctx, YakuResult yakuResult)
        {
            if (yakuResult?.Decomposition == null) return Base;

            // 칠대자는 25부 고정
            if (yakuResult.Decomposition.Form == AgariForm.Chiitoitsu)
                return 25;

            // 국사무쌍 등 역만은 부수 무관 (호출 측에서 점수 산정 시 별도 처리)
            // 그래도 기본값 반환
            if (yakuResult.YakumanCount > 0)
                return Base;

            // 핀후 여부 확인 (핀후 = 슌츠 4 + 머리(역패X) + 량멘)
            bool isPinfu = IsPinfuComposed(yakuResult);

            // 핀후 + 쯔모: 20부 고정 (쯔모 +2부 적용 안 함)
            if (isPinfu && ctx.IsTsumo) return 20;

            // 핀후 + 론 + 멘젠: 30부 고정 (멘젠 론 +10부만 적용)
            if (isPinfu && !ctx.IsTsumo) return 30;

            // 일반 계산
            int fu = Base;

            // 1) 화료 방식
            if (ctx.IsTsumo)
            {
                fu += 2; // 쯔모
            }
            else if (ctx.IsMenzen)
            {
                fu += 10; // 멘젠 론
            }

            // 2) 머리 (대자) 보너스
            var pairKind = yakuResult.Decomposition.Pair.BaseKind;
            fu += GetPairFu(pairKind, ctx);

            // 3) 대기 형태 보너스
            fu += GetWaitFu(yakuResult.WaitType);

            // 4) 면자별 점수
            foreach (var meld in yakuResult.Decomposition.Melds)
                fu += GetMeldFu(meld);

            // 후로 면자도 합산
            foreach (var meld in ctx.CalledMelds)
                fu += GetMeldFu(meld);

            // 5) 10단위 올림
            fu = RoundUpToTen(fu);

            // 6) 후로 핀후형(논핀후 후로) 예외: 후로 + 모든 슌츠 + 대기/머리 점수 0인 경우 30부 보장
            //    (멘젠이 아니면 핀후 미성립이라 위 핀후 처리 안 됨 → 모든 보너스 0이면 20부가 됨 → 30부로 보정)
            if (!ctx.IsMenzen && !ctx.IsTsumo && fu == 20)
                fu = 30;

            return fu;
        }

        // === 헬퍼 ===

        private static bool IsPinfuComposed(YakuResult yakuResult)
        {
            foreach (var (yaku, _) in yakuResult.Yakus)
            {
                if (yaku is Pinfu) return true;
            }
            return false;
        }

        /// <summary>머리(대자) 부수: 역패면 +2, 자풍과 장풍 동시 +4.</summary>
        private static int GetPairFu(TileKind pair, YakuContext ctx)
        {
            int fu = 0;

            // 삼원패 머리: +2
            if (pair.Suit == TileSuit.Dragon) fu += 2;

            // 자풍 또는 장풍 머리: 각 +2 (동시면 +4)
            if (pair == ctx.SeatWind) fu += 2;
            if (pair == ctx.RoundWind && ctx.SeatWind != ctx.RoundWind) fu += 2;
            // 자풍==장풍인 경우 (예: 동풍전 친의 동 머리)는 일본 표준에서는 4부.
            // 위 코드는 SeatWind==RoundWind일 때 +2만 더해짐 → 4부로 가야 하므로 보정 필요.
            if (pair == ctx.SeatWind && ctx.SeatWind == ctx.RoundWind)
                fu += 2; // 한 번 더 추가해서 총 +4

            return fu;
        }

        /// <summary>대기 형태 부수.</summary>
        private static int GetWaitFu(WaitType wait)
        {
            switch (wait)
            {
                case WaitType.Kanchan:
                case WaitType.Penchan:
                case WaitType.Tanki:
                    return 2;
                case WaitType.Ryanmen:
                case WaitType.Shanpon:
                default:
                    return 0;
            }
        }

        /// <summary>면자별 부수.</summary>
        private static int GetMeldFu(Meld meld)
        {
            // 슌츠: 0부
            if (meld.IsSequence) return 0;

            // 머리(분해의 머리 외에 별도 호출은 없음 — 안전 차원에서)
            if (meld.IsPair) return 0;

            bool isTerminalOrHonor = meld.BaseKind.IsTerminalOrHonor;

            if (meld.IsTriplet)
            {
                // 코츠
                if (meld.IsConcealed && !meld.IsCalled)
                {
                    // 안각
                    return isTerminalOrHonor ? 8 : 4;
                }
                else
                {
                    // 명각 (펑)
                    return isTerminalOrHonor ? 4 : 2;
                }
            }

            if (meld.IsQuad)
            {
                // 깡즈
                if (meld.IsConcealed && !meld.IsCalled)
                {
                    // 안깡
                    return isTerminalOrHonor ? 32 : 16;
                }
                else
                {
                    // 명깡
                    return isTerminalOrHonor ? 16 : 8;
                }
            }

            return 0;
        }

        /// <summary>10단위 올림. 21~30 → 30, 31~40 → 40 등.</summary>
        public static int RoundUpToTen(int n)
        {
            if (n <= 0) return 0;
            return ((n + 9) / 10) * 10;
        }
    }
}

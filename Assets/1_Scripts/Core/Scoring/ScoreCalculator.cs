namespace MahjongAtelier.Core
{
    /// <summary>
    /// 부수+판수를 실제 점수로 변환.
    /// 
    /// 일본 리치마작 표준 점수표:
    ///   기본점 = 부수 × 2^(판수 + 2)
    ///   단, 상한:
    ///     4판 30부, 3판 70부 이상 → 만관(2000)
    ///     6~7판 → 하네만(3000)
    ///     8~10판 → 배만(4000)
    ///     11~12판 → 삼배만(6000)
    ///     13판 이상 → 역만(8000)
    /// 
    ///   최종 지불:
    ///     친 쯔모: 자 각 (기본점×2)
    ///     자 쯔모: 친 (기본점×2), 다른 자 각 (기본점×1)
    ///     친 론: 쏜 사람 (기본점×6)
    ///     자 론: 쏜 사람 (기본점×4)
    ///     모두 100단위 올림
    /// </summary>
    public static class ScoreCalculator
    {
        private const int ManganBase = 2000;
        private const int HanemanBase = 3000;
        private const int BaimanBase = 4000;
        private const int SanbaimanBase = 6000;
        private const int YakumanBase = 8000;

        /// <summary>
        /// 종합 점수 계산.
        /// </summary>
        /// <param name="ctx">역 컨텍스트 (쯔모 여부, 자풍, 등)</param>
        /// <param name="yakuResult">역 판정 결과</param>
        /// <param name="isDealer">화료자가 친(親=동가)인지</param>
        public static ScoreResult Calculate(YakuContext ctx, YakuResult yakuResult, bool isDealer)
        {
            var result = new ScoreResult
            {
                IsDealer = isDealer,
                IsTsumo = ctx.IsTsumo,
                YakumanCount = yakuResult.YakumanCount,
                Han = yakuResult.TotalHanIncludingDora
            };

            // 1) 역만 처리 (별도 분기)
            if (yakuResult.YakumanCount > 0)
            {
                result.Fu = 0; // 의미 없음
                result.Class = ScoreClass.Yakuman;
                result.BasePoint = YakumanBase * yakuResult.YakumanCount;
                ComputePayments(result);
                return result;
            }

            // 2) 일반 케이스: 부수 계산
            int fu = FuCalculator.Calculate(ctx, yakuResult);
            int han = yakuResult.TotalHanIncludingDora;

            result.Fu = fu;
            result.Han = han;

            // 3) 등급 결정 및 기본점 계산
            DetermineClassAndBasePoint(result, fu, han);

            // 4) 지불 분배 계산
            ComputePayments(result);

            return result;
        }

        private static void DetermineClassAndBasePoint(ScoreResult r, int fu, int han)
        {
            // 13판 이상 = 역만 (수역만/數役満)
            if (han >= 13)
            {
                r.Class = ScoreClass.Yakuman;
                r.BasePoint = YakumanBase;
                return;
            }
            if (han >= 11)
            {
                r.Class = ScoreClass.Sanbaiman;
                r.BasePoint = SanbaimanBase;
                return;
            }
            if (han >= 8)
            {
                r.Class = ScoreClass.Baiman;
                r.BasePoint = BaimanBase;
                return;
            }
            if (han >= 6)
            {
                r.Class = ScoreClass.Haneman;
                r.BasePoint = HanemanBase;
                return;
            }

            // 5판: 항상 만관
            if (han == 5)
            {
                r.Class = ScoreClass.Mangan;
                r.BasePoint = ManganBase;
                return;
            }

            // 1~4판: 부수 × 2^(판+2) 계산, 만관 cap 적용
            int basePoint = fu * (1 << (han + 2));

            if (basePoint >= ManganBase)
            {
                r.Class = ScoreClass.Mangan;
                r.BasePoint = ManganBase;
            }
            else
            {
                r.Class = ScoreClass.Normal;
                r.BasePoint = basePoint;
            }
        }

        /// <summary>기본점으로부터 실제 지불액 분배 계산.</summary>
        private static void ComputePayments(ScoreResult r)
        {
            if (r.IsTsumo)
            {
                if (r.IsDealer)
                {
                    // 친 쯔모: 자 각 base×2
                    int perChild = RoundUpHundred(r.BasePoint * 2);
                    r.TsumoPaymentFromNonDealer = perChild;
                    r.TsumoPaymentFromDealer = 0;
                    r.TotalGain = perChild * 3;
                }
                else
                {
                    // 자 쯔모: 친 base×2, 다른 자 각 base×1
                    int fromDealer = RoundUpHundred(r.BasePoint * 2);
                    int fromChild = RoundUpHundred(r.BasePoint * 1);
                    r.TsumoPaymentFromDealer = fromDealer;
                    r.TsumoPaymentFromNonDealer = fromChild;
                    r.TotalGain = fromDealer + fromChild * 2;
                }
            }
            else
            {
                // 론
                int multiplier = r.IsDealer ? 6 : 4;
                int payment = RoundUpHundred(r.BasePoint * multiplier);
                r.RonPayment = payment;
                r.TotalGain = payment;
            }
        }

        /// <summary>100단위 올림.</summary>
        public static int RoundUpHundred(int n)
        {
            if (n <= 0) return 0;
            return ((n + 99) / 100) * 100;
        }
    }
}

using System.Collections.Generic;

namespace MahjongAtelier.Core
{
    /// <summary>
    /// 역 판정 총괄. v4: 일반 역 + 역만 모두 등록.
    /// 
    /// 등록 순서는 큰 의미 없음. 모든 분해 × 모든 역 검사 후 점수 최대 분해를 선택.
    /// 단, 일부 역은 동시 성립 시 배제 처리:
    ///   - 이배구 ↔ 칠대자: 일본 표준 룰에서 배타. 본 구현은 이배구만 인정.
    ///   - 챤타 ↔ 준챤타: 준챤타 성립 시 챤타 자동 배제 (준챤타가 상위).
    ///   - 청일색 ↔ 혼일색: 청일색이 상위.
    ///   - 이페코 ↔ 이배구: 이배구가 상위.
    /// </summary>
    public sealed class YakuChecker
    {
        private readonly List<IYaku> _registeredYakus = new List<IYaku>();
        private readonly List<IYaku> _registeredYakuman = new List<IYaku>();

        public static YakuChecker CreateStandard()
        {
            var c = new YakuChecker();

            // ====== 1판 역 ======
            c.Register(new Riichi());
            c.Register(new DoubleRiichi());
            c.Register(new Ippatsu());
            c.Register(new MenzenTsumo());
            c.Register(new Tanyao());
            c.Register(new Pinfu());
            c.Register(new Iipeikou());
            c.Register(new Haitei());
            c.Register(new Houtei());
            c.Register(new Rinshan());
            c.Register(new Chankan());

            // 역패
            c.Register(new YakuHai(TileKind.White));
            c.Register(new YakuHai(TileKind.Green));
            c.Register(new YakuHai(TileKind.Red));
            c.Register(new YakuHai(YakuHai.HaiType.SeatWind));
            c.Register(new YakuHai(YakuHai.HaiType.RoundWind));

            // ====== 2판 역 ======
            c.Register(new SanshokuDoujun());
            c.Register(new Ittsu());
            c.Register(new Toitoi());
            c.Register(new Sanankou());
            c.Register(new SanshokuDoukou());
            c.Register(new Sankantsu());
            c.Register(new Shousangen());
            c.Register(new Chanta());
            c.Register(new Honroutou());
            c.Register(new Chiitoitsu());

            // ====== 3판 역 ======
            c.Register(new Junchan());
            c.Register(new Ryanpeikou());
            c.Register(new Honitsu());

            // ====== 6판 역 ======
            c.Register(new Chinitsu());

            // ====== 역만 ======
            c.RegisterYakuman(new Kokushi());
            c.RegisterYakuman(new Daisangen());
            c.RegisterYakuman(new Shousuushi());
            c.RegisterYakuman(new Daisuushi());
            c.RegisterYakuman(new Suuankou());
            c.RegisterYakuman(new Suukantsu());
            c.RegisterYakuman(new Tsuuiisou());
            c.RegisterYakuman(new Chinroutou());
            c.RegisterYakuman(new Ryuuiisou());
            c.RegisterYakuman(new ChuurenPoutou());
            c.RegisterYakuman(new Tenhou());
            c.RegisterYakuman(new Chiihou());

            return c;
        }

        public void Register(IYaku yaku) => _registeredYakus.Add(yaku);
        public void RegisterYakuman(IYaku yaku) => _registeredYakuman.Add(yaku);

        public YakuResult Check(YakuContext ctx)
        {
            var bestResult = new YakuResult();
            if (ctx?.Hand == null || ctx.Hand.Total != 14) return bestResult;

            var decompositions = HandDecomposer.Decompose(ctx.Hand, ctx.WinningTile);
            if (decompositions.Count == 0) return bestResult;

            foreach (var decomp in decompositions)
            {
                var result = CheckSingleDecomposition(ctx, decomp);
                if (CompareResults(result, bestResult) > 0)
                    bestResult = result;
            }

            bestResult.DoraHan = CountDora(ctx);
            return bestResult;
        }

        private YakuResult CheckSingleDecomposition(YakuContext ctx, HandDecomposition decomp)
        {
            var result = new YakuResult
            {
                Decomposition = decomp,
                WaitType = WaitTypeAnalyzer.DetectWaitType(decomp)
            };

            // 1) 역만 먼저 검사 (역만 성립 시 일반 역 무시)
            foreach (var yaku in _registeredYakuman)
            {
                if (yaku.IsApplicable(ctx, decomp))
                {
                    int mul = yaku.GetHan(ctx, decomp);
                    result.YakumanCount += mul;
                    result.Yakus.Add((yaku, mul));
                }
            }

            if (result.YakumanCount > 0)
            {
                // 역만이 있으면 일반 역 평가 생략
                return result;
            }

            // 2) 일반 역 검사 → 임시 리스트에 모음
            var matched = new List<(IYaku yaku, int han)>();
            foreach (var yaku in _registeredYakus)
            {
                if (!yaku.IsApplicable(ctx, decomp)) continue;

                int han = ctx.IsMenzen
                    ? yaku.GetHan(ctx, decomp)
                    : yaku.GetHanCalled(ctx, decomp);
                if (han > 0) matched.Add((yaku, han));
            }

            // 3) 배타 처리

            // 준챤타 성립 시 챤타 제거
            bool hasJunchan = false;
            foreach (var pair in matched) if (pair.yaku is Junchan) { hasJunchan = true; break; }
            if (hasJunchan) matched.RemoveAll(p => p.yaku is Chanta);

            // 청일색 성립 시 혼일색 제거
            bool hasChinitsu = false;
            foreach (var pair in matched) if (pair.yaku is Chinitsu) { hasChinitsu = true; break; }
            if (hasChinitsu) matched.RemoveAll(p => p.yaku is Honitsu);

            // 이배구 성립 시 일배구 제거
            bool hasRyanpeikou = false;
            foreach (var pair in matched) if (pair.yaku is Ryanpeikou) { hasRyanpeikou = true; break; }
            if (hasRyanpeikou) matched.RemoveAll(p => p.yaku is Iipeikou);

            // 4) 결과 리스트에 복사 (Yakus 프로퍼티는 readonly라서 AddRange 사용)
            foreach (var pair in matched)
                result.Yakus.Add(pair);

            return result;
        }

        private static int CompareResults(YakuResult a, YakuResult b)
        {
            if (a.YakumanCount != b.YakumanCount)
                return a.YakumanCount.CompareTo(b.YakumanCount);
            return a.TotalYakuHan.CompareTo(b.TotalYakuHan);
        }

        private int CountDora(YakuContext ctx)
        {
            int dora = 0;
            foreach (var ind in ctx.DoraIndicators)
                dora += ctx.Hand.Get(WallManager.IndicatorToDora(ind));

            if (ctx.IsRiichi || ctx.IsDoubleRiichi)
            {
                foreach (var ind in ctx.UraDoraIndicators)
                    dora += ctx.Hand.Get(WallManager.IndicatorToDora(ind));
            }

            dora += ctx.AkaDoraCount;
            return dora;
        }
    }
}

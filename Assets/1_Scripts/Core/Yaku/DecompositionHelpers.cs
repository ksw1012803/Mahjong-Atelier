using System.Collections.Generic;

namespace MahjongAtelier.Core
{
    /// <summary>
    /// 여러 역에서 공용으로 쓰이는 분해 분석 헬퍼.
    /// 같은 로직을 매 역마다 작성하면 버그 발생 가능성이 높아지므로 한 곳에 모음.
    /// </summary>
    public static class DecompositionHelpers
    {
        /// <summary>표준형 분해의 모든 면자 (분해 + 후로). 머리 제외.</summary>
        public static IEnumerable<Meld> AllMelds(YakuContext ctx, HandDecomposition decomp)
        {
            if (decomp.Form != AgariForm.Standard) yield break;
            foreach (var m in decomp.Melds) yield return m;
            foreach (var m in ctx.CalledMelds) yield return m;
        }

        /// <summary>표준형 분해의 모든 면자 + 머리.</summary>
        public static IEnumerable<Meld> AllMeldsWithPair(YakuContext ctx, HandDecomposition decomp)
        {
            if (decomp.Form != AgariForm.Standard) yield break;
            foreach (var m in decomp.Melds) yield return m;
            foreach (var m in ctx.CalledMelds) yield return m;
            yield return decomp.Pair;
        }

        /// <summary>슌츠 목록 (분해 + 후로).</summary>
        public static List<Meld> Sequences(YakuContext ctx, HandDecomposition decomp)
        {
            var list = new List<Meld>();
            foreach (var m in AllMelds(ctx, decomp))
                if (m.IsSequence) list.Add(m);
            return list;
        }

        /// <summary>코츠/깡즈 목록 (분해 + 후로). 머리 제외.</summary>
        public static List<Meld> TripletsAndQuads(YakuContext ctx, HandDecomposition decomp)
        {
            var list = new List<Meld>();
            foreach (var m in AllMelds(ctx, decomp))
                if (m.IsTripletOrQuad) list.Add(m);
            return list;
        }

        /// <summary>안각(暗刻) 개수 - 안깡 포함. 삼암각/사암각 판정.</summary>
        public static int CountConcealedTriplets(YakuContext ctx, HandDecomposition decomp)
        {
            int count = 0;
            if (decomp.Form != AgariForm.Standard) return 0;

            // 분해의 면자 중 안각/안깡
            // 단, 론으로 화료한 마지막 패가 포함된 코츠는 명각으로 취급 (샨퐁 대기 시)
            for (int i = 0; i < decomp.Melds.Count; i++)
            {
                var m = decomp.Melds[i];
                if (!m.IsTripletOrQuad) continue;
                if (!m.IsConcealed) continue;

                // 론 + 이 코츠가 화료패를 포함한 경우 → 명각 취급
                if (!ctx.IsTsumo && i == decomp.WinningMeldIndex && !decomp.WinningIsPair)
                    continue;

                count++;
            }

            // 후로의 안깡
            foreach (var m in ctx.CalledMelds)
            {
                if (m.IsQuad && m.IsConcealed) count++;
            }
            return count;
        }

        /// <summary>깡즈(槓子) 개수. 안깡/명깡 모두 포함.</summary>
        public static int CountQuads(YakuContext ctx, HandDecomposition decomp)
        {
            int count = 0;
            foreach (var m in AllMelds(ctx, decomp))
                if (m.IsQuad) count++;
            return count;
        }

        /// <summary>모든 면자/머리가 한 수트만 사용하는지. 청일색 판정.</summary>
        public static bool IsSingleNumberSuit(YakuContext ctx, HandDecomposition decomp, out TileSuit suit)
        {
            suit = TileSuit.Man;
            bool found = false;

            foreach (var m in AllMeldsWithPair(ctx, decomp))
            {
                if (!m.BaseKind.Suit.IsNumber()) return false; // 자패 섞이면 청일색 아님
                if (!found)
                {
                    suit = m.BaseKind.Suit;
                    found = true;
                }
                else if (m.BaseKind.Suit != suit)
                {
                    return false;
                }
            }
            return found;
        }

        /// <summary>모든 면자/머리가 한 수트 + 자패만 사용하는지. 혼일색 판정.</summary>
        public static bool IsHonitsu(YakuContext ctx, HandDecomposition decomp, out TileSuit numberSuit, out bool hasHonors)
        {
            numberSuit = TileSuit.Man;
            hasHonors = false;
            bool foundNumber = false;

            foreach (var m in AllMeldsWithPair(ctx, decomp))
            {
                if (m.BaseKind.Suit.IsHonor())
                {
                    hasHonors = true;
                    continue;
                }
                if (!foundNumber)
                {
                    numberSuit = m.BaseKind.Suit;
                    foundNumber = true;
                }
                else if (m.BaseKind.Suit != numberSuit)
                {
                    return false;
                }
            }
            return foundNumber;
        }
    }
}

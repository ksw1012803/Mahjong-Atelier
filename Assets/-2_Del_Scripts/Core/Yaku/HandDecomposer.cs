using System.Collections.Generic;

namespace MahjongAtelier.Core
{
    /// <summary>
    /// 화료 손패를 가능한 모든 면자 조합으로 분해.
    /// 
    /// 같은 14장 손패도 여러 분해가 가능. 예:
    ///   234m 234m 234m 234m 22m
    ///     → (234m)(234m)(234m)(234m) + 머리 2m   [4슌츠 = 핀후 가능]
    ///     → (222m)(333m)(444m) + ... [불가, 3장씩 빼면 잔여 패가 안 맞음]
    /// 
    /// 핀후/일배구 같은 분해 의존 역의 점수가 가장 높은 분해를 선택해야 하므로
    /// 모든 분해를 열거합니다.
    /// 
    /// 후로(鳴き)는 현재 미지원. 추후 추가 시:
    ///   - 호출 시 calledMelds 매개변수 추가
    ///   - 남은 손패만 분해하고 결과에 calledMelds를 prepend
    /// </summary>
    public static class HandDecomposer
    {
        /// <summary>
        /// 손패를 모든 가능한 분해로 변환.
        /// </summary>
        /// <param name="hand">14장 손패의 카운터 표현</param>
        /// <param name="winningTile">마지막 화료패 (어떤 면자에 들어갔는지 표시용)</param>
        /// <returns>가능한 모든 분해 (없으면 빈 리스트)</returns>
        public static List<HandDecomposition> Decompose(TileCounter hand, TileKind winningTile)
        {
            var results = new List<HandDecomposition>();
            if (hand.Total != 14) return results;

            // 1) 국사무쌍 검사
            if (HandAnalyzer.IsAgari(hand) && IsKokushiHand(hand))
            {
                results.Add(HandDecomposition.Kokushi(winningTile));
                return results;
            }

            // 2) 칠대자 검사
            if (IsChiitoitsuHand(hand))
            {
                var pairs = new List<Meld>(7);
                for (int i = 0; i < TileCounter.Size; i++)
                {
                    if (hand[i] == 2)
                        pairs.Add(new Meld(MeldType.Pair, TileCounter.KindOf(i), isConcealed: true));
                }
                results.Add(new HandDecomposition(pairs, winningTile));
                // 칠대자는 동시에 표준형으로 해석될 수 있는데 (일본 룰은 둘 다 후보)
                // 점수가 더 높은 것이 선택되므로 표준형도 계속 검사
            }

            // 3) 표준형: 모든 머리 후보 × 모든 면자 분해 조합 열거
            for (int pairIdx = 0; pairIdx < TileCounter.Size; pairIdx++)
            {
                if (hand[pairIdx] < 2) continue;

                hand[pairIdx] -= 2;
                var pair = new Meld(MeldType.Pair, TileCounter.KindOf(pairIdx), isConcealed: true);

                var allMeldSets = new List<List<Meld>>();
                EnumerateMelds(hand, 0, new List<Meld>(4), allMeldSets);

                hand[pairIdx] += 2;

                foreach (var meldSet in allMeldSets)
                {
                    // 화료패가 어느 면자에 들어갔는지 식별
                    AnalyzeWinningPosition(meldSet, pair, winningTile,
                        out int winIdx, out bool winIsPair);

                    results.Add(new HandDecomposition(meldSet, pair, winningTile, winIdx, winIsPair));
                }
            }

            return results;
        }

        // === 분해 알고리즘 ===

        /// <summary>
        /// 머리 뺀 12장을 4개의 면자로 분해 가능한 모든 조합을 열거.
        /// 자패는 코츠만 가능하므로 먼저 처리, 수패는 인덱스 순회로 코츠/슌츠 분기.
        /// </summary>
        private static void EnumerateMelds(TileCounter hand, int startIdx,
            List<Meld> current, List<List<Meld>> results)
        {
            // 0이 아닌 첫 인덱스 찾기
            int i = startIdx;
            while (i < TileCounter.Size && hand[i] == 0) i++;

            if (i >= TileCounter.Size)
            {
                // 분해 완료
                if (current.Count == 4)
                    results.Add(new List<Meld>(current));
                return;
            }

            var kind = TileCounter.KindOf(i);

            // 분기 1: 코츠 (3장)
            if (hand[i] >= 3)
            {
                hand[i] -= 3;
                current.Add(new Meld(MeldType.Triplet, kind, isConcealed: true));
                EnumerateMelds(hand, i, current, results);
                current.RemoveAt(current.Count - 1);
                hand[i] += 3;
            }

            // 분기 2: 슌츠 (i, i+1, i+2) — 자패는 불가, 수트 경계 넘으면 안 됨
            if (kind.Suit.IsNumber() && kind.Number <= 7)
            {
                if (hand[i] >= 1 && hand[i + 1] >= 1 && hand[i + 2] >= 1)
                {
                    hand[i]--; hand[i + 1]--; hand[i + 2]--;
                    current.Add(new Meld(MeldType.Sequence, kind, isConcealed: true));
                    EnumerateMelds(hand, i, current, results);
                    current.RemoveAt(current.Count - 1);
                    hand[i]++; hand[i + 1]++; hand[i + 2]++;
                }
            }
        }

        // === 화료 위치 분석 ===

        /// <summary>화료패가 어느 면자/머리에 흡수되었는지 찾기.</summary>
        private static void AnalyzeWinningPosition(List<Meld> melds, Meld pair,
            TileKind winningTile, out int winIdx, out bool winIsPair)
        {
            // 머리에 흡수된 경우
            if (pair.BaseKind == winningTile)
            {
                // 머리가 단기 대기였는지 여부는 컨텍스트에서 따로 판단 가능.
                // 일단 "머리에도 들어갔다" 표시
                winIsPair = true;
                winIdx = -1;
                return;
            }

            // 면자 중 어떤 게 화료패를 포함하는지
            for (int i = 0; i < melds.Count; i++)
            {
                if (melds[i].Contains(winningTile))
                {
                    winIdx = i;
                    winIsPair = false;
                    return;
                }
            }

            // 도달 불가 (화료패가 손패에 있어야 함)
            winIdx = -1;
            winIsPair = false;
        }

        // === 보조 판정기 (HandAnalyzer와 중복되지만 분해 직전 빠른 컷을 위해) ===

        private static bool IsChiitoitsuHand(TileCounter hand)
        {
            int pairs = 0;
            for (int i = 0; i < TileCounter.Size; i++)
            {
                int c = hand[i];
                if (c == 0) continue;
                if (c == 2) pairs++;
                else return false;
            }
            return pairs == 7;
        }

        private static readonly int[] TerminalHonorIndices =
        {
            0, 8, 9, 17, 18, 26, 27, 28, 29, 30, 31, 32, 33
        };

        private static bool IsKokushiHand(TileCounter hand)
        {
            int pair = 0, single = 0;
            foreach (int i in TerminalHonorIndices)
            {
                int c = hand[i];
                if (c == 0) return false;
                if (c == 1) single++;
                else if (c == 2) pair++;
                else return false;
            }
            return pair == 1 && single == 12;
        }
    }
}

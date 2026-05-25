namespace MahjongAtelier.Core
{
    /// <summary>
    /// 화료(和了) 판정기.
    /// 14장 손패가 화료 형태인지 판정합니다.
    /// 
    /// 알고리즘:
    ///   1) 국사무쌍 검사 — 가장 빠름, O(1)
    ///   2) 칠대자 검사 — O(34)
    ///   3) 표준형 검사 — 머리 후보를 모두 시도하며 면자 분해 (재귀)
    /// 
    /// ※ 이 클래스는 "완성형" 판정만 합니다. 텐파이(1장 부족) 판정은 TenpaiAnalyzer 참조.
    /// ※ 후로(鳴き)는 현재 미지원. 후로 추가 시 면자 수만 미리 빼고 호출.
    /// </summary>
    public static class HandAnalyzer
    {
        /// <summary>14장 손패의 화료 형태를 판정.</summary>
        public static AgariForm DetectAgariForm(TileCounter hand)
        {
            // 후로 미지원 가정 → 14장이어야 함
            if (hand.Total != 14) return AgariForm.None;

            if (IsKokushi(hand)) return AgariForm.Kokushi;
            if (IsChiitoitsu(hand)) return AgariForm.Chiitoitsu;
            if (IsStandard(hand)) return AgariForm.Standard;

            return AgariForm.None;
        }

        /// <summary>편의 메서드.</summary>
        public static bool IsAgari(TileCounter hand) =>
            DetectAgariForm(hand) != AgariForm.None;

        // ================================================================
        // 국사무쌍 (国士無双)
        // 13요구패 모두 1장 이상 + 그 중 하나가 1장 더 (총 14장)
        // ================================================================

        private static readonly int[] TerminalHonorIndices =
        {
            0, 8,           // 1m, 9m
            9, 17,          // 1p, 9p
            18, 26,         // 1s, 9s
            27, 28, 29, 30, // 東南西北
            31, 32, 33      // 白發中
        };

        private static bool IsKokushi(TileCounter hand)
        {
            // 모든 패가 요구패여야 함
            // 13종 모두 등장 + 그 중 1종만 2장, 나머지 1장
            int pairCount = 0;
            int singleCount = 0;

            foreach (int idx in TerminalHonorIndices)
            {
                int c = hand[idx];
                if (c == 0) return false;
                if (c == 1) singleCount++;
                else if (c == 2) pairCount++;
                else return false;
            }

            // 요구패가 아닌 곳에 패가 있으면 안 됨
            // (TerminalHonorIndices 합계가 14면 자동으로 보장되지만 명시적 체크)
            if (pairCount != 1 || singleCount != 12) return false;

            return true;
        }

        // ================================================================
        // 칠대자 (七対子)
        // 정확히 7쌍 (서로 다른 7종). 같은 패 4장은 2쌍으로 치지 않음 (룰).
        // ================================================================

        private static bool IsChiitoitsu(TileCounter hand)
        {
            int pairs = 0;
            for (int i = 0; i < TileCounter.Size; i++)
            {
                int c = hand[i];
                if (c == 0) continue;
                if (c == 2) pairs++;
                else return false; // 1장, 3장, 4장은 칠대자 불가
            }
            return pairs == 7;
        }

        // ================================================================
        // 표준형 (4면자 + 1머리)
        // 모든 머리 후보를 시도하며, 남은 패가 면자로만 분해되는지 검사.
        // ================================================================

        private static bool IsStandard(TileCounter hand)
        {
            // 머리 후보 = 2장 이상인 종류
            for (int i = 0; i < TileCounter.Size; i++)
            {
                if (hand[i] < 2) continue;

                // 머리로 빼고 나머지를 면자로 분해 시도
                hand[i] -= 2;
                bool ok = CanDecomposeAllMelds(hand);
                hand[i] += 2; // 복원

                if (ok) return true;
            }
            return false;
        }

        /// <summary>
        /// 남은 손패(머리 제외)가 모두 면자(코츠/슌츠)로 분해되는지.
        /// 수트별로 독립 처리. 자패는 코츠(3장)만 가능.
        /// </summary>
        private static bool CanDecomposeAllMelds(TileCounter hand)
        {
            // 자패 먼저: 각 종류는 0 또는 3장만 허용
            for (int i = 27; i < 34; i++)
            {
                if (hand[i] != 0 && hand[i] != 3) return false;
            }

            // 수패: 각 수트마다 독립적으로 면자 분해
            return CanDecomposeSuit(hand, 0)  &&  // 만
                   CanDecomposeSuit(hand, 9)  &&  // 통
                   CanDecomposeSuit(hand, 18);    // 삭
        }

        /// <summary>
        /// 9장 슬롯(한 수트)을 모두 면자로 분해 가능한지.
        /// 그리디 알고리즘: 가장 작은 숫자부터 보면서
        ///   - 코츠(3장 동일)로 빼거나
        ///   - 슌츠(연속 3장)로 빼거나
        /// 두 분기를 모두 시도.
        /// </summary>
        private static bool CanDecomposeSuit(TileCounter hand, int suitStart)
        {
            // 처음으로 0이 아닌 위치 찾기
            int i = suitStart;
            while (i < suitStart + 9 && hand[i] == 0) i++;
            if (i >= suitStart + 9) return true; // 이 수트 끝

            // 분기 1: 코츠로 빼기
            if (hand[i] >= 3)
            {
                hand[i] -= 3;
                if (CanDecomposeSuit(hand, suitStart)) { hand[i] += 3; return true; }
                hand[i] += 3;
            }

            // 분기 2: 슌츠로 빼기 (i, i+1, i+2)
            // 수트 마지막 2개 위치에서는 슌츠 불가
            int localPos = i - suitStart; // 0~8
            if (localPos <= 6 && hand[i] >= 1 && hand[i + 1] >= 1 && hand[i + 2] >= 1)
            {
                hand[i]--; hand[i + 1]--; hand[i + 2]--;
                if (CanDecomposeSuit(hand, suitStart))
                {
                    hand[i]++; hand[i + 1]++; hand[i + 2]++;
                    return true;
                }
                hand[i]++; hand[i + 1]++; hand[i + 2]++;
            }

            return false;
        }
    }
}

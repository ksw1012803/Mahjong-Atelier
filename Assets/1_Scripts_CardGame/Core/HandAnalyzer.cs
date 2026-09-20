using System.Collections.Generic;

namespace CardGame.Core
{
    /// <summary>
    /// 완성 패턴 종류.
    /// </summary>
    public enum CompletionPattern
    {
        None = 0,           // 완성 못 함
        SetAndRun = 1,      // 세트 + 런 (10점, ×1)
        DoubleRun = 2,      // 런 + 런 (15점, ×1.5)
        DoubleSet = 3,      // 세트 + 세트 (15점, ×1.5)
        SameColor = 4,      // 단색 완성 (20점, ×2)
        SevenRun = 5        // 7연속 (30점, ×3)
    }

    /// <summary>
    /// 완성 판정 결과.
    /// </summary>
    public sealed class CompletionResult
    {
        public CompletionPattern Pattern { get; set; } = CompletionPattern.None;
        public int Points { get; set; }
        public bool IsComplete => Pattern != CompletionPattern.None;

        public static int GetPoints(CompletionPattern pattern)
        {
            return pattern switch
            {
                CompletionPattern.SetAndRun => 10,
                CompletionPattern.DoubleRun => 15,
                CompletionPattern.DoubleSet => 15,
                CompletionPattern.SameColor => 20,
                CompletionPattern.SevenRun => 30,
                _ => 0
            };
        }

        public static string GetPatternName(CompletionPattern pattern)
        {
            return pattern switch
            {
                CompletionPattern.SetAndRun => "기본 완성",
                CompletionPattern.DoubleRun => "더블 런",
                CompletionPattern.DoubleSet => "더블 세트",
                CompletionPattern.SameColor => "단색 완성",
                CompletionPattern.SevenRun => "7연속",
                _ => "미완성"
            };
        }
    }

    /// <summary>
    /// 7장 손패 완성 판정.
    /// 
    /// 판정 우선순위 (높은 점수부터, 중첩 안 함):
    ///   1) 7연속 (30) - 같은 색 연속 7장
    ///   2) 단색 완성 (20) - 같은 색 7장 + 2개 런으로 분리
    ///   3) 더블 런 (15) - 런 + 런
    ///   4) 더블 세트 (15) - 세트 + 세트
    ///   5) 기본 완성 (10) - 세트 + 런
    /// 
    /// 와일드 카드는 이번 구현에서 미포함 (다음 단계).
    /// </summary>
    public static class HandAnalyzer
    {
        /// <summary>
        /// 손패 7장으로 최고 점수 완성 판정.
        /// </summary>
        public static CompletionResult Analyze(List<CardData> hand)
        {
            var result = new CompletionResult();
            if (hand == null || hand.Count != 7) return result;

            // 특수 카드가 손에 있으면 완성 불가 (일반 카드만으로 완성)
            foreach (var c in hand)
                if (c.Kind.IsSpecial) return result;

            var kinds = new List<CardKind>();
            foreach (var c in hand) kinds.Add(c.Kind);

            // 높은 점수부터 검사
            if (IsSevenRun(kinds))
            {
                result.Pattern = CompletionPattern.SevenRun;
            }
            else if (IsSameColorRuns(kinds))
            {
                result.Pattern = CompletionPattern.SameColor;
            }
            else if (IsDoubleRun(kinds))
            {
                result.Pattern = CompletionPattern.DoubleRun;
            }
            else if (IsDoubleSet(kinds))
            {
                result.Pattern = CompletionPattern.DoubleSet;
            }
            else if (IsSetAndRun(kinds))
            {
                result.Pattern = CompletionPattern.SetAndRun;
            }

            result.Points = CompletionResult.GetPoints(result.Pattern);
            return result;
        }

        // === 각 패턴 판정 ===

        /// <summary>7연속: 같은 색 연속 7장.</summary>
        private static bool IsSevenRun(List<CardKind> kinds)
        {
            var color = kinds[0].Color;
            var nums = new List<int>();
            foreach (var k in kinds)
            {
                if (k.Color != color) return false;
                nums.Add(k.Number);
            }
            nums.Sort();
            for (int i = 0; i < nums.Count - 1; i++)
            {
                if (nums[i + 1] != nums[i] + 1) return false;
            }
            return true;
        }

        /// <summary>단색 완성: 같은 색 7장, 2개 이상의 런으로 완전 분리.</summary>
        private static bool IsSameColorRuns(List<CardKind> kinds)
        {
            var color = kinds[0].Color;
            foreach (var k in kinds)
                if (k.Color != color) return false;

            // 이미 7연속 아닌 게 확인됐으니 (호출 순서상), 여기서는 2개 런으로 분리 가능한지 검사
            // 숫자 카운트
            var counts = new int[10]; // index 1~9
            foreach (var k in kinds) counts[k.Number]++;

            // 같은 숫자 2장 이상 있으면 런 2개로 분리 불가 (한 런에 같은 숫자 중복 못 함)
            for (int i = 1; i <= 9; i++)
                if (counts[i] > 1) return false;

            // 연속 구간을 찾아 각 구간이 런(3장 이상)이 되는지 확인
            var runs = FindConsecutiveRuns(counts);
            if (runs.Count < 2) return false;

            int totalRunLength = 0;
            foreach (var runLen in runs)
            {
                if (runLen < 3) return false; // 각 런은 최소 3장
                totalRunLength += runLen;
            }
            return totalRunLength == 7;
        }

        /// <summary>더블 런: 두 개의 런으로 완성 (색 무관).</summary>
        private static bool IsDoubleRun(List<CardKind> kinds)
        {
            // 색상별로 분리
            var byColor = GroupByColor(kinds);

            // 정확히 2가지 색상이어야 하고, 각 색상이 런을 이루어야 함
            // 또는 같은 색 2개 런 (이건 SameColor에서 이미 잡힘, 여기선 다른 색)
            if (byColor.Count == 2)
            {
                foreach (var kv in byColor)
                {
                    if (!IsValidRun(kv.Value)) return false;
                }
                // 두 런의 길이 합이 7이어야 하고 각 3장 이상
                int len1 = 0, len2 = 0;
                int idx = 0;
                foreach (var kv in byColor)
                {
                    if (idx == 0) len1 = kv.Value.Count;
                    else len2 = kv.Value.Count;
                    idx++;
                }
                return (len1 >= 3 && len2 >= 3 && len1 + len2 == 7);
            }

            // 같은 색 두 개 런 (예: R123 + R567)
            if (byColor.Count == 1)
            {
                foreach (var kv in byColor)
                {
                    var nums = new List<int>();
                    foreach (var k in kv.Value) nums.Add(k.Number);
                    return CanSplitIntoTwoRuns(nums);
                }
            }
            return false;
        }

        /// <summary>더블 세트: 세트 + 세트 (3+4 또는 4+3).</summary>
        private static bool IsDoubleSet(List<CardKind> kinds)
        {
            // 숫자별로 그룹핑 → 각 숫자가 3장 또는 4장
            var byNumber = new Dictionary<int, List<CardKind>>();
            foreach (var k in kinds)
            {
                if (!byNumber.ContainsKey(k.Number)) byNumber[k.Number] = new List<CardKind>();
                byNumber[k.Number].Add(k);
            }

            if (byNumber.Count != 2) return false;

            int a = 0, b = 0;
            int idx = 0;
            foreach (var kv in byNumber)
            {
                if (!IsValidSet(kv.Value)) return false;
                if (idx == 0) a = kv.Value.Count;
                else b = kv.Value.Count;
                idx++;
            }
            return (a == 3 && b == 4) || (a == 4 && b == 3);
        }

        /// <summary>기본 완성: 세트(3 또는 4) + 런(4 또는 3).</summary>
        private static bool IsSetAndRun(List<CardKind> kinds)
        {
            // 모든 조합 시도: 3장 조합 or 4장 조합을 세트로, 나머지를 런으로
            // 손패에서 세트가 될 수 있는 숫자 후보를 찾음

            var byNumber = new Dictionary<int, List<CardKind>>();
            foreach (var k in kinds)
            {
                if (!byNumber.ContainsKey(k.Number)) byNumber[k.Number] = new List<CardKind>();
                byNumber[k.Number].Add(k);
            }

            foreach (var kv in byNumber)
            {
                int setSize = kv.Value.Count;
                // 세트는 3장 또는 4장
                if (setSize < 3 || setSize > 4) continue;
                if (!IsValidSet(kv.Value)) continue;

                // 나머지 카드가 런을 이루는지
                var remaining = new List<CardKind>();
                foreach (var k in kinds)
                {
                    if (k.Number == kv.Key) continue;
                    remaining.Add(k);
                }
                // 나머지 카드 수 = 7 - setSize
                if (remaining.Count != 7 - setSize) continue;
                if (remaining.Count < 3) continue; // 런은 3장 이상
                if (IsValidRun(remaining)) return true;
            }
            return false;
        }

        // === 헬퍼 ===

        /// <summary>같은 숫자 3~4장이 다른 색으로 구성된 세트인지.</summary>
        private static bool IsValidSet(List<CardKind> kinds)
        {
            if (kinds.Count < 3 || kinds.Count > 4) return false;
            int num = kinds[0].Number;
            var colorsSeen = new HashSet<CardColor>();
            foreach (var k in kinds)
            {
                if (k.Number != num) return false;
                if (colorsSeen.Contains(k.Color)) return false; // 같은 색 중복 불가
                colorsSeen.Add(k.Color);
            }
            return true;
        }

        /// <summary>같은 색 3장 이상 연속된 런인지.</summary>
        private static bool IsValidRun(List<CardKind> kinds)
        {
            if (kinds.Count < 3) return false;
            var color = kinds[0].Color;
            var nums = new List<int>();
            foreach (var k in kinds)
            {
                if (k.Color != color) return false;
                nums.Add(k.Number);
            }
            nums.Sort();
            for (int i = 0; i < nums.Count - 1; i++)
            {
                if (nums[i + 1] != nums[i] + 1) return false;
            }
            return true;
        }

        /// <summary>색상별로 카드 그룹핑.</summary>
        private static Dictionary<CardColor, List<CardKind>> GroupByColor(List<CardKind> kinds)
        {
            var result = new Dictionary<CardColor, List<CardKind>>();
            foreach (var k in kinds)
            {
                if (!result.ContainsKey(k.Color)) result[k.Color] = new List<CardKind>();
                result[k.Color].Add(k);
            }
            return result;
        }

        /// <summary>숫자 카운트 배열에서 연속 구간 길이 목록.</summary>
        private static List<int> FindConsecutiveRuns(int[] counts)
        {
            var result = new List<int>();
            int i = 1;
            while (i <= 9)
            {
                if (counts[i] > 0)
                {
                    int len = 0;
                    while (i <= 9 && counts[i] > 0)
                    {
                        len++;
                        i++;
                    }
                    result.Add(len);
                }
                else i++;
            }
            return result;
        }

        /// <summary>숫자 리스트를 두 개의 런(3장 이상 연속)으로 분리 가능한지.</summary>
        private static bool CanSplitIntoTwoRuns(List<int> nums)
        {
            if (nums.Count != 7) return false;
            nums.Sort();
            // 중복 없어야 함 (한 런에 같은 숫자 없음)
            for (int i = 0; i < nums.Count - 1; i++)
                if (nums[i] == nums[i + 1]) return false;

            // 연속 구간이 정확히 2개, 각 3장 이상
            int runsCount = 0;
            int runLen = 1;
            for (int i = 1; i < nums.Count; i++)
            {
                if (nums[i] == nums[i - 1] + 1) runLen++;
                else
                {
                    if (runLen < 3) return false;
                    runsCount++;
                    runLen = 1;
                }
            }
            if (runLen < 3) return false;
            runsCount++;
            return runsCount == 2;
        }
    }
}

using System.Collections.Generic;
using UnityEngine;

namespace CardGame.Core
{
    public enum CompletionPattern
    {
        None = 0,
        SetAndRun = 1,      // 10점 (세트+런)
        Monochrome = 2,     // 20점 ★ 신규: 같은 색 7장 (연속 아님)
        DoubleRun = 3,      // 15점 (두 색 각 3장+)
        DoubleSet = 4,      // 15점 (두 숫자 각 3~4장)
        SevenRun = 5        // 30점 (같은 색 + 연속 숫자 7장)
    }

    public sealed class CompletionResult
    {
        public CompletionPattern Pattern { get; set; } = CompletionPattern.None;
        public int Points { get; set; }
        public bool IsComplete => Pattern != CompletionPattern.None;
        public int WildsUsed { get; set; } = 0;

        public static int GetPoints(CompletionPattern pattern) => pattern switch
        {
            CompletionPattern.SetAndRun => 10,
            CompletionPattern.Monochrome => 20,
            CompletionPattern.DoubleRun => 15,
            CompletionPattern.DoubleSet => 15,
            CompletionPattern.SevenRun => 30,
            _ => 0
        };

        public static string GetPatternName(CompletionPattern pattern) => pattern switch
        {
            CompletionPattern.SetAndRun => "기본 완성",
            CompletionPattern.Monochrome => "단색 완성",
            CompletionPattern.DoubleRun => "더블 런",
            CompletionPattern.DoubleSet => "더블 세트",
            CompletionPattern.SevenRun => "7연속",
            _ => "미완성"
        };
    }

    /// <summary>
    /// 7장 손패 완성 판정 v5 — 단색 완성 추가.
    /// 
    /// 우선순위 (높은 점수 우선):
    ///   1. 7연속 (30점) — 같은 색 + 연속 숫자
    ///   2. 단색 완성 (20점) — 같은 색 7장 (연속 X)
    ///   3. 더블 런 (15점) — 두 색 각 3장+
    ///   4. 더블 세트 (15점) — 두 숫자 각 3~4장
    ///   5. 세트+런 (10점)
    /// </summary>
    public static class HandAnalyzer
    {
        public static bool VerboseLog = true;

        public static CompletionResult Analyze(List<CardData> hand)
        {
            var result = new CompletionResult();
            if (hand == null || hand.Count != 7)
            {
                if (VerboseLog) Debug.Log($"[HandAnalyzer] Count 불일치: {hand?.Count ?? 0}");
                return result;
            }

            int wildCount = 0;
            foreach (var c in hand)
            {
                if (c.Kind.Special == SpecialCardType.Wild && c.Kind.IsSpecial) wildCount++;
                else if (c.Kind.IsSpecial)
                {
                    if (VerboseLog) Debug.Log($"[HandAnalyzer] 비Wild 특수카드 있음: {c.Kind.Special}");
                    return result;
                }
            }

            var normalKinds = new List<CardKind>();
            foreach (var c in hand)
                if (!c.Kind.IsSpecial) normalKinds.Add(c.Kind);

            if (VerboseLog) Debug.Log($"[HandAnalyzer] normalKinds={normalKinds.Count}, wild={wildCount}");

            if (wildCount == 0) return AnalyzeNormal(normalKinds, 0);
            return AnalyzeWithWilds(normalKinds, wildCount);
        }

        private static CompletionResult AnalyzeNormal(List<CardKind> kinds, int wildsUsed)
        {
            var result = new CompletionResult();
            if (kinds.Count != 7) return result;

            bool seven = IsSevenConsecutive(kinds);
            bool monochrome = IsMonochrome(kinds);
            bool doubleRun = IsDoubleRun(kinds);
            bool doubleSet = IsDoubleSet(kinds);
            bool setAndRun = IsSetAndRun(kinds);

            if (VerboseLog)
                Debug.Log($"[HandAnalyzer] 체크: 7연속={seven}, 단색={monochrome}, 더블런={doubleRun}, 더블세트={doubleSet}, 세트+런={setAndRun}");

            // 우선순위대로 (높은 점수부터)
            if (seven) result.Pattern = CompletionPattern.SevenRun;
            else if (monochrome) result.Pattern = CompletionPattern.Monochrome;
            else if (doubleRun) result.Pattern = CompletionPattern.DoubleRun;
            else if (doubleSet) result.Pattern = CompletionPattern.DoubleSet;
            else if (setAndRun) result.Pattern = CompletionPattern.SetAndRun;

            result.Points = CompletionResult.GetPoints(result.Pattern);
            result.WildsUsed = wildsUsed;
            return result;
        }

        private static CompletionResult AnalyzeWithWilds(List<CardKind> normalKinds, int wildCount)
        {
            bool wasVerbose = VerboseLog;
            VerboseLog = false;

            var bestResult = new CompletionResult();

            var allPossibleKinds = new List<CardKind>();
            for (int c = 0; c < 4; c++)
                for (int n = 1; n <= 9; n++)
                    allPossibleKinds.Add(new CardKind((CardColor)c, n));

            if (wildCount == 1)
            {
                foreach (var wild1 in allPossibleKinds)
                {
                    var trial = new List<CardKind>(normalKinds) { wild1 };
                    var r = AnalyzeNormal(trial, 1);
                    if (r.Points > bestResult.Points) bestResult = r;
                }
            }
            else if (wildCount == 2)
            {
                foreach (var wild1 in allPossibleKinds)
                {
                    foreach (var wild2 in allPossibleKinds)
                    {
                        var trial = new List<CardKind>(normalKinds) { wild1, wild2 };
                        var r = AnalyzeNormal(trial, 2);
                        if (r.Points > bestResult.Points)
                        {
                            bestResult = r;
                            if (bestResult.Points == 30) { VerboseLog = wasVerbose; return bestResult; }
                        }
                    }
                    if (bestResult.Points == 30) { VerboseLog = wasVerbose; return bestResult; }
                }
            }

            VerboseLog = wasVerbose;
            return bestResult;
        }

        // === 패턴 판정 ===

        private static bool IsSevenConsecutive(List<CardKind> kinds)
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
                if (nums[i + 1] != nums[i] + 1) return false;
            return true;
        }

        /// <summary>
        /// ★ 신규 — 단색 완성: 같은 색 7장 (연속 아니어도 됨).
        /// 7연속 판정과 겹치지만 우선순위로 7연속이 먼저 체크되므로 안전.
        /// </summary>
        private static bool IsMonochrome(List<CardKind> kinds)
        {
            if (kinds.Count != 7) return false;
            var color = kinds[0].Color;
            foreach (var k in kinds)
                if (k.Color != color) return false;
            return true;
        }

        private static bool IsDoubleRun(List<CardKind> kinds)
        {
            var byColor = GroupByColor(kinds);
            if (byColor.Count != 2) return false;

            int count1 = 0, count2 = 0, idx = 0;
            foreach (var kv in byColor)
            {
                if (idx == 0) count1 = kv.Value.Count;
                else count2 = kv.Value.Count;
                idx++;
            }

            return count1 >= 3 && count2 >= 3 && (count1 + count2 == 7);
        }

        private static bool IsDoubleSet(List<CardKind> kinds)
        {
            var byNumber = GroupByNumber(kinds);
            if (byNumber.Count != 2) return false;

            int a = 0, b = 0, idx = 0;
            foreach (var kv in byNumber)
            {
                if (!IsValidSet(kv.Value)) return false;
                if (idx == 0) a = kv.Value.Count;
                else b = kv.Value.Count;
                idx++;
            }
            return (a == 3 && b == 4) || (a == 4 && b == 3);
        }

        private static bool IsSetAndRun(List<CardKind> kinds)
        {
            var byNumber = GroupByNumber(kinds);
            foreach (var kv in byNumber)
            {
                int setSize = kv.Value.Count;
                if (setSize < 3 || setSize > 4) continue;
                if (!IsValidSet(kv.Value)) continue;

                var remaining = new List<CardKind>();
                foreach (var k in kinds)
                {
                    if (k.Number == kv.Key) continue;
                    remaining.Add(k);
                }
                if (remaining.Count != 7 - setSize) continue;
                if (remaining.Count < 3) continue;

                if (IsValidRun(remaining)) return true;
            }
            return false;
        }

        private static bool IsValidSet(List<CardKind> kinds)
        {
            if (kinds.Count < 3 || kinds.Count > 4) return false;
            int num = kinds[0].Number;
            var colorsSeen = new HashSet<CardColor>();
            foreach (var k in kinds)
            {
                if (k.Number != num) return false;
                if (colorsSeen.Contains(k.Color)) return false;
                colorsSeen.Add(k.Color);
            }
            return true;
        }

        private static bool IsValidRun(List<CardKind> kinds)
        {
            if (kinds.Count < 3) return false;
            var color = kinds[0].Color;
            foreach (var k in kinds)
                if (k.Color != color) return false;
            return true;
        }

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

        private static Dictionary<int, List<CardKind>> GroupByNumber(List<CardKind> kinds)
        {
            var result = new Dictionary<int, List<CardKind>>();
            foreach (var k in kinds)
            {
                if (!result.ContainsKey(k.Number)) result[k.Number] = new List<CardKind>();
                result[k.Number].Add(k);
            }
            return result;
        }
    }
}
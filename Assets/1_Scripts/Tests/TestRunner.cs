using System;
using System.Collections.Generic;
using System.Linq;
using MahjongAtelier.Core;

namespace MahjongAtelier.Tests
{
    public static class TestRunner
    {
        private static int _passed = 0;
        private static int _failed = 0;
        private static readonly List<string> _failures = new List<string>();

        public static void Main()
        {
            Console.WriteLine("=== Mahjong Atelier Core Tests ===\n");

            // === TileKind 기본 동작 ===
            Test_TileKind_Equality();
            Test_TileKind_SortOrder();
            Test_TileKind_TerminalDetection();

            // === TileData ===
            Test_TileData_RedFiveValidation();

            // === TileCounter ===
            Test_TileCounter_IndexConversion();
            Test_TileCounter_AddRemove();

            // === HandAnalyzer: 화료 판정 ===
            Test_Agari_Standard_Basic();
            Test_Agari_Standard_AllTriplets();
            Test_Agari_Standard_MixedWithHonors();
            Test_Agari_Chiitoitsu();
            Test_Agari_Chiitoitsu_FourOfKind_NotAllowed();
            Test_Agari_Kokushi_13Way();
            Test_Agari_Kokushi_Single();
            Test_NotAgari_Cases();

            // === TenpaiAnalyzer ===
            Test_Tenpai_ShanponWait();
            Test_Tenpai_KanchanWait();
            Test_Tenpai_PenchanWait();
            Test_Tenpai_RyanmenWait();
            Test_Tenpai_TankiWait();
            Test_Tenpai_Chiitoitsu();
            Test_Tenpai_Kokushi_13Wait();
            Test_NotTenpai();

            // === 텐파이 가능한 버림 ===
            Test_TenpaiDiscards();

            // === 정렬 ===
            Test_HandSorter();

            // === 결과 ===
            Console.WriteLine($"\n=== Results: {_passed} passed, {_failed} failed ===");
            if (_failed > 0)
            {
                Console.WriteLine("\nFailures:");
                foreach (var f in _failures) Console.WriteLine($"  - {f}");
                Environment.Exit(1);
            }
        }

        // ==== Helpers ====

        private static void Assert(bool condition, string testName, string detail = "")
        {
            if (condition)
            {
                _passed++;
                Console.WriteLine($"  ✓ {testName}");
            }
            else
            {
                _failed++;
                _failures.Add($"{testName}{(detail != "" ? " — " + detail : "")}");
                Console.WriteLine($"  ✗ {testName} {detail}");
            }
        }

        /// <summary>표기로 카운터 생성. 예: Hand("123m 456p 789s 11z 222z") </summary>
        private static TileCounter Hand(string notation)
        {
            var c = new TileCounter();
            foreach (var token in notation.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                ParseToken(c, token);
            }
            return c;
        }

        private static void ParseToken(TileCounter c, string token)
        {
            // 마지막 글자가 수트, 나머지는 숫자
            char suitChar = token[^1];
            string nums = token[..^1];

            TileSuit suit = suitChar switch
            {
                'm' => TileSuit.Man,
                'p' => TileSuit.Pin,
                's' => TileSuit.Sou,
                'z' => TileSuit.Wind, // 1~4z = 동남서북, 5~7z = 백발중 (표준 mpsz 표기)
                _ => throw new ArgumentException($"Unknown suit: {suitChar}")
            };

            foreach (char ch in nums)
            {
                int n = ch - '0';
                if (suit == TileSuit.Wind && n >= 5)
                {
                    c.Add(new TileKind(TileSuit.Dragon, n - 4));
                }
                else
                {
                    c.Add(new TileKind(suit, n));
                }
            }
        }

        // ==== Tests ====

        private static void Test_TileKind_Equality()
        {
            var a = TileKind.Man(5);
            var b = new TileKind(TileSuit.Man, 5);
            var c = TileKind.Man(6);

            Assert(a == b, "TileKind: same suit+number → equal");
            Assert(a != c, "TileKind: different number → not equal");
            Assert(a.GetHashCode() == b.GetHashCode(), "TileKind: equal → same hash");
        }

        private static void Test_TileKind_SortOrder()
        {
            Assert(TileKind.Man(1).SortOrder < TileKind.Pin(1).SortOrder,
                "TileKind: Man < Pin order");
            Assert(TileKind.Pin(9).SortOrder < TileKind.Sou(1).SortOrder,
                "TileKind: Pin9 < Sou1 order");
            Assert(TileKind.Sou(9).SortOrder < TileKind.East.SortOrder,
                "TileKind: Sou9 < East order");
            Assert(TileKind.North.SortOrder < TileKind.White.SortOrder,
                "TileKind: North < White (winds before dragons)");
        }

        private static void Test_TileKind_TerminalDetection()
        {
            Assert(TileKind.Man(1).IsTerminal, "TileKind: 1m is terminal");
            Assert(TileKind.Man(9).IsTerminal, "TileKind: 9m is terminal");
            Assert(!TileKind.Man(5).IsTerminal, "TileKind: 5m is NOT terminal");
            Assert(!TileKind.East.IsTerminal, "TileKind: East is NOT terminal (it's honor)");
            Assert(TileKind.East.IsTerminalOrHonor, "TileKind: East is terminal-or-honor");
            Assert(TileKind.Man(1).IsTerminalOrHonor, "TileKind: 1m is terminal-or-honor");
            Assert(!TileKind.Man(5).IsTerminalOrHonor, "TileKind: 5m is simple");
        }

        private static void Test_TileData_RedFiveValidation()
        {
            // 정상: 5m을 아카도라로
            var redFive = new TileData(0, TileKind.Man(5), isRedFive: true);
            Assert(redFive.IsRedFive, "TileData: red 5m created");

            // 정상: 1m을 아카도라가 아닌 일반
            var normal = new TileData(1, TileKind.Man(1));
            Assert(!normal.IsRedFive, "TileData: normal 1m");

            // 비정상: 1m을 아카도라로 → 예외
            bool threw = false;
            try { _ = new TileData(2, TileKind.Man(1), isRedFive: true); }
            catch (ArgumentException) { threw = true; }
            Assert(threw, "TileData: red flag on non-5 throws");

            // 동등성: 같은 종류라도 다른 Id면 다름
            var a = new TileData(10, TileKind.Pin(3));
            var b = new TileData(11, TileKind.Pin(3));
            Assert(a != b, "TileData: same kind different id → not equal");
            Assert(a.Kind == b.Kind, "TileData: but Kind comparison equal");
        }

        private static void Test_TileCounter_IndexConversion()
        {
            Assert(TileCounter.IndexOf(TileKind.Man(1)) == 0, "TileCounter: 1m → 0");
            Assert(TileCounter.IndexOf(TileKind.Man(9)) == 8, "TileCounter: 9m → 8");
            Assert(TileCounter.IndexOf(TileKind.Pin(1)) == 9, "TileCounter: 1p → 9");
            Assert(TileCounter.IndexOf(TileKind.Sou(1)) == 18, "TileCounter: 1s → 18");
            Assert(TileCounter.IndexOf(TileKind.East) == 27, "TileCounter: 東 → 27");
            Assert(TileCounter.IndexOf(TileKind.Red) == 33, "TileCounter: 中 → 33");

            // Round-trip
            for (int i = 0; i < 34; i++)
            {
                var kind = TileCounter.KindOf(i);
                Assert(TileCounter.IndexOf(kind) == i, $"TileCounter: roundtrip index {i}");
            }
        }

        private static void Test_TileCounter_AddRemove()
        {
            var c = new TileCounter();
            c.Add(TileKind.Man(5), 3);
            Assert(c.Get(TileKind.Man(5)) == 3, "TileCounter: add 3 of 5m");
            c.Remove(TileKind.Man(5));
            Assert(c.Get(TileKind.Man(5)) == 2, "TileCounter: remove 1 of 5m");
            Assert(c.Total == 2, "TileCounter: total = 2");
        }

        // ==== 화료 판정 ====

        private static void Test_Agari_Standard_Basic()
        {
            // 가장 기본: 1234567899m 11p 22p 33p 같은 모양 대신
            // 정석 핑후: 234m 567m 234p 567p 22s (4면자+1머리는 총 4면자 필요)
            // 다시: 123m 456m 789m 123p 99p (4면자+1머리, 14장)
            var hand = Hand("123456789m 123p 99p");
            Assert(HandAnalyzer.DetectAgariForm(hand) == AgariForm.Standard,
                "Agari: 123456789m 123p 99p → Standard",
                $"got {HandAnalyzer.DetectAgariForm(hand)}, hand={hand}");
        }

        private static void Test_Agari_Standard_AllTriplets()
        {
            // 도이츠: 111m 222m 333m 444m 55m (오직 같은 수트로) — 토이토이 + ...
            // 더 단순하게: 111m 222p 333s 444m 55m
            var hand = Hand("111m 222p 333s 444m 55m");
            Assert(HandAnalyzer.DetectAgariForm(hand) == AgariForm.Standard,
                "Agari: all triplets → Standard");
        }

        private static void Test_Agari_Standard_MixedWithHonors()
        {
            // 동남서북 중 일부 + 수패 면자
            // 111m 234p 567s 1234z (East East East) 5z 5z
            // 동(1z)을 3장 코츠로 빼는 형
            var hand = Hand("123m 456p 789s 111z 55z"); // 동동동 + 백백
            Assert(HandAnalyzer.DetectAgariForm(hand) == AgariForm.Standard,
                "Agari: 順子3개 + 자패 코츠 + 자패 머리 → Standard");
        }

        private static void Test_Agari_Chiitoitsu()
        {
            // 7쌍
            var hand = Hand("11m 33m 55p 77p 22s 99s 11z");
            Assert(HandAnalyzer.DetectAgariForm(hand) == AgariForm.Chiitoitsu,
                "Agari: 7 pairs → Chiitoitsu");
        }

        private static void Test_Agari_Chiitoitsu_FourOfKind_NotAllowed()
        {
            // 일본 룰에서는 같은 패 4장으로 2쌍 만드는 것 불가
            // 1111m 22m 33m 44m 55m 66m 77m → 7쌍처럼 보이지만 4장 1m 때문에 칠대자 아님
            var hand = Hand("1111m 22m 33m 44p 55p 66p 7p"); // 합 14장: 4+2+2+2+2+2+1=15. 다시.
            // 4+2+2+2+2+2 = 14, 6종밖에 안 됨. 따라서 부적합 예시.
            // 진짜 케이스: 1111m 2233m 4455m 66m → 합: 4+2+2+2+2+2 = 14, 6종
            hand = Hand("1111m 2233m 4455m 66m");
            Assert(HandAnalyzer.DetectAgariForm(hand) != AgariForm.Chiitoitsu,
                "Agari: 4-of-kind not allowed as 2 pairs in chiitoitsu");
            // 단, 표준형으로는 화료 가능할 수 있음 (1111이 4장이라 표준형도 어려움)
        }

        private static void Test_Agari_Kokushi_13Way()
        {
            // 1m 9m 1p 9p 1s 9s 동남서북 백발중 + 그 중 1장 중복
            // 13종 모두 + 1z 한장 더
            var hand = Hand("19m 19p 19s 1234z 567z 1z"); // 1z 2장
            Assert(HandAnalyzer.DetectAgariForm(hand) == AgariForm.Kokushi,
                "Agari: 国士無双 (pair on East)",
                $"got {HandAnalyzer.DetectAgariForm(hand)}, hand={hand}");
        }

        private static void Test_Agari_Kokushi_Single()
        {
            // 中(7z)을 머리로
            var hand = Hand("19m 19p 19s 1234z 567z 7z");
            Assert(HandAnalyzer.DetectAgariForm(hand) == AgariForm.Kokushi,
                "Agari: 国士無双 (pair on 中)");
        }

        private static void Test_NotAgari_Cases()
        {
            // 1장 부족
            var hand = Hand("123456789m 123p 9p");
            Assert(HandAnalyzer.DetectAgariForm(hand) == AgariForm.None,
                "NotAgari: 13 tiles → None");

            // 14장이지만 분해 불가
            hand = Hand("12468m 13579p 357s"); // 5+5+3 = 13, 다시
            hand = Hand("12468m 13579p 357s 5z"); // 14장, 분해 불가
            Assert(HandAnalyzer.DetectAgariForm(hand) == AgariForm.None,
                "NotAgari: random 14 tiles → None");
        }

        // ==== 텐파이 ====

        private static void Test_Tenpai_ShanponWait()
        {
            // 샨퐁대기: 두 쌍 중 하나가 코츠로 → 다른 하나가 머리
            // 예: 123m 456p 789s 11z 22z (13장) → 1z 또는 2z 대기
            var hand = Hand("123m 456p 789s 11z 22z");
            var waits = TenpaiAnalyzer.GetWaitingTiles(hand);
            Assert(waits.Count == 2, "Tenpai: shanpon → 2 waits",
                $"got {waits.Count}: [{string.Join(",", waits)}]");
            Assert(waits.Contains(TileKind.East) && waits.Contains(TileKind.South),
                "Tenpai: shanpon waits include both pairs");
        }

        private static void Test_Tenpai_KanchanWait()
        {
            // 칸챤대기: 2 _ 4 의 사이 → 3 대기
            // 13m 가운데에 2m 대기 (= 1m 3m → 2m 칸챤)
            // 풀 핸드: 13m 456p 789s 11s 222s → 13m 사이의 2m 대기
            var hand = Hand("13m 456p 789s 11s 222s");
            var waits = TenpaiAnalyzer.GetWaitingTiles(hand);
            Assert(waits.Count == 1 && waits[0] == TileKind.Man(2),
                "Tenpai: kanchan → wait on 2m",
                $"got [{string.Join(",", waits)}]");
        }

        private static void Test_Tenpai_PenchanWait()
        {
            // 펜챤대기: 12 → 3 대기 (또는 89 → 7)
            var hand = Hand("12m 456p 789s 11s 222s");
            var waits = TenpaiAnalyzer.GetWaitingTiles(hand);
            Assert(waits.Count == 1 && waits[0] == TileKind.Man(3),
                "Tenpai: penchan 12 → wait on 3m",
                $"got [{string.Join(",", waits)}]");
        }

        private static void Test_Tenpai_RyanmenWait()
        {
            // 량멘대기: 34 → 2 또는 5 대기
            var hand = Hand("34m 456p 789s 11s 222s");
            var waits = TenpaiAnalyzer.GetWaitingTiles(hand);
            Assert(waits.Count == 2 &&
                   waits.Contains(TileKind.Man(2)) &&
                   waits.Contains(TileKind.Man(5)),
                "Tenpai: ryanmen 34 → waits 2m & 5m",
                $"got [{string.Join(",", waits)}]");
        }

        private static void Test_Tenpai_TankiWait()
        {
            // 탕키대기(단기): 머리 1장 대기. 4면자 완성 상태 + 1장.
            // 123m 456m 789m 123p 5s → 5s 대기
            var hand = Hand("123456789m 123p 5s");
            var waits = TenpaiAnalyzer.GetWaitingTiles(hand);
            Assert(waits.Count == 1 && waits[0] == TileKind.Sou(5),
                "Tenpai: tanki → wait on pair",
                $"got [{string.Join(",", waits)}]");
        }

        private static void Test_Tenpai_Chiitoitsu()
        {
            // 6쌍 + 1단독 → 그 단독이 짝되는 것이 대기
            var hand = Hand("11m 33m 55p 77p 22s 99s 1z");
            var waits = TenpaiAnalyzer.GetWaitingTiles(hand);
            Assert(waits.Count == 1 && waits[0] == TileKind.East,
                "Tenpai: chiitoitsu 1-wait",
                $"got [{string.Join(",", waits)}]");
        }

        private static void Test_Tenpai_Kokushi_13Wait()
        {
            // 13면 대기: 13요구패 모두 1장씩 → 13종 모두 대기
            var hand = Hand("19m 19p 19s 1234z 567z");
            var waits = TenpaiAnalyzer.GetWaitingTiles(hand);
            Assert(waits.Count == 13,
                "Tenpai: kokushi 13-way wait",
                $"got {waits.Count}");
        }

        private static void Test_NotTenpai()
        {
            // 도통 안 되는 손
            var hand = Hand("147m 258p 369s 1357z"); // 3+3+3+4 = 13
            Assert(!TenpaiAnalyzer.IsTenpai(hand),
                "NotTenpai: random hand");
        }

        // ==== 텐파이 가능한 버림 ====

        private static void Test_TenpaiDiscards()
        {
            // 명백한 1슈텐(1짝 부족): 123m 456p 789s 11s 222s + 5z (14장)
            // 5z 버리면 13장 텐파이
            var hand = Hand("123m 456p 789s 11s 222s 5z");
            var discards = TenpaiAnalyzer.GetTenpaiDiscards(hand);
            Assert(discards.Count >= 1, "TenpaiDiscards: at least one option found");
            Assert(discards.Any(d => d.discard == TileKind.White),
                "TenpaiDiscards: discarding 白 leads to tenpai",
                $"found: [{string.Join(", ", discards.Select(d => d.discard.ToString()))}]");
        }

        // ==== 정렬 ====

        private static void Test_HandSorter()
        {
            var tiles = new List<TileData>
            {
                new TileData(0, TileKind.Red),
                new TileData(1, TileKind.Man(3)),
                new TileData(2, TileKind.Pin(1)),
                new TileData(3, TileKind.Man(1)),
                new TileData(4, TileKind.Sou(9)),
                new TileData(5, TileKind.Man(5), isRedFive: true),
                new TileData(6, TileKind.Man(5)),
            };

            var sorted = HandSorter.Sorted(tiles);

            // 기대 순서: 1m, 3m, 赤5m, 5m, 1p, 9s, 中
            Assert(sorted[0].Kind == TileKind.Man(1), "Sort: 1m first");
            Assert(sorted[1].Kind == TileKind.Man(3), "Sort: 3m second");
            Assert(sorted[2].Kind == TileKind.Man(5) && sorted[2].IsRedFive,
                "Sort: 赤5m before normal 5m");
            Assert(sorted[3].Kind == TileKind.Man(5) && !sorted[3].IsRedFive,
                "Sort: normal 5m after 赤");
            Assert(sorted[4].Kind == TileKind.Pin(1), "Sort: 1p after man");
            Assert(sorted[5].Kind == TileKind.Sou(9), "Sort: 9s after pin");
            Assert(sorted[6].Kind == TileKind.Red, "Sort: 中 last (honors after numbers)");
        }
    }
}

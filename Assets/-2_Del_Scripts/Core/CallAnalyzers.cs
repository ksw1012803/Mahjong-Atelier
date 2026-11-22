using System.Collections.Generic;

namespace MahjongAtelier.Core
{
    /// <summary>
    /// 치(チー) 가능성 분석.
    /// 
    /// 치 룰:
    ///   - 슌츠(연속 3장) 만들기 위해 모자란 1장을 가져옴
    ///   - 카미챠(上家, 왼쪽/이전 차례) 버림패만 가능
    ///   - 수패만 가능 (자패 슌츠 불가)
    /// 
    /// 가능 패턴 예시 (받는 패 = X):
    ///   - X = 5m, 손에 3m4m 있음 → 3-4-5 슌츠
    ///   - X = 5m, 손에 4m6m 있음 → 4-5-6 슌츠
    ///   - X = 5m, 손에 6m7m 있음 → 5-6-7 슌츠
    /// 
    /// 한 패에 여러 슌츠 가능하면 사용자가 선택해야 함.
    /// </summary>
    public static class ChiAnalyzer
    {
        /// <summary>
        /// 치 가능한 슌츠 옵션 모두 반환.
        /// 각 옵션은 (lowTile, highTile) 두 패 — 손패에서 이 두 패와 calledTile을 합쳐 슌츠.
        /// </summary>
        public static List<ChiOption> FindOptions(MahjongPlayer caller, TileData calledTile)
        {
            var options = new List<ChiOption>();
            if (caller == null || calledTile == null) return options;
            if (!calledTile.Kind.Suit.IsNumber()) return options;

            int n = calledTile.Kind.Number;
            TileSuit suit = calledTile.Kind.Suit;

            // 손패에서 같은 수트의 패 인덱스별 카운트
            int countOf(int num)
            {
                if (num < 1 || num > 9) return 0;
                int c = 0;
                foreach (var t in caller.HandTiles)
                    if (t.Kind.Suit == suit && t.Kind.Number == num) c++;
                return c;
            }

            TileData findInHand(int num)
            {
                foreach (var t in caller.HandTiles)
                    if (t.Kind.Suit == suit && t.Kind.Number == num) return t;
                return null;
            }

            // 패턴 1: X가 슌츠의 가장 큰 수 (X-2, X-1, X)
            if (n >= 3 && countOf(n - 2) > 0 && countOf(n - 1) > 0)
            {
                options.Add(new ChiOption
                {
                    SequenceStart = new TileKind(suit, n - 2),
                    LowTile = findInHand(n - 2),
                    MiddleTile = findInHand(n - 1),
                    HighTile = calledTile
                });
            }

            // 패턴 2: X가 슌츠의 중간 (X-1, X, X+1)
            if (n >= 2 && n <= 8 && countOf(n - 1) > 0 && countOf(n + 1) > 0)
            {
                options.Add(new ChiOption
                {
                    SequenceStart = new TileKind(suit, n - 1),
                    LowTile = findInHand(n - 1),
                    MiddleTile = calledTile,
                    HighTile = findInHand(n + 1)
                });
            }

            // 패턴 3: X가 슌츠의 가장 작은 수 (X, X+1, X+2)
            if (n <= 7 && countOf(n + 1) > 0 && countOf(n + 2) > 0)
            {
                options.Add(new ChiOption
                {
                    SequenceStart = new TileKind(suit, n),
                    LowTile = calledTile,
                    MiddleTile = findInHand(n + 1),
                    HighTile = findInHand(n + 2)
                });
            }

            return options;
        }
    }

    /// <summary>치 옵션 한 가지.</summary>
    public sealed class ChiOption
    {
        public TileKind SequenceStart;      // 슌츠의 시작 패 (1~7)
        public TileData LowTile;            // 작은 패
        public TileData MiddleTile;         // 중간 패
        public TileData HighTile;           // 큰 패
    }

    /// <summary>
    /// 펑(ポン) 가능성 분석.
    /// 펑은 같은 패 2장이 손에 있을 때 1장을 가져와 코츠 만듦.
    /// </summary>
    public static class PonAnalyzer
    {
        public static bool CanPon(MahjongPlayer caller, TileData calledTile)
        {
            if (caller == null || calledTile == null) return false;
            int count = 0;
            foreach (var t in caller.HandTiles)
                if (t.Kind == calledTile.Kind) count++;
            return count >= 2;
        }

        /// <summary>펑 시 사용할 손패 2장 반환.</summary>
        public static List<TileData> FindPonTiles(MahjongPlayer caller, TileKind kind)
        {
            var result = new List<TileData>();
            foreach (var t in caller.HandTiles)
            {
                if (t.Kind == kind) result.Add(t);
                if (result.Count >= 2) break;
            }
            return result;
        }
    }

    /// <summary>
    /// 깡(カン) 가능성 분석.
    /// 
    /// 3종류:
    ///   - 명깡(大明槓): 다른 사람 버림패로 만듦. 손에 3장 + 버림패 1장.
    ///   - 안깡(暗槓): 자기 차례에 손에 4장 모은 상태에서 선언.
    ///   - 가깡(加槓): 펑한 코츠에 같은 패를 자기 차례에 추가.
    /// </summary>
    public static class KanAnalyzer
    {
        /// <summary>명깡 가능? (다른 사람이 버린 패에 대해)</summary>
        public static bool CanDaiminkan(MahjongPlayer caller, TileData calledTile)
        {
            if (caller == null || calledTile == null) return false;
            int count = 0;
            foreach (var t in caller.HandTiles)
                if (t.Kind == calledTile.Kind) count++;
            return count >= 3;
        }

        /// <summary>안깡 가능한 패 종류 목록. 자기 차례에 자기 손패 검사.</summary>
        public static List<TileKind> FindAnkanKinds(MahjongPlayer caller)
        {
            var result = new List<TileKind>();
            if (caller == null) return result;

            var counts = new Dictionary<TileKind, int>();
            foreach (var t in caller.HandTiles)
            {
                counts.TryGetValue(t.Kind, out int c);
                counts[t.Kind] = c + 1;
            }
            foreach (var kv in counts)
            {
                if (kv.Value >= 4) result.Add(kv.Key);
            }
            return result;
        }

        /// <summary>
        /// 가깡 가능한 패 종류 목록.
        /// 펑한 코츠가 있고, 같은 패를 손에 가지고 있을 때.
        /// </summary>
        public static List<TileKind> FindShouminkanKinds(MahjongPlayer caller)
        {
            var result = new List<TileKind>();
            if (caller == null) return result;

            // 손패의 각 패 종류 카운트
            var handCounts = new Dictionary<TileKind, int>();
            foreach (var t in caller.HandTiles)
            {
                handCounts.TryGetValue(t.Kind, out int c);
                handCounts[t.Kind] = c + 1;
            }

            // 펑한 코츠 중에 손에 그 패가 있는 것
            foreach (var meld in caller.CalledMeldsExt)
            {
                if (meld.CallType != CallType.Pon) continue;
                if (handCounts.TryGetValue(meld.BaseKind, out int handCount) && handCount >= 1)
                {
                    result.Add(meld.BaseKind);
                }
            }
            return result;
        }
    }
}

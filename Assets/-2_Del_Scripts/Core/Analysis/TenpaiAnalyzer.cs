using System.Collections.Generic;

namespace MahjongAtelier.Core
{
    /// <summary>
    /// 텐파이(聽牌) 판정 및 대기패(待ち牌) 검출.
    /// 
    /// 텐파이: 1장만 더 있으면 화료할 수 있는 상태 (13장).
    /// 대기패: 어떤 패가 와야 화료가 되는지의 패 종류 목록.
    /// 
    /// 구현 방식:
    ///   34종의 패를 하나씩 가상으로 추가 → HandAnalyzer.IsAgari로 확인.
    ///   단순하지만 충분히 빠릅니다 (34회 × HandAnalyzer 1회).
    /// </summary>
    public static class TenpaiAnalyzer
    {
        /// <summary>13장 손패가 텐파이인지.</summary>
        public static bool IsTenpai(TileCounter hand)
        {
            return GetWaitingTiles(hand).Count > 0;
        }

        /// <summary>
        /// 대기패 목록을 반환.
        /// 단, "4장 모두 자신이 가지고 있는 패"는 산패(山牌)에 없으므로 실질 대기 아님.
        /// 그러나 룰 상 카라텐(空聽: 형식적 텐파이)도 텐파이로 인정되므로 포함합니다.
        /// (호출 측에서 필요 시 필터링)
        /// </summary>
        public static List<TileKind> GetWaitingTiles(TileCounter hand)
        {
            var result = new List<TileKind>();

            if (hand.Total != 13) return result;

            for (int i = 0; i < TileCounter.Size; i++)
            {
                // 이미 4장 가진 패는 추가 불가 (총 4장 제약)
                if (hand[i] >= 4) continue;

                hand[i]++;
                bool agari = HandAnalyzer.IsAgari(hand);
                hand[i]--;

                if (agari) result.Add(TileCounter.KindOf(i));
            }

            return result;
        }

        /// <summary>
        /// 14장 손패에서 "어떤 패를 버리면 텐파이가 되는가"를 검출.
        /// 리치 선언 시점, AI 결정에 사용.
        /// 반환: (버릴 패 종류, 그때의 대기패 목록).
        /// </summary>
        public static List<(TileKind discard, List<TileKind> waits)> GetTenpaiDiscards(TileCounter hand)
        {
            var result = new List<(TileKind, List<TileKind>)>();

            if (hand.Total != 14) return result;

            // 손패에 있는 종류만 후보
            for (int i = 0; i < TileCounter.Size; i++)
            {
                if (hand[i] == 0) continue;

                hand[i]--;
                var waits = GetWaitingTiles(hand);
                hand[i]++;

                if (waits.Count > 0)
                    result.Add((TileCounter.KindOf(i), waits));
            }

            return result;
        }
    }
}

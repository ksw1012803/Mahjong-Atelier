using System.Collections.Generic;
using UnityEngine;

namespace CardGame.Core
{
    /// <summary>
    /// 가중치 기반 봇 전략. Inspector에서 밸런싱 가능.
    /// 다른 가중치로 Standard / Aggressive / Defensive 에셋 생성.
    /// </summary>
    [CreateAssetMenu(menuName = "SevenMaid/Bot Strategy/Weighted", fileName = "Bot_")]
    public class WeightedBotStrategy : BotStrategy
    {
        [Header("일반 카드 가중치")]
        [Tooltip("같은 색 N장일 때의 유용성 점수")]
        public float sameColor_0 = 0f;
        public float sameColor_1 = 2f;
        public float sameColor_2 = 5f;
        public float sameColor_3plus = 10f;

        [Space]
        public float sameNumber_0 = 0f;
        public float sameNumber_1 = 3f;
        public float sameNumber_2 = 8f;
        public float sameNumber_3plus = 12f;

        [Header("7연속 보너스")]
        public float adjacentBonus = 1.5f;
        public float nearAdjacentBonus = 0.5f;

        [Header("특수 카드 가치")]
        public float wildValue = 15f;
        public float peekValue = 7f;
        public float swapValue = 6f;
        public float chaosValue = 4f;
        public float defaultSpecialValue = 1f;

        [Header("상대 회피")]
        [Tooltip("상대가 그 색을 25% 미만 버렸을 때의 감점")]
        public float colorAvoidancePenalty = 1.5f;

        [Tooltip("상대가 그 숫자를 10% 미만 버렸을 때의 감점")]
        public float numberAvoidancePenalty = 1f;

        [Header("Debug")]
        public bool verbose = false;

        public override CardData ChooseDiscard(Player self, IReadOnlyList<Player> allPlayers, BotMemory memory)
        {
            if (self.Hand.Count == 0) return null;
            if (self.Hand.Count == 1) return self.Hand[0];

            CardData worst = null;
            float worstScore = float.MaxValue;

            foreach (var card in self.Hand)
            {
                float score = CalculateUsefulness(card, self.Hand, self.Index, memory);
                if (verbose) Debug.Log($"  {card}: {score:F1}");
                if (score < worstScore) { worstScore = score; worst = card; }
            }

            return worst;
        }

        private float CalculateUsefulness(CardData card, List<CardData> hand, int selfIndex, BotMemory memory)
        {
            if (card.Kind.IsSpecial)
                return card.Kind.Special switch
                {
                    SpecialCardType.Wild  => wildValue,
                    SpecialCardType.Peek  => peekValue,
                    SpecialCardType.Swap  => swapValue,
                    SpecialCardType.Chaos => chaosValue,
                    _ => defaultSpecialValue
                };

            int sameColor = 0, sameNumber = 0;
            foreach (var other in hand)
            {
                if (other == card || other.Kind.IsSpecial) continue;
                if (other.Kind.Color == card.Kind.Color) sameColor++;
                if (other.Kind.Number == card.Kind.Number) sameNumber++;
            }

            float score = 0f;
            score += sameColor switch { 0 => sameColor_0, 1 => sameColor_1, 2 => sameColor_2, _ => sameColor_3plus };
            score += sameNumber switch { 0 => sameNumber_0, 1 => sameNumber_1, 2 => sameNumber_2, _ => sameNumber_3plus };

            // 연속 숫자 보너스
            foreach (var other in hand)
            {
                if (other == card || other.Kind.IsSpecial) continue;
                if (other.Kind.Color != card.Kind.Color) continue;
                int diff = Mathf.Abs(other.Kind.Number - card.Kind.Number);
                if (diff == 1) score += adjacentBonus;
                else if (diff == 2) score += nearAdjacentBonus;
            }

            // 상대 회피
            if (memory != null)
                score -= memory.GetAvoidanceScore(card, selfIndex, colorAvoidancePenalty, numberAvoidancePenalty);

            return score;
        }
    }
}

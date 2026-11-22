using System.Collections.Generic;
using UnityEngine;

namespace CardGame.Core
{
    /// <summary>
    /// 봇 AI 오케스트레이터. 전략(SO) 받아서 실행.
    /// 기본 생성자: 전략 없이 fallback 동작 (뽑은 카드 그대로 버림).
    /// </summary>
    public class BotAI
    {
        private readonly BotStrategy _strategy;
        private readonly BotMemory _memory;

        /// <summary>전략 없이 생성 (fallback: 뽑은 카드 버림).</summary>
        public BotAI() : this(null) { }

        public BotAI(BotStrategy strategy)
        {
            _strategy = strategy;
            _memory = new BotMemory();
        }

        public void OnCardDiscarded(int playerIndex, CardData card) => _memory.RecordDiscard(playerIndex, card);
        public void OnRoundStart() => _memory.Clear();

        public CardData ChooseDiscard(Player self, IReadOnlyList<Player> allPlayers)
        {
            if (self == null || self.Hand == null || self.Hand.Count == 0) return null;

            // 전략 없으면 fallback: 첫 카드 버림
            if (_strategy == null)
            {
                Debug.LogWarning("[BotAI] Strategy 없음, fallback 사용");
                return self.Hand[0];
            }

            return _strategy.ChooseDiscard(self, allPlayers, _memory);
        }
    }
}
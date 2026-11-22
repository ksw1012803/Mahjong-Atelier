using System.Collections.Generic;

namespace CardGame.Core
{
    /// <summary>
    /// 봇의 메모리: 다른 플레이어 버림패 추적.
    /// </summary>
    public class BotMemory
    {
        private readonly Dictionary<int, List<CardData>> _discardHistory = new Dictionary<int, List<CardData>>();

        public void RecordDiscard(int playerIndex, CardData card)
        {
            if (!_discardHistory.ContainsKey(playerIndex))
                _discardHistory[playerIndex] = new List<CardData>();
            _discardHistory[playerIndex].Add(card);
        }

        public void Clear() => _discardHistory.Clear();

        public float GetAvoidanceScore(CardData card, int selfIndex, float colorPenalty, float numberPenalty)
        {
            if (card.Kind.IsSpecial) return 0f;

            float total = 0f;
            foreach (var kv in _discardHistory)
            {
                if (kv.Key == selfIndex) continue;
                var discards = kv.Value;
                if (discards.Count < 2) continue;

                int sameColorDiscarded = 0, sameNumberDiscarded = 0;
                foreach (var d in discards)
                {
                    if (d.Kind.IsSpecial) continue;
                    if (d.Kind.Color == card.Kind.Color) sameColorDiscarded++;
                    if (d.Kind.Number == card.Kind.Number) sameNumberDiscarded++;
                }

                float colorRatio = (float)sameColorDiscarded / discards.Count;
                float numberRatio = (float)sameNumberDiscarded / discards.Count;

                if (colorRatio < 0.25f) total += colorPenalty;
                if (numberRatio < 0.1f) total += numberPenalty;
            }
            return total;
        }
    }
}

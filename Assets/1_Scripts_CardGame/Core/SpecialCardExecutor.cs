using System.Collections.Generic;
using UnityEngine;

namespace CardGame.Core
{
    /// <summary>
    /// 특수 카드 효과 실행자.
    /// 
    /// 각 효과는 즉시 실행 후 결과 반환. 
    /// UI 선택이 필요한 경우 (Swap 상대 지정, Peek 카드 선택)는
    /// CardGameManager가 phase를 WaitingSpecialAction으로 전환하여 처리.
    /// </summary>
    public static class SpecialCardExecutor
    {
        /// <summary>
        /// Swap: 두 플레이어 간 카드 교환.
        /// </summary>
        public static bool ExecuteSwap(
            Player source, Player target,
            CardData sourceCard, CardData targetCard)
        {
            if (source == null || target == null) return false;
            if (!source.Hand.Contains(sourceCard)) return false;
            if (!target.Hand.Contains(targetCard)) return false;

            source.RemoveCard(sourceCard);
            target.RemoveCard(targetCard);
            source.AddCard(targetCard);
            target.AddCard(sourceCard);

            Debug.Log($"[Swap] P{source.Index}의 {sourceCard} ↔ P{target.Index}의 {targetCard}");
            return true;
        }

        /// <summary>
        /// Swap 봇용 자동 실행 (랜덤 상대, 랜덤 카드).
        /// </summary>
        public static void ExecuteSwapAuto(
            Player source, IReadOnlyList<Player> allPlayers, System.Random rng)
        {
            var candidates = new List<Player>();
            foreach (var p in allPlayers)
                if (p.Index != source.Index && p.Hand.Count > 0) candidates.Add(p);

            if (candidates.Count == 0 || source.Hand.Count == 0) return;

            var target = candidates[rng.Next(candidates.Count)];
            var sourceCard = source.Hand[rng.Next(source.Hand.Count)];
            var targetCard = target.Hand[rng.Next(target.Hand.Count)];
            ExecuteSwap(source, target, sourceCard, targetCard);
        }

        /// <summary>
        /// Peek: 덱 위 3장 확인.
        /// </summary>
        public static List<CardData> PeekTop(Deck deck, int count = 3)
        {
            return deck.PeekTop(count);
        }

        /// <summary>
        /// Peek 결과에서 1장 선택해 손패에 추가.
        /// </summary>
        public static bool ExecutePeekChoice(
            Player source, Deck deck, CardData chosenCard)
        {
            if (source == null || deck == null || chosenCard == null) return false;
            if (!deck.RemoveFromDeck(chosenCard)) return false;
            source.AddCard(chosenCard);
            Debug.Log($"[Peek] P{source.Index} 선택: {chosenCard}");
            return true;
        }

        /// <summary>
        /// Peek 봇용 자동 실행: 상위 3장 중 첫 번째 선택.
        /// </summary>
        public static CardData ExecutePeekAuto(Player source, Deck deck)
        {
            var top = deck.PeekTop(3);
            if (top.Count == 0) return null;
            var chosen = top[0];
            ExecutePeekChoice(source, deck, chosen);
            return chosen;
        }

        /// <summary>
        /// Chaos: 모든 플레이어가 손패 1장을 왼쪽(index+1)으로 넘김.
        /// 넘기는 카드는 각자 자동 선택 (봇은 첫 카드, 사용자도 임의 - 간단화).
        /// 
        /// 간단화: 각 플레이어의 첫 번째 카드가 넘어감. 실제 게임에선 선택 UI 가능하지만 이번은 자동.
        /// </summary>
        public static void ExecuteChaos(IReadOnlyList<Player> allPlayers)
        {
            if (allPlayers == null || allPlayers.Count < 2) return;

            // 각 플레이어가 넘길 카드를 미리 뽑음
            var handedOff = new List<CardData>();
            foreach (var p in allPlayers)
            {
                if (p.Hand.Count > 0)
                {
                    var toGive = p.Hand[0];
                    handedOff.Add(toGive);
                }
                else handedOff.Add(null);
            }

            // 실제로 옮김 (i의 카드가 i+1로)
            for (int i = 0; i < allPlayers.Count; i++)
            {
                var card = handedOff[i];
                if (card == null) continue;
                var giver = allPlayers[i];
                var receiver = allPlayers[(i + 1) % allPlayers.Count];
                giver.RemoveCard(card);
                receiver.AddCard(card);
                Debug.Log($"[Chaos] P{giver.Index} → P{receiver.Index}: {card}");
            }
        }
    }
}

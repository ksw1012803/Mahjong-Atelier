using System.Collections.Generic;
using UnityEngine;

namespace CardGame.Core
{
    /// <summary>
    /// 봇 AI 전략 베이스 (ScriptableObject).
    /// 다양한 전략/난이도를 에셋으로 생성해서 Inspector에서 할당.
    /// </summary>
    public abstract class BotStrategy : ScriptableObject
    {
        [Header("Metadata")]
        public string strategyName = "Default";
        [TextArea(2, 4)] public string description;

        /// <summary>버릴 카드 결정.</summary>
        public abstract CardData ChooseDiscard(Player self, IReadOnlyList<Player> allPlayers, BotMemory memory);
    }
}

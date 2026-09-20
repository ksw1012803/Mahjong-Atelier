using System.Collections.Generic;

namespace CardGame.Core
{
    public enum PlayerType
    {
        Human,
        Bot
    }

    /// <summary>
    /// 카드게임 플레이어.
    /// 
    /// 손패 7장. 마작 플레이어의 축소 버전.
    /// 후로/리치 개념 없음. 단순.
    /// </summary>
    public sealed class Player
    {
        public int Index { get; }
        public PlayerType Type { get; set; }

        public bool IsBot => Type == PlayerType.Bot;
        public bool IsHuman => Type == PlayerType.Human;

        /// <summary>손패. 기본 7장. 뽑기 직후 일시적으로 8장이 됨.</summary>
        public List<CardData> Hand { get; } = new List<CardData>();

        /// <summary>누적 점수 (게임 전체).</summary>
        public int TotalScore { get; set; }

        public Player(int index, PlayerType type = PlayerType.Human)
        {
            Index = index;
            Type = type;
        }

        public void AddCard(CardData card)
        {
            if (card != null) Hand.Add(card);
        }

        public bool RemoveCard(CardData card)
        {
            return Hand.Remove(card);
        }

        /// <summary>라운드 시작 시 손패 초기화.</summary>
        public void ClearHand()
        {
            Hand.Clear();
        }

        /// <summary>게임 시작 시 완전 리셋.</summary>
        public void ResetAll()
        {
            Hand.Clear();
            TotalScore = 0;
        }

        public override string ToString() =>
            $"P{Index}({Type}): {Hand.Count}장, {TotalScore}점";
    }
}

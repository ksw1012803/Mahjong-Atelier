using System.Collections.Generic;

namespace CardGame.Core
{
    /// <summary>
    /// 플레이어 타입.
    ///   Human       — 이 기기에서 조작하는 사람 (로컬)
    ///   Bot         — AI
    ///   RemoteHuman — 온라인 상대 (호스트 입장에서 "입력을 네트워크로 기다려야 하는 사람")
    /// </summary>
    public enum PlayerType { Human, Bot, RemoteHuman }

    /// <summary>
    /// 카드게임 플레이어 v6 — 온라인 지원.
    ///
    /// v6 변경:
    ///   - PlayerType.RemoteHuman 추가
    ///   - IsHuman = Human 또는 RemoteHuman (봇이 아닌 모든 사람)
    ///   - IsLocalHuman / IsRemoteHuman 구분 프로퍼티
    ///   - DisplayName (Steam 닉네임 등)
    ///   - FindCard(instanceId)
    /// </summary>
    public sealed class Player
    {
        public int Index { get; }
        public PlayerType Type { get; set; }
        public bool IsBot => Type == PlayerType.Bot;
        public bool IsHuman => Type != PlayerType.Bot;
        public bool IsLocalHuman => Type == PlayerType.Human;
        public bool IsRemoteHuman => Type == PlayerType.RemoteHuman;

        public string DisplayName { get; set; }

        public List<CardData> Hand { get; } = new List<CardData>();
        public int TotalScore { get; set; }
        public CompletionPattern LastRoundPattern { get; set; } = CompletionPattern.None;

        public Player(int index, PlayerType type = PlayerType.Human)
        {
            Index = index;
            Type = type;
            DisplayName = type == PlayerType.Bot ? $"BOT {index + 1}" : $"P{index + 1}";
        }

        public void AddCard(CardData card)
        {
            if (card == null) return;
            Hand.Add(card);
            CardGameEvents.RaiseHandChanged(Index);
        }

        public bool RemoveCard(CardData card)
        {
            bool removed = Hand.Remove(card);
            if (removed) CardGameEvents.RaiseHandChanged(Index);
            return removed;
        }

        /// <summary>InstanceId로 손패 검색 (온라인 명령 해석용).</summary>
        public CardData FindCard(int instanceId)
        {
            for (int i = 0; i < Hand.Count; i++)
                if (Hand[i] != null && Hand[i].InstanceId == instanceId) return Hand[i];
            return null;
        }

        /// <summary>
        /// 손패 안에서 카드의 순서를 변경 (드래그 재정렬용).
        /// 완성 판정은 순서 무관하므로 게임 로직에 영향 없음.
        /// HandChanged 이벤트를 발생시키지 않음 (UI가 호출한 쪽에서 직접 재구성).
        /// </summary>
        public void ReorderCard(CardData card, int newIndex)
        {
            if (card == null || !Hand.Contains(card)) return;

            Hand.Remove(card);

            if (newIndex < 0) newIndex = 0;
            if (newIndex > Hand.Count) newIndex = Hand.Count;

            Hand.Insert(newIndex, card);
        }

        public void ClearHand()
        {
            Hand.Clear();
            LastRoundPattern = CompletionPattern.None;
            CardGameEvents.RaiseHandChanged(Index);
        }

        public void ResetAll()
        {
            Hand.Clear();
            TotalScore = 0;
            LastRoundPattern = CompletionPattern.None;
            CardGameEvents.RaiseHandChanged(Index);
        }

        public override string ToString() =>
            $"P{Index}({Type}:{DisplayName}): {Hand.Count}장, {TotalScore}점";
    }
}

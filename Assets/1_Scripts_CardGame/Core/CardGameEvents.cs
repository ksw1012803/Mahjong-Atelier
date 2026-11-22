using System;
using System.Collections.Generic;

namespace CardGame.Core
{
    public enum GamePhase
    {
        NotStarted,
        Dealing,
        WaitingDraw,
        WaitingDiscard,
        WaitingSpecialAction,   // ★ 특수 카드 발동 후 사용자 입력 대기
        RoundFinished,
        GameFinished
    }

    /// <summary>
    /// 특수 카드 발동 이벤트 정보.
    /// </summary>
    public sealed class SpecialCardEvent
    {
        public int SourcePlayerIndex { get; set; }
        public SpecialCardType Type { get; set; }
        public int TargetPlayerIndex { get; set; } = -1;
        public CardData ChosenCard { get; set; }
    }

    public static class CardGameEvents
    {
        public static event Action<GamePhase, GamePhase> OnPhaseChanged;
        public static void RaisePhaseChanged(GamePhase prev, GamePhase next) =>
            OnPhaseChanged?.Invoke(prev, next);

        public static event Action OnDeckCreated;
        public static void RaiseDeckCreated() => OnDeckCreated?.Invoke();

        public static event Action<int> OnDeckCountChanged;
        public static void RaiseDeckCountChanged(int remaining) =>
            OnDeckCountChanged?.Invoke(remaining);

        public static event Action OnDealStarted;
        public static event Action OnDealCompleted;
        public static void RaiseDealStarted() => OnDealStarted?.Invoke();
        public static void RaiseDealCompleted() => OnDealCompleted?.Invoke();

        public static event Action<int> OnTurnStarted;
        public static void RaiseTurnStarted(int playerIndex) =>
            OnTurnStarted?.Invoke(playerIndex);

        public static event Action<int, CardData> OnCardDrawn;
        public static void RaiseCardDrawn(int playerIndex, CardData card) =>
            OnCardDrawn?.Invoke(playerIndex, card);

        public static event Action<int, CardData> OnCardDiscarded;
        public static void RaiseCardDiscarded(int playerIndex, CardData card) =>
            OnCardDiscarded?.Invoke(playerIndex, card);

        public static event Action<int> OnHandChanged;
        public static void RaiseHandChanged(int playerIndex) =>
            OnHandChanged?.Invoke(playerIndex);

        public static event Action<int, CompletionResult> OnCompletion;
        public static void RaiseCompletion(int playerIndex, CompletionResult result) =>
            OnCompletion?.Invoke(playerIndex, result);

        public static event Action<int> OnGameFinished;
        public static void RaiseGameFinished(int winnerIndex) =>
            OnGameFinished?.Invoke(winnerIndex);

        // === 특수 카드 이벤트 ===

        /// <summary>특수 카드가 발동됐음 (효과 발동 직전).</summary>
        public static event Action<SpecialCardEvent> OnSpecialCardTriggered;
        public static void RaiseSpecialCardTriggered(SpecialCardEvent ev) =>
            OnSpecialCardTriggered?.Invoke(ev);

        /// <summary>특수 카드 효과 완료 (턴 진행 재개).</summary>
        public static event Action<SpecialCardEvent> OnSpecialCardCompleted;
        public static void RaiseSpecialCardCompleted(SpecialCardEvent ev) =>
            OnSpecialCardCompleted?.Invoke(ev);

        public static void Clear()
        {
            OnPhaseChanged = null;
            OnDeckCreated = null;
            OnDeckCountChanged = null;
            OnDealStarted = null;
            OnDealCompleted = null;
            OnTurnStarted = null;
            OnCardDrawn = null;
            OnCardDiscarded = null;
            OnHandChanged = null;
            OnCompletion = null;
            OnGameFinished = null;
            OnSpecialCardTriggered = null;
            OnSpecialCardCompleted = null;
        }
    }
}

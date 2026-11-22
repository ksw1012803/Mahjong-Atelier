using System;
using System.Collections.Generic;

namespace MahjongAtelier.Core
{
    /// <summary>
    /// 이벤트 버스 v5 — 후로 이벤트 추가.
    /// </summary>
    public static class GameEvents
    {
        // 기존 이벤트들
        public static event Action<GamePhase, GamePhase> OnPhaseChanged;
        public static void RaisePhaseChanged(GamePhase prev, GamePhase next) => OnPhaseChanged?.Invoke(prev, next);

        public static event Action OnWallCreated;
        public static void RaiseWallCreated() => OnWallCreated?.Invoke();

        public static event Action<int> OnWallCountChanged;
        public static void RaiseWallCountChanged(int remaining) => OnWallCountChanged?.Invoke(remaining);

        public static event Action OnDealStarted;
        public static event Action OnDealCompleted;
        public static void RaiseDealStarted() => OnDealStarted?.Invoke();
        public static void RaiseDealCompleted() => OnDealCompleted?.Invoke();

        public static event Action<int> OnTurnStarted;
        public static void RaiseTurnStarted(int playerIndex) => OnTurnStarted?.Invoke(playerIndex);

        public static event Action<int, TileData> OnTileDrawn;
        public static void RaiseTileDrawn(int playerIndex, TileData tile) => OnTileDrawn?.Invoke(playerIndex, tile);

        public static event Action<int, TileData> OnTileDiscarded;
        public static void RaiseTileDiscarded(int playerIndex, TileData tile) => OnTileDiscarded?.Invoke(playerIndex, tile);

        public static event Action<int> OnHandChanged;
        public static void RaiseHandChanged(int playerIndex) => OnHandChanged?.Invoke(playerIndex);

        public static event Action<int, AgariForm> OnAgari;
        public static void RaiseAgari(int playerIndex, AgariForm form) => OnAgari?.Invoke(playerIndex, form);

        public static event Action<int> OnRiichiDeclared;
        public static void RaiseRiichiDeclared(int playerIndex) => OnRiichiDeclared?.Invoke(playerIndex);

        public static event Action<int, RiichiAnalysis> OnRiichiAvailabilityChanged;
        public static void RaiseRiichiAvailabilityChanged(int playerIndex, RiichiAnalysis analysis) =>
            OnRiichiAvailabilityChanged?.Invoke(playerIndex, analysis);

        public static event Action<TileData, int, List<RonCandidate>> OnRonOpportunity;
        public static void RaiseRonOpportunity(TileData discardedTile, int discarderIndex, List<RonCandidate> candidates) =>
            OnRonOpportunity?.Invoke(discardedTile, discarderIndex, candidates);

        public static event Action OnRonOpportunityClosed;
        public static void RaiseRonOpportunityClosed() => OnRonOpportunityClosed?.Invoke();

        // === 후로 ===

        /// <summary>
        /// 누가 패를 버린 직후, 사용자가 치/펑/명깡 가능한지 알림.
        /// (모두 분석한 결과를 한 번에 전달)
        /// </summary>
        public static event Action<CallOpportunity> OnCallOpportunity;
        public static void RaiseCallOpportunity(CallOpportunity opp) => OnCallOpportunity?.Invoke(opp);

        public static event Action OnCallOpportunityClosed;
        public static void RaiseCallOpportunityClosed() => OnCallOpportunityClosed?.Invoke();

        /// <summary>자기 차례에 안깡 또는 가깡 가능한지 알림.</summary>
        public static event Action<List<TileKind>, List<TileKind>> OnSelfKanAvailability;
        public static void RaiseSelfKanAvailability(List<TileKind> ankanKinds, List<TileKind> shouminkanKinds) =>
            OnSelfKanAvailability?.Invoke(ankanKinds, shouminkanKinds);

        /// <summary>후로 선언됨 (시각 갱신용).</summary>
        public static event Action<int, CalledMeld> OnCallDeclared;
        public static void RaiseCallDeclared(int playerIndex, CalledMeld meld) =>
            OnCallDeclared?.Invoke(playerIndex, meld);

        public static void Clear()
        {
            OnPhaseChanged = null;
            OnWallCreated = null;
            OnWallCountChanged = null;
            OnDealStarted = null;
            OnDealCompleted = null;
            OnTurnStarted = null;
            OnTileDrawn = null;
            OnTileDiscarded = null;
            OnHandChanged = null;
            OnAgari = null;
            OnRiichiDeclared = null;
            OnRiichiAvailabilityChanged = null;
            OnRonOpportunity = null;
            OnRonOpportunityClosed = null;
            OnCallOpportunity = null;
            OnCallOpportunityClosed = null;
            OnSelfKanAvailability = null;
            OnCallDeclared = null;
        }
    }

    /// <summary>
    /// 사용자에게 전달할 후로 기회 정보.
    /// </summary>
    public sealed class CallOpportunity
    {
        public TileData DiscardedTile;
        public int DiscarderIndex;

        public bool CanChi;
        public List<ChiOption> ChiOptions = new List<ChiOption>();

        public bool CanPon;
        public bool CanDaiminkan;

        public bool HasAny => CanChi || CanPon || CanDaiminkan;
    }
}

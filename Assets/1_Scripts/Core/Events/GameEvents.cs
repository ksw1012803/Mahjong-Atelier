using System;

namespace MahjongAtelier.Core
{
    /// <summary>
    /// 게임 코어 ↔ UI/네트워크/AI 사이의 이벤트 버스.
    /// 
    /// 코어 로직(MahjongGameManager 등)은 이벤트를 발생만 시키고,
    /// UI/AI/네트워크 모듈은 이벤트를 구독해서 반응합니다.
    /// 
    /// 이렇게 분리하면:
    ///   - 코어를 Unity 의존 없이 단위 테스트 가능
    ///   - 4인용/네트워크 확장 시 코어 수정 최소화
    ///   - 같은 이벤트에 UI도 반응하고 사운드도 반응하는 등 멀티 구독 자연스러움
    /// 
    /// 사용 예:
    ///   GameEvents.OnTileDrawn += (p, tile) => handUI.AddTile(tile);
    ///   GameEvents.OnTileDrawn += (p, tile) => soundSystem.PlayDraw();
    /// </summary>
    public static class GameEvents
    {
        // === 게임 상태 ===

        /// <summary>게임 단계 전환. (이전, 새로운)</summary>
        public static event Action<GamePhase, GamePhase> OnPhaseChanged;

        public static void RaisePhaseChanged(GamePhase prev, GamePhase next) =>
            OnPhaseChanged?.Invoke(prev, next);

        // === 패산 ===

        /// <summary>패산 생성 완료.</summary>
        public static event Action OnWallCreated;
        public static void RaiseWallCreated() => OnWallCreated?.Invoke();

        /// <summary>패산 남은 수 변동.</summary>
        public static event Action<int> OnWallCountChanged;
        public static void RaiseWallCountChanged(int remaining) =>
            OnWallCountChanged?.Invoke(remaining);

        // === 배패 ===

        public static event Action OnDealStarted;
        public static event Action OnDealCompleted;
        public static void RaiseDealStarted() => OnDealStarted?.Invoke();
        public static void RaiseDealCompleted() => OnDealCompleted?.Invoke();

        // === 턴/액션 ===

        /// <summary>플레이어 차례 시작. (플레이어 인덱스)</summary>
        public static event Action<int> OnTurnStarted;
        public static void RaiseTurnStarted(int playerIndex) =>
            OnTurnStarted?.Invoke(playerIndex);

        /// <summary>플레이어가 패를 쯔모(자가패산). (플레이어, 쯔모한 패)</summary>
        public static event Action<int, TileData> OnTileDrawn;
        public static void RaiseTileDrawn(int playerIndex, TileData tile) =>
            OnTileDrawn?.Invoke(playerIndex, tile);

        /// <summary>플레이어가 패를 버림. (플레이어, 버린 패)</summary>
        public static event Action<int, TileData> OnTileDiscarded;
        public static void RaiseTileDiscarded(int playerIndex, TileData tile) =>
            OnTileDiscarded?.Invoke(playerIndex, tile);

        // === 손패 변경 (UI 갱신용) ===

        public static event Action<int> OnHandChanged;
        public static void RaiseHandChanged(int playerIndex) =>
            OnHandChanged?.Invoke(playerIndex);

        // === 화료 ===

        public static event Action<int, AgariForm> OnAgari;
        public static void RaiseAgari(int playerIndex, AgariForm form) =>
            OnAgari?.Invoke(playerIndex, form);

        // === 디버그 ===

        /// <summary>모든 구독 해제. 씬 전환 시 호출 권장.</summary>
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
        }
    }
}

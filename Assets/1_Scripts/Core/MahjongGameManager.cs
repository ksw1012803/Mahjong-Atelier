using System.Collections.Generic;
using UnityEngine;

namespace MahjongAtelier.Core
{
    /// <summary>
    /// 게임 진행 매니저 — 점수 계산 통합 버전.
    /// 
    /// 변경점:
    ///   - PlayerScore 추가 (시작 25000점)
    ///   - 화료 시 ScoreCalculator로 실제 점수 산정
    ///   - 화료자에게 가산, 패자(쯔모면 모두, 론이면 쏜 사람)에게 차감
    /// 
    /// 1인용에선 친(親) 자기 자신만 있으므로 다음과 같이 동작:
    ///   - 쯔모 화료 시: 시뮬레이션상 "자(子) 3명에게서 받음" 가정으로 계산 후 자신에게만 가산
    ///   - 실제 다른 플레이어 없음 → 시각화만
    /// 
    /// 4인용에서는 이 코드가 그대로 동작 (PlayerScore 4개로 늘리기만 하면 됨).
    /// </summary>
    public class MahjongGameManager : MonoBehaviour
    {
        [Header("게임 설정")]
        [SerializeField] private int playerCount = 1;
        [SerializeField] private bool includeAkaDora = true;
        [SerializeField] private int randomSeed = 0;
        [SerializeField] private int startingPoints = 25000;

        [Header("자풍/장풍 (1인용 테스트 기본값)")]
        [SerializeField] private TileSuitWindKr seatWindKr = TileSuitWindKr.East;
        [SerializeField] private TileSuitWindKr roundWindKr = TileSuitWindKr.East;

        public enum TileSuitWindKr { East, South, West, North }

        public IReadOnlyList<MahjongPlayer> Players => _players;
        public IReadOnlyList<PlayerScore> Scores => _scores;
        public WallManager Wall => _wallManager;
        public GamePhase CurrentPhase => _phase;
        public int CurrentPlayerIndex => _currentPlayerIndex;
        public int TurnCount => _turnCount;

        private readonly List<MahjongPlayer> _players = new List<MahjongPlayer>();
        private readonly List<PlayerScore> _scores = new List<PlayerScore>();
        private readonly WallManager _wallManager = new WallManager();
        private readonly YakuChecker _yakuChecker = YakuChecker.CreateStandard();
        private System.Random _rng;

        private GamePhase _phase = GamePhase.NotStarted;
        private int _currentPlayerIndex = 0;
        private int _turnCount = 0;

        /// <summary>1인용에선 친이 항상 P0. 4인용 확장 시 동(東)이 누구인지로 결정.</summary>
        private int DealerIndex => 0;

        private void Start()
        {
            StartGame();
        }

        private void OnDestroy()
        {
            GameEvents.Clear();
        }

        public void StartGame()
        {
            _rng = randomSeed == 0 ? new System.Random() : new System.Random(randomSeed);
            CreatePlayersAndScores();
            SetupWall();
            DealInitialHands();
            _currentPlayerIndex = 0;
            _turnCount = 0;
            BeginTurn();
        }

        private void CreatePlayersAndScores()
        {
            _players.Clear();
            _scores.Clear();
            for (int i = 0; i < playerCount; i++)
            {
                _players.Add(new MahjongPlayer(i));
                _scores.Add(new PlayerScore(i, startingPoints));
            }
        }

        private void SetupWall()
        {
            _wallManager.CreateWall(includeAkaDora);
            _wallManager.Shuffle(_rng);
        }

        private void DealInitialHands()
        {
            ChangePhase(GamePhase.Dealing);
            GameEvents.RaiseDealStarted();

            for (int round = 0; round < 13; round++)
            {
                for (int p = 0; p < _players.Count; p++)
                {
                    TileData tile = _wallManager.DrawTile();
                    if (tile == null)
                    {
                        Debug.LogError("배패 중 패산 소진.");
                        return;
                    }
                    _players[p].AddTile(tile);
                }
            }

            GameEvents.RaiseDealCompleted();
            Debug.Log($"[GameManager] 배패 완료. 남은 패산: {_wallManager.RemainingDrawable}");
        }

        // ============================================================
        // 턴 진행
        // ============================================================

        private void BeginTurn()
        {
            _turnCount++;
            GameEvents.RaiseTurnStarted(_currentPlayerIndex);
            DrawForCurrentPlayer();
        }

        private void DrawForCurrentPlayer()
        {
            ChangePhase(GamePhase.WaitingDraw);

            TileData drawn = _wallManager.DrawTile();
            if (drawn == null)
            {
                HandleExhaustiveDraw();
                return;
            }

            var player = _players[_currentPlayerIndex];
            player.AddTile(drawn);
            GameEvents.RaiseTileDrawn(_currentPlayerIndex, drawn);

            Debug.Log($"[GameManager] P{_currentPlayerIndex} 쯔모: {drawn}");

            // 쯔모 화료 자동 체크
            if (player.IsAgari())
            {
                HandleAgari(player, drawn.Kind, isTsumo: true, ronTargetIndex: -1);
                return;
            }

            ChangePhase(GamePhase.WaitingDiscard);
        }

        public bool RequestDiscard(TileData tile)
        {
            if (_phase != GamePhase.WaitingDiscard)
            {
                Debug.LogWarning($"[GameManager] 지금은 버릴 수 없습니다. Phase={_phase}");
                return false;
            }

            var player = _players[_currentPlayerIndex];
            if (!player.HandTiles.Contains(tile))
            {
                Debug.LogWarning($"[GameManager] 손패에 없는 패: {tile}");
                return false;
            }

            if (player.HandTiles.Count != 14)
            {
                Debug.LogWarning($"[GameManager] 손패가 14장이 아님: {player.HandTiles.Count}");
                return false;
            }

            player.DiscardTile(tile);
            Debug.Log($"[GameManager] P{_currentPlayerIndex} 버림: {tile}");

            AdvanceToNextPlayer();
            return true;
        }

        private void AdvanceToNextPlayer()
        {
            _currentPlayerIndex = (_currentPlayerIndex + 1) % _players.Count;
            BeginTurn();
        }

        private void HandleExhaustiveDraw()
        {
            Debug.Log("[GameManager] 유국(流局) — 패산 소진");
            ChangePhase(GamePhase.HandFinished);
        }

        private void ChangePhase(GamePhase next)
        {
            if (_phase == next) return;
            var prev = _phase;
            _phase = next;
            GameEvents.RaisePhaseChanged(prev, next);
        }

        // ============================================================
        // 화료 + 점수 처리
        // ============================================================

        /// <summary>
        /// 화료 처리. 역 판정 → 점수 계산 → 점수 분배 → 이벤트 발행.
        /// </summary>
        /// <param name="player">화료자</param>
        /// <param name="winningTile">화료패</param>
        /// <param name="isTsumo">쯔모 화료?</param>
        /// <param name="ronTargetIndex">론 화료 시 쏜 사람 인덱스 (쯔모면 -1)</param>
        private void HandleAgari(MahjongPlayer player, TileKind winningTile,
            bool isTsumo, int ronTargetIndex)
        {
            var form = HandAnalyzer.DetectAgariForm(player.ToCounter());
            Debug.Log($"[GameManager] 화료! Form: {form}, 방식: {(isTsumo ? "쯔모" : "론")}");

            // 1) 역 판정
            var yakuResult = EvaluateYaku(player, winningTile, isTsumo);

            if (!yakuResult.HasAnyYaku)
            {
                Debug.LogWarning("[Agari] 역 없음 — 화료 불가 (실제 게임에선 차하/노아가리)");
                // 실제 룰에선 역 없음은 화료 선언 자체가 불가지만, 디버그 단계라 일단 로그만
                ChangePhase(GamePhase.HandFinished);
                return;
            }

            // 2) 점수 계산
            bool isDealer = (player.Index == DealerIndex);
            var scoreResult = ScoreCalculator.Calculate(
                BuildContext(player, winningTile, isTsumo), yakuResult, isDealer);

            Debug.Log($"[YakuResult] {yakuResult}");
            Debug.Log($"[ScoreResult] {scoreResult}");

            // 3) 점수 분배
            ApplyScoreChanges(player.Index, scoreResult, ronTargetIndex);

            // 4) 통계 + 이벤트
            _scores[player.Index].RecordWin();
            if (!isTsumo && ronTargetIndex >= 0)
                _scores[ronTargetIndex].RecordDealIn();

            LogAllScores();
            GameEvents.RaiseAgari(player.Index, form);
            ChangePhase(GamePhase.HandFinished);
        }

        /// <summary>점수 결과를 각 플레이어에게 적용.</summary>
        private void ApplyScoreChanges(int winnerIndex, ScoreResult score, int ronTargetIndex)
        {
            _scores[winnerIndex].AddPoints(score.TotalGain);

            if (score.IsTsumo)
            {
                // 4인용 시: 친/자 구분해서 다른 3명에게서 차감
                for (int i = 0; i < _players.Count; i++)
                {
                    if (i == winnerIndex) continue;
                    int payment = (i == DealerIndex)
                        ? score.TsumoPaymentFromDealer
                        : score.TsumoPaymentFromNonDealer;
                    // 화료자가 친인 경우엔 친 지불 0이고 자 지불만 있음
                    if (score.IsDealer) payment = score.TsumoPaymentFromNonDealer;
                    _scores[i].AddPoints(-payment);
                }
            }
            else
            {
                // 론: 쏜 사람만 차감
                if (ronTargetIndex >= 0 && ronTargetIndex < _scores.Count)
                {
                    _scores[ronTargetIndex].AddPoints(-score.RonPayment);
                }
            }
        }

        private void LogAllScores()
        {
            var sb = new System.Text.StringBuilder("[현재 점수] ");
            foreach (var s in _scores) sb.Append(s + "  ");
            Debug.Log(sb.ToString());
        }

        // ============================================================
        // 컨텍스트 빌더
        // ============================================================

        private YakuContext BuildContext(MahjongPlayer player, TileKind winningTile, bool isTsumo)
        {
            return new YakuContext
            {
                Hand = player.ToCounter(),
                WinningTile = winningTile,
                IsTsumo = isTsumo,
                IsRiichi = player.IsRiichi,
                SeatWind = WindKrToKind(seatWindKr),
                RoundWind = WindKrToKind(roundWindKr),
                DoraIndicators = GetDoraIndicatorKinds(),
                AkaDoraCount = player.CountAkaDora()
            };
        }

        public YakuResult EvaluateYaku(MahjongPlayer player, TileKind winningTile, bool isTsumo)
        {
            return _yakuChecker.Check(BuildContext(player, winningTile, isTsumo));
        }

        private List<TileKind> GetDoraIndicatorKinds()
        {
            var list = new List<TileKind>();
            foreach (var ind in _wallManager.GetDoraIndicators())
                list.Add(ind.Kind);
            return list;
        }

        private static TileKind WindKrToKind(TileSuitWindKr wind)
        {
            switch (wind)
            {
                case TileSuitWindKr.East: return TileKind.East;
                case TileSuitWindKr.South: return TileKind.South;
                case TileSuitWindKr.West: return TileKind.West;
                case TileSuitWindKr.North: return TileKind.North;
                default: return TileKind.East;
            }
        }

        // ============================================================
        // 디버그
        // ============================================================

        [ContextMenu("Debug: Print Current Hand")]
        public void DebugPrintCurrentHand()
        {
            if (_players.Count == 0) return;
            var p = _players[_currentPlayerIndex];
            var counter = p.ToCounter();
            Debug.Log($"P{_currentPlayerIndex} Hand ({p.HandTiles.Count}): {counter}");
            if (p.HandTiles.Count == 13)
            {
                var waits = p.GetWaitingTiles();
                if (waits.Count > 0)
                    Debug.Log($"  → 텐파이! 대기: {string.Join(", ", waits)}");
            }
        }

        [ContextMenu("Debug: Force Agari Check")]
        public void DebugForceAgariCheck()
        {
            if (_players.Count == 0) return;
            var p = _players[_currentPlayerIndex];
            if (p.HandTiles.Count != 14)
            {
                Debug.LogWarning("14장 상태에서만 가능합니다.");
                return;
            }
            if (!p.IsAgari())
            {
                Debug.Log("화료 형태 아님");
                return;
            }
            var winTile = p.HandTiles[p.HandTiles.Count - 1].Kind;
            HandleAgari(p, winTile, isTsumo: true, ronTargetIndex: -1);
        }

        [ContextMenu("Debug: Print All Scores")]
        public void DebugPrintScores() => LogAllScores();
    }
}

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MahjongAtelier.Core
{
    /// <summary>
    /// 게임 매니저 v11 — 게임 종료 처리.
    /// 
    /// 추가:
    ///   - GameMatchState 통합 (반장전/동풍전 모드)
    ///   - 장풍 자동 전환 (반장전: 4국 후 동→남)
    ///   - 게임 종료 시 FinalResultPanel 표시
    ///   - ForceEndGame() — 테스트용 강제 종료
    /// </summary>
    public class MahjongGameManager : MonoBehaviour
    {
        public enum GameMode { SinglePlayer, FourPlayerWithBots }

        [Header("게임 모드")]
        [SerializeField] private GameMode gameMode = GameMode.FourPlayerWithBots;

        [Header("게임 길이")]
        [SerializeField] private MatchMode matchMode = MatchMode.Hanchan;

        [Header("게임 설정")]
        [SerializeField] private bool includeAkaDora = true;
        [SerializeField] private int randomSeed = 0;
        [SerializeField] private int startingPoints = 25000;

        [Header("UI 연결 (옵션)")]
        [SerializeField] private MahjongAtelier.UI.AgariResultPanel agariResultPanel;
        [SerializeField] private MahjongAtelier.UI.ExhaustiveDrawPanel exhaustiveDrawPanel;
        [SerializeField] private MahjongAtelier.UI.FinalResultPanel finalResultPanel;

        [Header("타이밍")]
        [SerializeField, Range(0f, 5f)] private float riichiAutoDiscardDelay = 1.0f;
        [SerializeField, Range(0f, 3f)] private float riichiDrawObserveDelay = 0.5f;
        [SerializeField, Range(0f, 3f)] private float botActionDelay = 0.7f;
        [SerializeField, Range(0f, 10f)] private float ronUserDecisionTimeout = 5f;
        [SerializeField, Range(0f, 10f)] private float callUserDecisionTimeout = 4f;

        public IReadOnlyList<MahjongPlayer> Players => _players;
        public IReadOnlyList<PlayerScore> Scores => _scores;
        public WallManager Wall => _wallManager;
        public GamePhase CurrentPhase => _phase;
        public int CurrentPlayerIndex => _currentPlayerIndex;
        public int TurnCount => _turnCount;
        public int RiichiDeposits => _riichiDeposits;
        public int DealerIndex => _dealerIndex;
        public int HonbaCount => _honbaCount;
        public int UserPlayerIndex => 0;
        public GameMatchState MatchState => _matchState;
        public SeatWind RoundWind => _matchState != null ? _matchState.CurrentRoundWind : SeatWind.East;

        public string GetCurrentHandLabel() => _matchState != null ? _matchState.GetHandLabel() : "";

        private readonly List<MahjongPlayer> _players = new List<MahjongPlayer>();
        private readonly List<PlayerScore> _scores = new List<PlayerScore>();
        private readonly WallManager _wallManager = new WallManager();
        private readonly YakuChecker _yakuChecker = YakuChecker.CreateStandard();
        private System.Random _rng;

        private GameMatchState _matchState;

        private GamePhase _phase = GamePhase.NotStarted;
        private int _currentPlayerIndex = 0;
        private int _turnCount = 0;
        private int _riichiDeposits = 0;
        private int _dealerIndex = 0;
        private int _honbaCount = 0;
        private Coroutine _activeCoroutine;
        private bool _pendingDealerRepeat = false;

        // 론/후로 대기 상태
        private TileData _pendingDiscardedTile;
        private int _pendingDiscarderIndex = -1;
        private List<RonCandidate> _pendingRonCandidates;
        private bool _waitingForUserRonDecision = false;
        private CallOpportunity _pendingCallOpportunity;
        private bool _waitingForUserCallDecision = false;

        private bool _isRinshanDraw = false;

        // 게임 종료 처리
        private bool _gameEnded = false;

        private void Start()
        {
            if (agariResultPanel != null)
                agariResultPanel.OnNextButtonClicked += HandleNextHandRequested;
            if (exhaustiveDrawPanel != null)
                exhaustiveDrawPanel.OnNextButtonClicked += HandleNextHandRequested;
            if (finalResultPanel != null)
                finalResultPanel.OnRestartClicked += HandleRestartRequested;
            StartGame();
        }

        private void OnDestroy()
        {
            if (agariResultPanel != null)
                agariResultPanel.OnNextButtonClicked -= HandleNextHandRequested;
            if (exhaustiveDrawPanel != null)
                exhaustiveDrawPanel.OnNextButtonClicked -= HandleNextHandRequested;
            if (finalResultPanel != null)
                finalResultPanel.OnRestartClicked -= HandleRestartRequested;
            GameEvents.Clear();
        }

        public void StartGame()
        {
            StopActiveCoroutine();
            _rng = randomSeed == 0 ? new System.Random() : new System.Random(randomSeed);
            CreatePlayersAndScores();
            _matchState = new GameMatchState(matchMode);
            _riichiDeposits = 0;
            _dealerIndex = 0;
            _honbaCount = 0;
            _gameEnded = false;
            AssignSeatWinds();
            SetupWall();
            DealInitialHands();
            _currentPlayerIndex = _dealerIndex;
            _turnCount = 0;
            ClearPendingStates();
            BeginTurn();
        }

        /// <summary>다시 시작 (최종 결과창에서 호출).</summary>
        private void HandleRestartRequested() => StartGame();

        public void StartNextHand(bool dealerRepeat = false)
        {
            StopActiveCoroutine();
            foreach (var p in _players) p.Reset();

            if (dealerRepeat)
            {
                _honbaCount++;
                Debug.Log($"[GameManager] 친 연장. 본장: {_honbaCount}");
            }
            else
            {
                _dealerIndex = (_dealerIndex + 1) % _players.Count;
                _honbaCount = 0;
                // 친이 이동한 경우에만 국 인덱스 증가
                _matchState.AdvanceHand();
                Debug.Log($"[GameManager] 친 이동. 새 친: P{_dealerIndex}, 국: {_matchState.GetHandLabel()}");

                // 게임 종료 체크
                if (_matchState.IsGameOver)
                {
                    Debug.Log("[GameManager] 모든 국 종료 — 최종 결과창 표시");
                    EndGame(wasForceEnded: false);
                    return;
                }
            }
            AssignSeatWinds();

            _wallManager.CreateWall(includeAkaDora);
            _wallManager.Shuffle(_rng);
            ChangePhase(GamePhase.Dealing);
            GameEvents.RaiseDealStarted();
            for (int round = 0; round < 13; round++)
            {
                for (int p = 0; p < _players.Count; p++)
                {
                    TileData tile = _wallManager.DrawTile();
                    if (tile == null) return;
                    _players[p].AddTile(tile);
                }
            }
            GameEvents.RaiseDealCompleted();

            _currentPlayerIndex = _dealerIndex;
            _turnCount = 0;
            ClearPendingStates();
            BeginTurn();
        }

        private void HandleNextHandRequested()
        {
            StartNextHand(_pendingDealerRepeat);
            _pendingDealerRepeat = false;
        }

        // === 게임 종료 ===

        /// <summary>
        /// 강제 게임 종료 (테스트용).
        /// </summary>
        public void ForceEndGame()
        {
            if (_gameEnded) return;
            Debug.Log("[GameManager] 강제 종료 요청됨");
            EndGame(wasForceEnded: true);
        }

        private void EndGame(bool wasForceEnded)
        {
            _gameEnded = true;
            StopActiveCoroutine();
            ChangePhase(GamePhase.HandFinished);

            int handsPlayed = _matchState != null ? _matchState.HandIndex : 0;
            // 마지막 국이 도중 종료된 경우, 진행 중인 국 자체는 카운트 안 함
            if (wasForceEnded && handsPlayed > 0)
            {
                // HandIndex는 다음 국으로 가는 카운터라 진행한 국 수는 그대로 둠
            }

            var result = FinalResult.Build(_players, _scores, handsPlayed, wasForceEnded);

            Debug.Log("[GameManager] === 게임 최종 결과 ===");
            foreach (var entry in result.Rankings)
                Debug.Log($"  {entry}");

            if (finalResultPanel != null)
            {
                finalResultPanel.Show(result);
            }
            else
            {
                Debug.LogWarning("[GameManager] finalResultPanel 미연결 — 콘솔에만 출력");
            }
        }

        private void StopActiveCoroutine()
        {
            if (_activeCoroutine != null)
            {
                StopCoroutine(_activeCoroutine);
                _activeCoroutine = null;
            }
        }

        private void ClearPendingStates()
        {
            _waitingForUserRonDecision = false;
            _pendingDiscardedTile = null;
            _pendingDiscarderIndex = -1;
            _pendingRonCandidates = null;
            _waitingForUserCallDecision = false;
            _pendingCallOpportunity = null;
            _isRinshanDraw = false;
        }

        private void CreatePlayersAndScores()
        {
            _players.Clear();
            _scores.Clear();
            int count = gameMode == GameMode.SinglePlayer ? 1 : 4;
            for (int i = 0; i < count; i++)
            {
                var type = (gameMode == GameMode.SinglePlayer || i == UserPlayerIndex)
                    ? PlayerType.Human : PlayerType.Bot;
                _players.Add(new MahjongPlayer(i, type));
                _scores.Add(new PlayerScore(i, startingPoints));
            }
        }

        private void AssignSeatWinds()
        {
            for (int i = 0; i < _players.Count; i++)
            {
                int rel = (i - _dealerIndex + _players.Count) % _players.Count;
                _players[i].Seat = (SeatWind)rel;
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
                    if (tile == null) return;
                    _players[p].AddTile(tile);
                }
            }
            GameEvents.RaiseDealCompleted();
        }

        // === 턴 진행 ===

        private void BeginTurn()
        {
            if (_gameEnded) return;
            _turnCount++;
            GameEvents.RaiseTurnStarted(_currentPlayerIndex);
            DrawForCurrentPlayer();
        }

        private void DrawForCurrentPlayer()
        {
            ChangePhase(GamePhase.WaitingDraw);
            TileData drawn = _isRinshanDraw
                ? _wallManager.DrawRinshan()
                : _wallManager.DrawTile();

            if (drawn == null) { HandleExhaustiveDraw(); return; }

            var player = _players[_currentPlayerIndex];
            player.AddTile(drawn);
            GameEvents.RaiseTileDrawn(_currentPlayerIndex, drawn);

            Debug.Log($"[GameManager] P{_currentPlayerIndex}({player.Seat}) 쯔모: {drawn}" +
                (_isRinshanDraw ? " (린샨)" : ""));

            if (player.IsAgari() && player.IsValidAgariTile(drawn.Kind))
            {
                var ctx = BuildContext(player, drawn.Kind, true);
                ctx.IsRinshan = _isRinshanDraw;
                var preCheck = _yakuChecker.Check(ctx);

                if (preCheck.HasAnyYaku)
                {
                    HandleAgari(player, drawn, isTsumo: true, ronTargetIndex: -1, isRinshan: _isRinshanDraw);
                    return;
                }
                else
                {
                    Debug.Log($"[GameManager] 쯔모 화료 형태이지만 역 없음 — 계속 진행");
                }
            }

            _isRinshanDraw = false;
            ChangePhase(GamePhase.WaitingDiscard);

            if (player.IsBot)
                _activeCoroutine = StartCoroutine(BotDiscardCoroutine(player, drawn));
            else
            {
                UpdateRiichiAvailability(player);
                UpdateSelfKanAvailability(player);
                if (player.IsRiichi)
                    _activeCoroutine = StartCoroutine(RiichiAutoDiscardCoroutine(player, drawn));
            }
        }

        private IEnumerator BotDiscardCoroutine(MahjongPlayer player, TileData drawnTile)
        {
            if (botActionDelay > 0f)
                yield return new WaitForSeconds(botActionDelay);
            if (_phase != GamePhase.WaitingDiscard) yield break;
            if (!player.HandTiles.Contains(drawnTile)) yield break;

            Debug.Log($"[Bot] P{player.Index}({player.Seat}) 버림: {drawnTile}");
            ExecuteDiscard(player, drawnTile);
            _activeCoroutine = null;
        }

        private IEnumerator RiichiAutoDiscardCoroutine(MahjongPlayer player, TileData drawnTile)
        {
            if (riichiDrawObserveDelay > 0f)
                yield return new WaitForSeconds(riichiDrawObserveDelay);
            if (riichiAutoDiscardDelay > 0f)
                yield return new WaitForSeconds(riichiAutoDiscardDelay);
            if (_phase != GamePhase.WaitingDiscard) yield break;
            if (!player.IsRiichi) yield break;
            if (!player.HandTiles.Contains(drawnTile)) yield break;
            ExecuteDiscard(player, drawnTile);
            _activeCoroutine = null;
        }

        private void UpdateRiichiAvailability(MahjongPlayer player)
        {
            var analysis = RiichiAnalyzer.Analyze(
                player, _scores[player.Index].Points, _wallManager.RemainingDrawable);
            GameEvents.RaiseRiichiAvailabilityChanged(player.Index, analysis);
        }

        private void UpdateSelfKanAvailability(MahjongPlayer player)
        {
            if (!_wallManager.CanDrawRinshan)
            {
                GameEvents.RaiseSelfKanAvailability(new List<TileKind>(), new List<TileKind>());
                return;
            }
            var ankanKinds = KanAnalyzer.FindAnkanKinds(player);
            var shouminkanKinds = KanAnalyzer.FindShouminkanKinds(player);
            if (player.IsRiichi)
            {
                ankanKinds.Clear();
                shouminkanKinds.Clear();
            }
            GameEvents.RaiseSelfKanAvailability(ankanKinds, shouminkanKinds);
        }

        // === 버림 + 후로 검사 ===

        private void ExecuteDiscard(MahjongPlayer player, TileData tile)
        {
            player.DiscardTile(tile);
            CheckReactions(player.Index, tile);
        }

        private void CheckReactions(int discarderIndex, TileData discardedTile)
        {
            var ronCandidates = RonAnalyzer.FindCandidates(
                discarderIndex, discardedTile, _players, _yakuChecker,
                (player, kind, isTsumo) => BuildContext(player, kind, isTsumo));

            bool userCanRon = false;
            foreach (var c in ronCandidates)
                if (c.PlayerIndex == UserPlayerIndex) { userCanRon = true; break; }

            if (userCanRon)
            {
                _pendingDiscardedTile = discardedTile;
                _pendingDiscarderIndex = discarderIndex;
                _pendingRonCandidates = ronCandidates;
                _waitingForUserRonDecision = true;
                GameEvents.RaiseRonOpportunity(discardedTile, discarderIndex, ronCandidates);
                return;
            }

            CheckCallOpportunity(discarderIndex, discardedTile);
        }

        private void CheckCallOpportunity(int discarderIndex, TileData discardedTile)
        {
            if (discarderIndex == UserPlayerIndex) { AdvanceToNextPlayer(); return; }

            var user = _players[UserPlayerIndex];
            if (user.IsRiichi) { AdvanceToNextPlayer(); return; }

            var opp = new CallOpportunity
            {
                DiscardedTile = discardedTile,
                DiscarderIndex = discarderIndex
            };

            opp.CanPon = PonAnalyzer.CanPon(user, discardedTile);
            opp.CanDaiminkan = KanAnalyzer.CanDaiminkan(user, discardedTile) && _wallManager.CanDrawRinshan;

            int prevPlayerIdx = (UserPlayerIndex - 1 + _players.Count) % _players.Count;
            if (discarderIndex == prevPlayerIdx)
            {
                var chiOptions = ChiAnalyzer.FindOptions(user, discardedTile);
                if (chiOptions.Count > 0)
                {
                    opp.CanChi = true;
                    opp.ChiOptions = chiOptions;
                }
            }

            if (!opp.HasAny)
            {
                AdvanceToNextPlayer();
                return;
            }

            _pendingCallOpportunity = opp;
            _waitingForUserCallDecision = true;
            GameEvents.RaiseCallOpportunity(opp);
        }

        // === 사용자 입력 ===

        public bool RequestRon(int playerIndex, TileData tile, int discarderIndex)
        {
            if (!_waitingForUserRonDecision) return false;
            if (playerIndex != UserPlayerIndex) return false;
            if (_pendingDiscardedTile != tile) return false;

            RonCandidate userCandidate = null;
            if (_pendingRonCandidates != null)
            {
                foreach (var c in _pendingRonCandidates)
                    if (c.PlayerIndex == playerIndex) { userCandidate = c; break; }
            }
            if (userCandidate == null) return false;

            _waitingForUserRonDecision = false;
            var winners = new List<RonCandidate> { userCandidate };
            var capturedTile = _pendingDiscardedTile;
            var capturedDiscarder = _pendingDiscarderIndex;
            _pendingDiscardedTile = null;
            _pendingDiscarderIndex = -1;
            _pendingRonCandidates = null;

            GameEvents.RaiseRonOpportunityClosed();
            ExecuteRon(winners, capturedTile, capturedDiscarder);
            return true;
        }

        public void PassRonOpportunity(int playerIndex)
        {
            if (!_waitingForUserRonDecision) return;
            if (playerIndex != UserPlayerIndex) return;

            _waitingForUserRonDecision = false;
            var savedTile = _pendingDiscardedTile;
            var savedDiscarder = _pendingDiscarderIndex;
            _pendingDiscardedTile = null;
            _pendingDiscarderIndex = -1;
            _pendingRonCandidates = null;

            GameEvents.RaiseRonOpportunityClosed();
            CheckCallOpportunity(savedDiscarder, savedTile);
        }

        public bool RequestPon()
        {
            if (!_waitingForUserCallDecision || _pendingCallOpportunity == null) return false;
            if (!_pendingCallOpportunity.CanPon) return false;
            var opp = _pendingCallOpportunity;
            ClearCallOpportunity();
            ExecuteCall(UserPlayerIndex, opp.DiscardedTile, opp.DiscarderIndex, CallType.Pon, null);
            return true;
        }

        public bool RequestDaiminkan()
        {
            if (!_waitingForUserCallDecision || _pendingCallOpportunity == null) return false;
            if (!_pendingCallOpportunity.CanDaiminkan) return false;
            var opp = _pendingCallOpportunity;
            ClearCallOpportunity();
            ExecuteCall(UserPlayerIndex, opp.DiscardedTile, opp.DiscarderIndex, CallType.Daiminkan, null);
            return true;
        }

        public bool RequestChi(ChiOption option)
        {
            if (!_waitingForUserCallDecision || _pendingCallOpportunity == null) return false;
            if (!_pendingCallOpportunity.CanChi) return false;
            if (option == null) return false;
            var opp = _pendingCallOpportunity;
            ClearCallOpportunity();
            ExecuteCall(UserPlayerIndex, opp.DiscardedTile, opp.DiscarderIndex, CallType.Chi, option);
            return true;
        }

        public void PassCallOpportunity()
        {
            if (!_waitingForUserCallDecision) return;
            ClearCallOpportunity();
            AdvanceToNextPlayer();
        }

        private void ClearCallOpportunity()
        {
            _waitingForUserCallDecision = false;
            _pendingCallOpportunity = null;
            GameEvents.RaiseCallOpportunityClosed();
        }

        private void ExecuteCall(int callerIndex, TileData calledTile, int fromPlayerIndex,
            CallType callType, ChiOption chiOption)
        {
            var caller = _players[callerIndex];
            var fromPlayer = _players[fromPlayerIndex];
            fromPlayer.DiscardPile.Remove(calledTile);

            CalledMeld meld = null;
            switch (callType)
            {
                case CallType.Pon:
                    {
                        var twoFromHand = PonAnalyzer.FindPonTiles(caller, calledTile.Kind);
                        foreach (var t in twoFromHand) caller.RemoveTile(t);
                        var tiles = new List<TileData> { twoFromHand[0], twoFromHand[1], calledTile };
                        meld = new CalledMeld(MeldType.Triplet, calledTile.Kind, tiles, calledTile, fromPlayerIndex, CallType.Pon);
                        break;
                    }
                case CallType.Daiminkan:
                    {
                        var fromHand = new List<TileData>();
                        foreach (var t in caller.HandTiles)
                        {
                            if (t.Kind == calledTile.Kind) fromHand.Add(t);
                            if (fromHand.Count >= 3) break;
                        }
                        foreach (var t in fromHand) caller.RemoveTile(t);
                        var tiles = new List<TileData>(fromHand) { calledTile };
                        meld = new CalledMeld(MeldType.Quad, calledTile.Kind, tiles, calledTile, fromPlayerIndex, CallType.Daiminkan);
                        break;
                    }
                case CallType.Chi:
                    {
                        if (chiOption.LowTile != calledTile) caller.RemoveTile(chiOption.LowTile);
                        if (chiOption.MiddleTile != calledTile) caller.RemoveTile(chiOption.MiddleTile);
                        if (chiOption.HighTile != calledTile) caller.RemoveTile(chiOption.HighTile);
                        var tiles = new List<TileData> { chiOption.LowTile, chiOption.MiddleTile, chiOption.HighTile };
                        meld = new CalledMeld(MeldType.Sequence, chiOption.SequenceStart, tiles, calledTile, fromPlayerIndex, CallType.Chi);
                        break;
                    }
            }

            if (meld == null) return;
            caller.AddCalledMeld(meld);
            caller.CancelIppatsu();
            foreach (var p in _players) if (p.IsRiichi) p.CancelIppatsu();
            GameEvents.RaiseCallDeclared(callerIndex, meld);

            _currentPlayerIndex = callerIndex;

            if (callType == CallType.Daiminkan)
            {
                _wallManager.RevealAdditionalDora();
                _isRinshanDraw = true;
                BeginTurn();
            }
            else
            {
                ChangePhase(GamePhase.WaitingDiscard);
                if (caller.IsBot)
                {
                    var firstTile = caller.HandTiles.Count > 0 ? caller.HandTiles[0] : null;
                    if (firstTile != null) ExecuteDiscard(caller, firstTile);
                }
                else
                {
                    UpdateSelfKanAvailability(caller);
                }
            }
        }

        public bool RequestAnkan(TileKind kind)
        {
            if (_phase != GamePhase.WaitingDiscard) return false;
            var player = _players[_currentPlayerIndex];
            if (player.IsBot) return false;
            if (!_wallManager.CanDrawRinshan) return false;

            var tiles = new List<TileData>();
            foreach (var t in player.HandTiles)
            {
                if (t.Kind == kind) tiles.Add(t);
                if (tiles.Count >= 4) break;
            }
            if (tiles.Count < 4) return false;
            foreach (var t in tiles) player.RemoveTile(t);
            var meld = new CalledMeld(MeldType.Quad, kind, tiles, null, -1, CallType.Ankan);
            player.AddCalledMeld(meld);
            GameEvents.RaiseCallDeclared(player.Index, meld);
            _wallManager.RevealAdditionalDora();
            _isRinshanDraw = true;
            BeginTurn();
            return true;
        }

        public bool RequestShouminkan(TileKind kind)
        {
            if (_phase != GamePhase.WaitingDiscard) return false;
            var player = _players[_currentPlayerIndex];
            if (player.IsBot) return false;
            if (!_wallManager.CanDrawRinshan) return false;

            TileData target = null;
            foreach (var t in player.HandTiles)
            {
                if (t.Kind == kind) { target = t; break; }
            }
            if (target == null) return false;
            if (!player.UpgradePonToKan(kind, target)) return false;
            player.RemoveTile(target);
            _wallManager.RevealAdditionalDora();
            _isRinshanDraw = true;
            BeginTurn();
            return true;
        }

        public bool RequestDiscard(TileData tile)
        {
            if (_phase != GamePhase.WaitingDiscard) return false;
            if (_waitingForUserRonDecision || _waitingForUserCallDecision) return false;

            var player = _players[_currentPlayerIndex];
            if (player.IsBot) return false;
            if (!player.HandTiles.Contains(tile)) return false;
            if (player.IsRiichi) return false;

            ExecuteDiscard(player, tile);
            return true;
        }

        public bool RequestRiichi(TileData discardTile)
        {
            if (_phase != GamePhase.WaitingDiscard) return false;
            var player = _players[_currentPlayerIndex];
            if (player.IsBot) return false;

            var analysis = RiichiAnalyzer.Analyze(
                player, _scores[player.Index].Points, _wallManager.RemainingDrawable);
            if (!analysis.CanDeclareRiichi) return false;
            if (!analysis.IsValidDiscard(discardTile)) return false;

            _scores[player.Index].AddPoints(-RiichiAnalyzer.RiichiDepositAmount);
            _riichiDeposits += RiichiAnalyzer.RiichiDepositAmount;
            player.DiscardTile(discardTile);
            var waits = TenpaiAnalyzer.GetWaitingTiles(player.ToCounter());
            player.DeclareRiichi(_turnCount, waits);
            GameEvents.RaiseRiichiDeclared(player.Index);

            CheckReactions(player.Index, discardTile);
            return true;
        }

        private void AdvanceToNextPlayer()
        {
            int nextIdx = (_currentPlayerIndex + 1) % _players.Count;
            if (nextIdx == _currentPlayerIndex)
            {
                var p = _players[_currentPlayerIndex];
                if (p.IsRiichi && p.IsIppatsuChance && _turnCount > p.RiichiTurn)
                    p.CancelIppatsu();
            }
            _currentPlayerIndex = nextIdx;
            BeginTurn();
        }

        private void HandleExhaustiveDraw()
        {
            ChangePhase(GamePhase.HandFinished);
            var result = ExhaustiveDrawProcessor.Process(_players, _dealerIndex);
            foreach (var status in result.Statuses)
            {
                if (status.PointChange != 0)
                    _scores[status.PlayerIndex].AddPoints(status.PointChange);
            }
            _pendingDealerRepeat = result.IsDealerRepeat;

            if (exhaustiveDrawPanel != null) exhaustiveDrawPanel.Show(result);
            else { StartNextHand(_pendingDealerRepeat); _pendingDealerRepeat = false; }
        }

        private void ChangePhase(GamePhase next)
        {
            if (_phase == next) return;
            var prev = _phase;
            _phase = next;
            GameEvents.RaisePhaseChanged(prev, next);
        }

        private void HandleAgari(MahjongPlayer player, TileData winningTileData,
            bool isTsumo, int ronTargetIndex, bool isRinshan = false)
        {
            var form = HandAnalyzer.DetectAgariForm(player.ToCounter());
            var ctx = BuildContext(player, winningTileData.Kind, isTsumo);
            ctx.IsRinshan = isRinshan;
            var yakuResult = _yakuChecker.Check(ctx);

            if (!yakuResult.HasAnyYaku)
            {
                Debug.LogWarning("[Agari] 역 없는 손 도달 — 게임 계속");
                if (!isTsumo)
                {
                    player.RemoveTile(winningTileData);
                    var discarder = _players[ronTargetIndex];
                    discarder.DiscardPile.Add(winningTileData);
                    AdvanceToNextPlayer();
                }
                else
                {
                    ChangePhase(GamePhase.WaitingDiscard);
                    if (player.IsBot)
                        _activeCoroutine = StartCoroutine(BotDiscardCoroutine(player, winningTileData));
                }
                return;
            }

            bool isDealer = (player.Index == _dealerIndex);
            var scoreResult = ScoreCalculator.Calculate(ctx, yakuResult, isDealer);

            ApplyScoreChanges(player.Index, scoreResult, ronTargetIndex);
            if (_riichiDeposits > 0) { _scores[player.Index].AddPoints(_riichiDeposits); _riichiDeposits = 0; }
            _scores[player.Index].RecordWin();
            if (!isTsumo && ronTargetIndex >= 0) _scores[ronTargetIndex].RecordDealIn();

            _pendingDealerRepeat = isDealer;
            GameEvents.RaiseAgari(player.Index, form);
            ChangePhase(GamePhase.HandFinished);
            ShowResultUI(player, winningTileData, ctx, yakuResult, scoreResult);
        }

        private void ExecuteRon(List<RonCandidate> winners, TileData tile, int discarderIndex)
        {
            var discarder = _players[discarderIndex];
            discarder.DiscardPile.Remove(tile);

            foreach (var winner in winners)
            {
                var player = _players[winner.PlayerIndex];
                player.AddTile(tile);
                HandleAgari(player, tile, isTsumo: false, ronTargetIndex: discarderIndex);
            }
        }

        private void ShowResultUI(MahjongPlayer player, TileData winningTileData,
            YakuContext ctx, YakuResult yakuResult, ScoreResult scoreResult)
        {
            if (agariResultPanel == null) return;

            var doraIndicators = new List<TileData>();
            foreach (var ind in _wallManager.GetDoraIndicators()) doraIndicators.Add(ind);
            var uraDoraIndicators = new List<TileData>();
            if (ctx.IsRiichi || ctx.IsDoubleRiichi)
            {
                foreach (var ind in _wallManager.GetUraDoraIndicators()) uraDoraIndicators.Add(ind);
            }

            var data = AgariResultData.From(
                winnerIndex: player.Index,
                isDealer: (player.Index == _dealerIndex),
                handTiles: new List<TileData>(player.HandTiles),
                winningTile: winningTileData,
                doraIndicators: doraIndicators,
                uraDoraIndicators: uraDoraIndicators,
                yakuResult: yakuResult,
                scoreResult: scoreResult);

            agariResultPanel.Show(data);
        }

        private void ApplyScoreChanges(int winnerIndex, ScoreResult score, int ronTargetIndex)
        {
            _scores[winnerIndex].AddPoints(score.TotalGain);
            if (score.IsTsumo)
            {
                for (int i = 0; i < _players.Count; i++)
                {
                    if (i == winnerIndex) continue;
                    int payment;
                    if (score.IsDealer) payment = score.TsumoPaymentFromNonDealer;
                    else payment = (i == _dealerIndex) ? score.TsumoPaymentFromDealer : score.TsumoPaymentFromNonDealer;
                    _scores[i].AddPoints(-payment);
                }
            }
            else
            {
                if (ronTargetIndex >= 0 && ronTargetIndex < _scores.Count)
                    _scores[ronTargetIndex].AddPoints(-score.RonPayment);
            }
        }

        private YakuContext BuildContext(MahjongPlayer player, TileKind winningTile, bool isTsumo)
        {
            var uraDoraKinds = new List<TileKind>();
            if (player.IsRiichi)
            {
                foreach (var ind in _wallManager.GetUraDoraIndicators())
                    uraDoraKinds.Add(ind.Kind);
            }
            var calledMelds = new List<Meld>(player.CalledMelds);

            // ★ 핵심 수정: 손패 + 후로 패 합쳐서 14장 카운터 생성
            var combinedTiles = new List<TileData>(player.HandTiles);
            foreach (var meld in player.CalledMeldsExt)
            {
                foreach (var t in meld.Tiles)
                    combinedTiles.Add(t);
            }
            var fullHand = new TileCounter(combinedTiles);

            // 디버그 (선택)
            if (player.Index == 0 && calledMelds.Count > 0)
            {
                Debug.Log($"[BuildContext P0] 손패 {player.HandTiles.Count}장 + 후로 {calledMelds.Count}개 = 총 {combinedTiles.Count}장 (ctx.Hand에 전달)");
            }

            return new YakuContext
            {
                Hand = fullHand,                          // ← 손패+후로 합친 14장
                WinningTile = winningTile,
                IsTsumo = isTsumo,
                IsRiichi = player.IsRiichi,
                IsIppatsu = player.IsIppatsuChance,
                CalledMelds = calledMelds,
                SeatWind = player.Seat.ToTileKind(),
                RoundWind = RoundWind.ToTileKind(),
                DoraIndicators = GetDoraIndicatorKinds(),
                UraDoraIndicators = uraDoraKinds,
                AkaDoraCount = player.CountAkaDora()
            };
        }

        public YakuResult EvaluateYaku(MahjongPlayer player, TileKind winningTile, bool isTsumo)
            => _yakuChecker.Check(BuildContext(player, winningTile, isTsumo));

        private List<TileKind> GetDoraIndicatorKinds()
        {
            var list = new List<TileKind>();
            foreach (var ind in _wallManager.GetDoraIndicators()) list.Add(ind.Kind);
            return list;
        }

        // === 디버그 ===

        [ContextMenu("Debug: Force End Game")]
        public void DebugForceEndGame() => ForceEndGame();

        [ContextMenu("Debug: Force Exhaustive Draw")]
        public void DebugForceExhaustiveDraw()
        {
            while (_wallManager.RemainingDrawable > 0)
                _wallManager.DrawTile();
        }
    }
}

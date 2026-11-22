using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace CardGame.Core
{
    /// <summary>
    /// 카드게임 매니저 v11 — 승리 선언 + 온라인 3모드 + 턴 타이머 / Swap 선택 / 재접속 / 호스트 이전.
    ///
    /// ■ v11 추가:
    ///   - 턴 타이머   : OnTurnTimerChanged(seat, duration). 호스트가 타임아웃을 걸 때 발생, 세션이 RPC로 중계.
    ///   - Swap 선택   : 온라인에서도 사람은 상대를 직접 선택 (OnSwapChoiceRequested → SubmitSwapTarget).
    ///   - 재접속      : RestoreSeatToHuman(seat) + ClientApplyFullState(...) 로 진행 중인 판에 복귀.
    ///   - 호스트 이전 : BuildResumeData() / SetResumeData() — 점수·라운드를 유지한 채 현재 라운드를 다시 시작.
    ///
    /// ■ 승리 선언 (v10, manualWinDeclaration=true):
    ///   - 완성은 자동 판정되지 않음. 자기 차례에 카드를 뽑은 직후(WaitingDiscard, 손패 7장) 손패가 완성이면
    ///     OnWinAvailableChanged(seat, true) 발생 → UI가 [승리 선언] 버튼 표시.
    ///   - DeclareWin() (로컬) / DeclareWinForSeat(seat) (호스트) 호출 시 재검증 후 HandleCompletion.
    ///   - 카드를 버리면 선언 기회는 사라짐 (다음 차례에 다시 완성 상태면 다시 버튼).
    ///   - 봇은 가능하면 botActionDelay 후 자동 선언.
    ///   - 온라인 턴 타임아웃 시 선언 가능하면 자동 선언, 아니면 자동 버림.
    ///   - 특수카드 효과로 손패가 다시 7장이 된 경우도 같은 규칙(그 시점 완성이면 버튼).
    ///
    /// ■ Local (싱글)        : v8.5와 동일. Start()에서 자동 시작.
    /// ■ NetworkHost (호스트) : 게임 로직 권위. 원격 사람(RemoteHuman)의 차례엔 네트워크 입력을 기다림.
    ///                         NetworkGameSession이 ConfigureNetwork() + RequestStartGame() 호출.
    /// ■ NetworkClient (클라) : 로직 없음. ClientApply*() 로 호스트 상태를 복제하고 기존 UI 이벤트를 재발행.
    ///                         사용자 입력(버리기/완성체크)은 OnClient*Requested 이벤트로 세션에 위임.
    ///
    /// 온라인 공통 규칙 (v1):
    ///   - Swap  : 사람은 상대만 선택 (카드는 블라인드 — 내 첫 카드 ↔ 상대 랜덤 카드). 봇/타임아웃은 자동.
    ///   - Peek  : 사람은 UI로 선택 (OnPeekChoiceRequested → 세션이 라우팅 → SubmitPeekChoice).
    ///   - 라운드 결과창 : 호스트가 onlineRoundResultDuration 후 자동 진행 (버튼 의존 X).
    ///   - 턴 타임아웃  : 사람이 onlineTurnTimeout 안에 안 버리면 방금 뽑은 카드 자동 버림.
    ///   - 중도 이탈    : ConvertSeatToBot(seat)로 그 자리를 봇으로 전환해 계속 진행.
    /// </summary>
    public class CardGameManager : MonoBehaviour
    {
        [Header("Data")]
        [SerializeField] private CardDatabase cardDatabase;

        [Header("게임 설정")]
        [SerializeField, Range(2, 4)] private int playerCount = 4;
        [SerializeField] private int userPlayerIndex = 0;
        [SerializeField] private int startingHandSize = 6;
        [SerializeField] private int completionHandSize = 7;
        [SerializeField] private int winningScore = 30;
        [Tooltip("true: 완성 시 자동 승리가 아니라 [승리 선언] 버튼을 눌러야 승리. false: v9 이전처럼 자동 판정.")]
        [SerializeField] private bool manualWinDeclaration = true;
        [SerializeField] private int randomSeed = 0;

        [Header("타이밍")]
        [SerializeField, Range(0f, 3f)] private float botActionDelay = 0.8f;
        [SerializeField, Range(0f, 3f)] private float roundEndDelay = 1.5f;
        [SerializeField, Range(0f, 3f)] private float specialCardDelay = 0.5f;
        [Tooltip("배패 애니메이션 완료 후 첫 턴 시작까지 대기 시간")]
        [SerializeField, Range(0f, 3f)] private float initialDealDelay = 1.0f;

        [Header("온라인")]
        [Tooltip("온라인: 라운드 결과창을 보여준 뒤 자동으로 다음 라운드로 넘어가는 시간")]
        [SerializeField, Range(1f, 15f)] private float onlineRoundResultDuration = 4f;
        [Tooltip("온라인: 사람 플레이어가 버리기 입력을 안 할 때 자동 버림까지의 시간 (0 = 무제한)")]
        [SerializeField, Range(0f, 120f)] private float onlineTurnTimeout = 30f;
        [Tooltip("온라인: Peek 선택 대기 시간 초과 시 자동 선택")]
        [SerializeField, Range(1f, 60f)] private float onlineChoiceTimeout = 15f;

        [Header("봇 AI")]
        [SerializeField] private bool botAIVerbose = true;

        [Header("UI 연결 (선택)")]
        [SerializeField] private CardGame.UI.RoundResultPanel roundResultPanel;
        [SerializeField] private CardGame.UI.FinalResultPanel finalResultPanel;
        [SerializeField] private CardGame.UI.SwapTargetPanel swapTargetPanel;
        [SerializeField] private CardGame.UI.PeekChoicePanel peekChoicePanel;

        // ---- 공개 상태 ----
        public int PlayerCount => playerCount;
        public int UserPlayerIndex => userPlayerIndex;
        public int WinningScore => winningScore;
        public int CompletionHandSize => completionHandSize;
        public IReadOnlyList<Player> Players => _players;
        public Deck Deck => _deck;
        public GamePhase CurrentPhase => _phase;
        public int CurrentPlayerIndex => _currentPlayerIndex;
        public int RoundNumber => _roundNumber;
        public CardDatabase Database => cardDatabase;
        public bool IsDatabaseReady { get; private set; }

        // ---- 온라인 상태 ----
        public GameRunMode RunMode { get; private set; } = GameRunMode.Local;
        public bool IsOnline => RunMode != GameRunMode.Local;
        public bool IsNetworkHost => RunMode == GameRunMode.NetworkHost;
        public bool IsNetworkClient => RunMode == GameRunMode.NetworkClient;
        public GameSessionConfig SessionConfig => _sessionConfig;
        public CardGame.UI.PeekChoicePanel PeekPanel => peekChoicePanel;
        public CardGame.UI.SwapTargetPanel SwapPanel => swapTargetPanel;
        public int StartingPlayerIndex => _startingPlayerIndex;

        // ---- 턴 타이머 (온라인) ----
        /// <summary>
        /// 타이머 시작/종료 (seat, durationSeconds). seat &lt; 0 이면 종료.
        /// 호스트는 타임아웃을 걸 때, 클라이언트는 ClientApplyTurnTimer로 발생. TurnTimerUI가 구독.
        /// </summary>
        public event Action<int, float> OnTurnTimerChanged;
        /// <summary>타이머가 걸린 좌석. 없으면 -1.</summary>
        public int TurnTimerSeat => _timerSeat;
        public float TurnTimerDuration => _timerDuration;
        public float TurnTimerRemaining => _timerSeat < 0 ? 0f : Mathf.Max(0f, _timerEnd - Time.time);

        // ---- 이번 라운드 버림 기록 (재접속 동기화용) ----
        public struct DiscardRecord { public int Seat; public CardData Card; }
        public IReadOnlyList<DiscardRecord> DiscardLog => _discardLog;

        // ---- 온라인 훅 (NetworkGameSession이 구독) ----
        /// <summary>호스트/로컬: 라운드 시작 (round, startingSeat). 배패 이벤트보다 먼저 발생.</summary>
        public event Action<int, int> OnRoundStarted;
        /// <summary>온라인: 사람 플레이어의 Peek 선택 요청 (seat, 덱 위 카드들).</summary>
        public event Action<int, List<CardData>> OnPeekChoiceRequested;
        /// <summary>온라인: 사람 플레이어의 Swap 상대 선택 요청 (seat).</summary>
        public event Action<int> OnSwapChoiceRequested;
        /// <summary>온라인: 결과창 "확인/나가기" 등으로 세션 종료 요청.</summary>
        public event Action OnLeaveOnlineRequested;
        /// <summary>클라이언트: 로컬 UI가 버리기를 요청함 → 세션이 호스트로 전송.</summary>
        public event Action<CardData> OnClientDiscardRequested;
        /// <summary>클라이언트: 로컬 UI가 완성 체크를 요청함 → 세션이 호스트로 전송.</summary>
        public event Action OnClientCompletionCheckRequested;
        /// <summary>
        /// 승리 선언 가능 여부 변경 (seat, available). 로컬/호스트는 판정 직후, 클라이언트는 ClientApplyWinAvailable로 발생.
        /// UI(WinDeclareButton)는 seat == UserPlayerIndex 일 때만 반응.
        /// </summary>
        public event Action<int, bool> OnWinAvailableChanged;
        /// <summary>클라이언트: 로컬 UI가 승리 선언을 요청함 → 세션이 호스트로 전송.</summary>
        public event Action OnClientWinDeclareRequested;

        public bool ManualWinDeclaration => manualWinDeclaration;
        /// <summary>현재 차례 플레이어가 승리 선언 가능한 상태인지 (로컬/호스트 기준).</summary>
        public bool IsWinAvailable => _winAvailable;
        /// <summary>로컬 사용자가 지금 승리 선언할 수 있는지 (UI용, 모든 모드).</summary>
        public bool CanUserDeclareWin => _winAvailable && _currentPlayerIndex == userPlayerIndex && _phase == GamePhase.WaitingDiscard;

        // ---- 내부 ----
        private readonly List<Player> _players = new List<Player>();
        private readonly Deck _deck = new Deck();
        private System.Random _rng;

        private GamePhase _phase = GamePhase.NotStarted;
        private int _currentPlayerIndex = 0;
        private int _startingPlayerIndex = 0;
        private int _roundNumber = 0;
        private Coroutine _activeCoroutine;
        private Coroutine _timeoutCoroutine;
        private Coroutine _botDeclareCoroutine;
        private bool _winAvailable;

        private int _timerSeat = -1;
        private float _timerDuration;
        private float _timerEnd;
        private readonly List<DiscardRecord> _discardLog = new List<DiscardRecord>();
        private GameResumeData _resume;
        private bool _gameEnded = false;
        private bool _gameRunning = false;

        private SpecialCardEvent _pendingSpecial;
        private BotAI _botAI;

        private GameSessionConfig _sessionConfig;
        private bool _gameStartRequested = false;

        // 클라이언트 복제본용 카드 레지스트리 (InstanceId → 동일 객체 보장)
        private readonly Dictionary<int, CardData> _cardRegistry = new Dictionary<int, CardData>();

        private int _consecutiveEmptyRounds = 0;
        private const int MAX_EMPTY_ROUNDS = 3;
        private const int MAX_TOTAL_ROUNDS = 100;

        // =====================================================================
        //  Lifecycle
        // =====================================================================

        private async void Start()
        {
            if (roundResultPanel != null) roundResultPanel.OnNextRoundClicked += HandleNextRoundRequested;
            if (finalResultPanel != null) finalResultPanel.OnRestartClicked += HandleRestartRequested;
            if (swapTargetPanel != null) swapTargetPanel.OnTargetSelected += HandleSwapTargetSelected;
            if (peekChoicePanel != null) peekChoicePanel.OnCardSelected += HandlePeekCardSelectedFromPanel;

            if (cardDatabase == null)
            {
                Debug.LogError("[CardGame] CardDatabase 미연결!");
                return;
            }

            Debug.Log("[CardGame] Database 로드 확인...");
            await cardDatabase.InitializeAsync();
            if (this == null) return;

            CardGame.UI.CardView.SetDatabase(cardDatabase);
            CardGame.UI3D.Card3DDiscardPileView.SetDatabase(cardDatabase);
            CardGame.UI3D.Card3DCentralDiscardPile.SetDatabase(cardDatabase);
            _deck.SetDatabase(cardDatabase);
            IsDatabaseReady = true;

            bool online = SceneContext.Get<bool>(SceneContextKeys.OnlineSession, false);
            if (!online)
            {
                Debug.Log("[CardGame] Local 모드 — 게임 시작");
                StartGame();
                return;
            }

            Debug.Log("[CardGame] 온라인 세션 — NetworkGameSession 구성 대기");
            TryStartPendingNetworkGame();
        }

        private void OnDestroy()
        {
            if (roundResultPanel != null) roundResultPanel.OnNextRoundClicked -= HandleNextRoundRequested;
            if (finalResultPanel != null) finalResultPanel.OnRestartClicked -= HandleRestartRequested;
            if (swapTargetPanel != null) swapTargetPanel.OnTargetSelected -= HandleSwapTargetSelected;
            if (peekChoicePanel != null) peekChoicePanel.OnCardSelected -= HandlePeekCardSelectedFromPanel;

            cardDatabase?.Release();
            CardGameEvents.Clear();
        }

        // =====================================================================
        //  온라인 구성 (NetworkGameSession 호출)
        // =====================================================================

        /// <summary>온라인 모드 구성. 호스트/클라이언트 모두 StartGame 전에 호출.</summary>
        public void ConfigureNetwork(GameRunMode mode, GameSessionConfig config)
        {
            if (mode == GameRunMode.Local || config == null || config.SeatCount < 2)
            {
                Debug.LogError("[CardGame] ConfigureNetwork 잘못된 인자");
                return;
            }

            RunMode = mode;
            _sessionConfig = config;
            playerCount = config.SeatCount;
            userPlayerIndex = Mathf.Clamp(config.LocalSeat, 0, config.SeatCount - 1);
            if (config.Seed != 0) randomSeed = config.Seed;

            Debug.Log($"[CardGame] 온라인 구성: {mode}, 좌석 {playerCount}, 내 자리 {userPlayerIndex}");

            if (mode == GameRunMode.NetworkClient)
                BuildClientReplica();
        }

        /// <summary>호스트: 모든 클라이언트 준비 완료 시 호출 → DB 준비되면 StartGame.</summary>
        public void RequestStartGame()
        {
            _gameStartRequested = true;
            TryStartPendingNetworkGame();
        }

        private void TryStartPendingNetworkGame()
        {
            if (!IsNetworkHost) return;
            if (!_gameStartRequested || !IsDatabaseReady || _gameRunning) return;
            Debug.Log("[CardGame] 호스트 게임 시작");
            StartGame();
        }

        /// <summary>호스트: 중도 이탈한 자리를 봇으로 전환하고 진행을 이어감.</summary>
        public void ConvertSeatToBot(int seat)
        {
            if (!IsNetworkHost) return;

            // 아직 플레이어 생성 전(준비 대기 중)이면 설정만 바꿔서 StartGame 때 봇으로 생성되게
            if (_sessionConfig != null)
                foreach (var sc in _sessionConfig.Seats)
                    if (sc.Seat == seat) { sc.Type = PlayerType.Bot; sc.DisplayName = $"BOT {seat + 1}"; }

            if (seat < 0 || seat >= _players.Count) return;
            var p = _players[seat];
            if (p.IsBot) return;

            p.Type = PlayerType.Bot;
            p.DisplayName = $"BOT {seat + 1} (이탈)";
            Debug.Log($"[CardGame] 좌석 {seat} → 봇 전환");

            if (_gameEnded) return;
            StopTimeout();

            if (_currentPlayerIndex == seat && _phase == GamePhase.WaitingDiscard && p.Hand.Count == completionHandSize)
            {
                var justDrawn = p.Hand[p.Hand.Count - 1];
                StopActiveCoroutine();
                StopBotDeclare();
                if (manualWinDeclaration && _winAvailable)
                    _botDeclareCoroutine = StartCoroutine(BotDeclareWinCoroutine(p));
                else
                    _activeCoroutine = StartCoroutine(BotDiscardCoroutine(p, justDrawn));
            }
            else if (_phase == GamePhase.WaitingSpecialAction && _pendingSpecial != null && _pendingSpecial.SourcePlayerIndex == seat)
            {
                if (_pendingSpecial.Type == SpecialCardType.Peek) SpecialCardExecutor.ExecutePeekAuto(p, _deck);
                else if (_pendingSpecial.Type == SpecialCardType.Swap) SpecialCardExecutor.ExecuteSwapAuto(p, _players, _rng);
                CompleteSpecialCard();
            }
        }

        /// <summary>
        /// 호스트: 재접속한 플레이어에게 자리를 돌려줌 (봇 → 원격 사람).
        /// 그 자리의 차례가 진행 중이면 봇 동작을 멈추고 사람 입력(타임아웃 포함)으로 전환.
        /// </summary>
        public void RestoreSeatToHuman(int seat, string displayName)
        {
            if (!IsNetworkHost) return;

            if (_sessionConfig != null)
                foreach (var sc in _sessionConfig.Seats)
                    if (sc.Seat == seat)
                    {
                        sc.Type = PlayerType.RemoteHuman;
                        if (!string.IsNullOrEmpty(displayName)) sc.DisplayName = displayName;
                    }

            if (seat < 0 || seat >= _players.Count) return;
            var p = _players[seat];
            p.Type = PlayerType.RemoteHuman;
            if (!string.IsNullOrEmpty(displayName)) p.DisplayName = displayName;
            Debug.Log($"[CardGame] 좌석 {seat} → 사람 복귀 ({p.DisplayName})");

            if (_gameEnded) return;

            if (_currentPlayerIndex == seat && _phase == GamePhase.WaitingDiscard && p.Hand.Count == completionHandSize)
            {
                StopActiveCoroutine();
                StopBotDeclare();
                StopTimeout();

                if (manualWinDeclaration) UpdateWinAvailability(p, "재접속");

                if (onlineTurnTimeout > 0f)
                {
                    var last = p.Hand[p.Hand.Count - 1];
                    BeginTimer(seat, onlineTurnTimeout);
                    _timeoutCoroutine = StartCoroutine(HumanDiscardTimeout(p, last));
                }
            }
        }

        // =====================================================================
        //  호스트 이전 (점수·라운드 유지, 현재 라운드 재시작)
        // =====================================================================

        /// <summary>현재 진행 상황 요약. 클라이언트 복제본에서도 호출 가능 (새 호스트가 될 때 사용).</summary>
        public GameResumeData BuildResumeData()
        {
            if (_players.Count == 0) return null;

            bool roundOver = _phase == GamePhase.RoundFinished;
            var data = new GameResumeData
            {
                // 라운드가 이미 끝났으면 다음 라운드부터, 아니면 지금 라운드를 다시
                Round = Mathf.Max(1, roundOver ? _roundNumber + 1 : _roundNumber),
                StartingSeat = roundOver ? (_startingPlayerIndex + 1) % _players.Count : _startingPlayerIndex,
                Scores = new int[_players.Count],
                ModeIndex = _sessionConfig != null ? _sessionConfig.ModeIndex : 0,
            };

            for (int i = 0; i < _players.Count; i++)
            {
                data.Scores[i] = _players[i].TotalScore;
                var sc = new SeatConfig { Seat = i, Type = _players[i].Type, DisplayName = _players[i].DisplayName };
                if (_sessionConfig != null)
                    foreach (var s in _sessionConfig.Seats)
                        if (s.Seat == i) sc.SteamId = s.SteamId;
                data.Seats.Add(sc);
            }
            return data;
        }

        /// <summary>호스트: StartGame 전에 호출하면 점수·라운드 번호·선을 이어받아 시작.</summary>
        public void SetResumeData(GameResumeData data) => _resume = data;

        private void ApplyResumeData()
        {
            if (_resume == null) return;
            var d = _resume;
            _resume = null;

            if (d.Scores != null)
                for (int i = 0; i < d.Scores.Length && i < _players.Count; i++)
                    _players[i].TotalScore = d.Scores[i];

            _roundNumber = Mathf.Max(0, d.Round - 1);   // StartNextRound가 +1
            _startingPlayerIndex = _players.Count > 0 ? Mathf.Abs(d.StartingSeat) % _players.Count : 0;
            Debug.Log($"[CardGame] 이어하기: 라운드 {d.Round}, 선 P{_startingPlayerIndex}, 점수 [{string.Join(",", d.Scores ?? new int[0])}]");
        }

        /// <summary>클라이언트: 호스트와 연결이 끊김 — 타이머/승리 버튼 등 입력 UI를 잠금.</summary>
        public void NotifyConnectionLost()
        {
            StopActiveCoroutine();
            StopTimeout();
            if (_winAvailable)
            {
                _winAvailable = false;
                OnWinAvailableChanged?.Invoke(_currentPlayerIndex, false);
            }
            swapTargetPanel?.Hide();
            peekChoicePanel?.Hide();
        }

        // =====================================================================
        //  턴 타이머
        // =====================================================================

        private void BeginTimer(int seat, float duration)
        {
            _timerSeat = seat;
            _timerDuration = duration;
            _timerEnd = Time.time + duration;
            OnTurnTimerChanged?.Invoke(seat, duration);
        }

        private void EndTimer()
        {
            if (_timerSeat < 0) return;
            _timerSeat = -1;
            _timerDuration = 0f;
            OnTurnTimerChanged?.Invoke(-1, 0f);
        }

        // =====================================================================
        //  게임 시작 / 라운드
        // =====================================================================

        public void StartGame()
        {
            if (IsNetworkClient)
            {
                Debug.LogWarning("[CardGame] 클라이언트는 StartGame 불가 (호스트 권위)");
                return;
            }

            StopActiveCoroutine();
            StopTimeout();
            _rng = randomSeed == 0 ? new System.Random() : new System.Random(randomSeed);
            _gameEnded = false;
            _gameRunning = true;
            _roundNumber = 0;
            _startingPlayerIndex = 0;
            _consecutiveEmptyRounds = 0;

            _botAI = new BotAI();

            CreatePlayers();
            ApplyResumeData();
            StartNextRound();
        }

        public void StartNextRound()
        {
            StopActiveCoroutine();
            StopTimeout();
            _pendingSpecial = null;
            _roundNumber++;

            if (_roundNumber > MAX_TOTAL_ROUNDS)
            {
                Debug.LogError($"[CardGame] 라운드 수 비정상: {_roundNumber}, 게임 중단");
                _gameEnded = true;
                ChangePhase(GamePhase.GameFinished);
                return;
            }

            foreach (var p in _players) p.ClearHand();
            _discardLog.Clear();
            _botAI?.OnRoundStart();

            _deck.CreateDeck();

            if (_deck.DrawPileCount == 0)
            {
                Debug.LogError("[CardGame] 덱이 비었음! 게임 중단.");
                _gameEnded = true;
                ChangePhase(GamePhase.GameFinished);
                return;
            }

            _deck.Shuffle(_rng);
            _currentPlayerIndex = _startingPlayerIndex;
            Debug.Log($"[CardGame] === 라운드 {_roundNumber} 시작 === (덱: {_deck.DrawPileCount}장, 선: P{_startingPlayerIndex})");

            // ★ 세션 훅: 배패 이벤트보다 먼저
            OnRoundStarted?.Invoke(_roundNumber, _startingPlayerIndex);

            ChangePhase(GamePhase.Dealing);
            CardGameEvents.RaiseDealStarted();

            int dealt = 0;
            for (int i = 0; i < startingHandSize; i++)
            {
                for (int p = 0; p < _players.Count; p++)
                {
                    var card = _deck.DrawTop();
                    if (card == null) break;
                    _players[p].AddCard(card);
                    dealt++;
                }
            }

            int expectedDeal = startingHandSize * _players.Count;
            if (dealt < expectedDeal)
            {
                Debug.LogError($"[CardGame] 배패 실패: {dealt}/{expectedDeal}");
                _gameEnded = true;
                ChangePhase(GamePhase.GameFinished);
                return;
            }

            CardGameEvents.RaiseDealCompleted();

            _activeCoroutine = StartCoroutine(DelayedBeginTurn());
        }

        private IEnumerator DelayedBeginTurn()
        {
            yield return new WaitForSeconds(initialDealDelay);
            _activeCoroutine = null;
            BeginTurn();
        }

        private void HandleNextRoundRequested()
        {
            // 온라인: 버튼 무시 (호스트가 자동 진행, 클라이언트는 권한 없음)
            if (IsOnline) return;
            AdvanceRound();
        }

        private void AdvanceRound()
        {
            _startingPlayerIndex = (_startingPlayerIndex + 1) % _players.Count;
            StartNextRound();
        }

        private void HandleRestartRequested()
        {
            if (IsOnline)
            {
                OnLeaveOnlineRequested?.Invoke();
                return;
            }
            StartGame();
        }

        private void StopActiveCoroutine()
        {
            if (_activeCoroutine != null)
            {
                StopCoroutine(_activeCoroutine);
                _activeCoroutine = null;
            }
        }

        private void StopTimeout()
        {
            if (_timeoutCoroutine != null)
            {
                StopCoroutine(_timeoutCoroutine);
                _timeoutCoroutine = null;
            }
            EndTimer();
        }

        private void StopBotDeclare()
        {
            if (_botDeclareCoroutine != null)
            {
                StopCoroutine(_botDeclareCoroutine);
                _botDeclareCoroutine = null;
            }
        }

        private void CreatePlayers()
        {
            _players.Clear();

            if (_sessionConfig != null)
            {
                foreach (var seat in _sessionConfig.Seats)
                {
                    var p = new Player(seat.Seat, seat.Type);
                    if (!string.IsNullOrEmpty(seat.DisplayName)) p.DisplayName = seat.DisplayName;
                    _players.Add(p);
                }
                _players.Sort((a, b) => a.Index.CompareTo(b.Index));
                Debug.Log($"[CardGame] 온라인 플레이어 {_players.Count}명: {string.Join(", ", _players)}");
                return;
            }

            for (int i = 0; i < playerCount; i++)
            {
                var type = (i == userPlayerIndex) ? PlayerType.Human : PlayerType.Bot;
                _players.Add(new Player(i, type));
            }
            Debug.Log($"[CardGame] 플레이어 {playerCount}명 생성");
        }

        // =====================================================================
        //  턴 진행 (Local / Host)
        // =====================================================================

        private void BeginTurn()
        {
            if (_gameEnded) return;

            CardGameEvents.RaiseTurnStarted(_currentPlayerIndex);
            var player = _players[_currentPlayerIndex];
            Debug.Log($"[CardGame] P{_currentPlayerIndex}({player.Type}) 차례 (손패 {player.Hand.Count}장)");
            DrawForCurrentPlayer();
        }

        private void DrawForCurrentPlayer()
        {
            ChangePhase(GamePhase.WaitingDraw);

            var player = _players[_currentPlayerIndex];
            var card = _deck.DrawTop();

            if (card == null)
            {
                Debug.Log("[CardGame] 덱 소진, 재셔플");
                _deck.ReshuffleDiscardIntoDeck(_rng);
                card = _deck.DrawTop();
                if (card == null)
                {
                    Debug.LogWarning("[CardGame] 카드 없음 → 유국");
                    _consecutiveEmptyRounds++;
                    if (_consecutiveEmptyRounds >= MAX_EMPTY_ROUNDS)
                    {
                        Debug.LogError("[CardGame] 연속 유국 3회, 게임 종료");
                        _gameEnded = true;
                        ChangePhase(GamePhase.GameFinished);
                        return;
                    }
                    AdvanceRound();
                    return;
                }
            }

            _consecutiveEmptyRounds = 0;

            player.AddCard(card);
            CardGameEvents.RaiseCardDrawn(_currentPlayerIndex, card);
            Debug.Log($"[CardGame] P{_currentPlayerIndex} 뽑음: {card} (손패 {player.Hand.Count}장)");

            if (!manualWinDeclaration)
            {
                CheckAndHandleCompletion(player, "뽑기직후");
                if (_phase == GamePhase.RoundFinished || _gameEnded) return;
            }

            EnterDiscardWindow(player, card);
        }

        /// <summary>
        /// 손패가 꽉 찬(completionHandSize) 상태로 자기 차례의 "버리기 대기"에 들어감.
        /// 여기서 승리 선언 가능 여부를 판정하고, 봇 처리/타임아웃을 건다.
        /// </summary>
        private void EnterDiscardWindow(Player player, CardData justDrawn)
        {
            ChangePhase(GamePhase.WaitingDiscard);

            if (manualWinDeclaration)
                UpdateWinAvailability(player, "뽑기직후");

            if (player.IsBot)
            {
                if (_winAvailable)
                    _botDeclareCoroutine = StartCoroutine(BotDeclareWinCoroutine(player));
                else
                    _activeCoroutine = StartCoroutine(BotDiscardCoroutine(player, justDrawn));
            }
            else if (IsOnline && onlineTurnTimeout > 0f)
            {
                // 사람(로컬/원격) 턴 타임아웃
                BeginTimer(player.Index, onlineTurnTimeout);
                _timeoutCoroutine = StartCoroutine(HumanDiscardTimeout(player, justDrawn));
            }
        }

        // =====================================================================
        //  승리 선언
        // =====================================================================

        private void UpdateWinAvailability(Player player, string context)
        {
            bool available = false;
            if (player.Hand.Count == completionHandSize)
            {
                var result = HandAnalyzer.Analyze(player.Hand);
                available = result.IsComplete;
                Debug.Log($"[승리가능/{context}] P{player.Index} IsComplete={result.IsComplete}, Pattern={result.Pattern}");
            }
            SetWinAvailable(player.Index, available);
        }

        private void SetWinAvailable(int seat, bool available)
        {
            if (_winAvailable == available && !available) return;
            _winAvailable = available;
            OnWinAvailableChanged?.Invoke(seat, available);
        }

        private void ClearWinAvailable()
        {
            if (!_winAvailable) return;
            _winAvailable = false;
            OnWinAvailableChanged?.Invoke(_currentPlayerIndex, false);
        }

        /// <summary>로컬 사용자의 승리 선언 (버튼). 클라이언트는 호스트로 위임.</summary>
        public bool DeclareWin()
        {
            if (IsNetworkClient)
            {
                if (!CanUserDeclareWin) return false;
                OnClientWinDeclareRequested?.Invoke();
                return true;
            }
            return DeclareWinForSeat(userPlayerIndex);
        }

        /// <summary>호스트/로컬: 특정 좌석의 승리 선언. 차례·페이즈·완성 재검증.</summary>
        public bool DeclareWinForSeat(int seat)
        {
            if (IsNetworkClient) return false;
            if (_gameEnded) return false;
            if (_phase != GamePhase.WaitingDiscard) return false;
            if (seat != _currentPlayerIndex) return false;
            if (seat < 0 || seat >= _players.Count) return false;

            var player = _players[seat];
            if (player.Hand.Count != completionHandSize) return false;

            var result = HandAnalyzer.Analyze(player.Hand);
            if (!result.IsComplete)
            {
                Debug.LogWarning($"[CardGame] P{seat} 승리 선언 거부: 완성 아님");
                SetWinAvailable(seat, false);
                return false;
            }

            Debug.Log($"[CardGame] ★★★ P{seat} 승리 선언! {CompletionResult.GetPatternName(result.Pattern)}");
            _winAvailable = false;
            OnWinAvailableChanged?.Invoke(seat, false);
            HandleCompletion(player, result);
            return true;
        }

        private IEnumerator BotDeclareWinCoroutine(Player player)
        {
            if (botActionDelay > 0f)
                yield return new WaitForSeconds(botActionDelay);
            _botDeclareCoroutine = null;
            if (_phase != GamePhase.WaitingDiscard || _players[_currentPlayerIndex] != player) yield break;
            DeclareWinForSeat(player.Index);
        }

        private IEnumerator HumanDiscardTimeout(Player player, CardData justDrawn)
        {
            yield return new WaitForSeconds(onlineTurnTimeout);
            _timeoutCoroutine = null;
            EndTimer();

            if (_phase != GamePhase.WaitingDiscard) yield break;
            if (_players[_currentPlayerIndex] != player) yield break;
            if (!player.Hand.Contains(justDrawn)) justDrawn = player.Hand[player.Hand.Count - 1];

            if (manualWinDeclaration && _winAvailable)
            {
                Debug.LogWarning($"[CardGame] P{player.Index} 턴 타임아웃 → 자동 승리 선언");
                if (DeclareWinForSeat(player.Index)) yield break;
            }

            Debug.LogWarning($"[CardGame] P{player.Index} 턴 타임아웃 → 자동 버림: {justDrawn}");
            ExecuteDiscard(player, justDrawn);
        }

        private void CheckAndHandleCompletion(Player player, string context)
        {
            if (player.Hand.Count != completionHandSize)
            {
                Debug.Log($"[완성체크/{context}] P{player.Index} SKIP: 손패 {player.Hand.Count}장 (필요 {completionHandSize}장)");
                return;
            }

            string handStr = string.Join(", ", player.Hand);
            int specialCount = 0;
            foreach (var c in player.Hand)
                if (c.Kind.IsSpecial && c.Kind.Special != SpecialCardType.Wild) specialCount++;

            if (specialCount > 0)
                Debug.LogWarning($"[완성체크/{context}] P{player.Index} 특수카드 {specialCount}장 → 완성 불가. 손패=[{handStr}]");

            var result = HandAnalyzer.Analyze(player.Hand);
            Debug.Log($"[완성체크/{context}] P{player.Index} 결과: IsComplete={result.IsComplete}, Pattern={result.Pattern}, Points={result.Points}, 손패=[{handStr}]");

            if (result.IsComplete)
            {
                Debug.Log($"[CardGame] ★★★ P{player.Index} 완성! {CompletionResult.GetPatternName(result.Pattern)}");
                HandleCompletion(player, result);
            }
        }

        /// <summary>로컬 사용자의 완성 체크 (드래그 후 등). 클라이언트는 호스트로 위임.</summary>
        public void TryUserCompletionCheck()
        {
            if (manualWinDeclaration) return;   // v10: 자동 판정 없음. 버튼(DeclareWin)으로만 승리.
            if (IsNetworkClient)
            {
                OnClientCompletionCheckRequested?.Invoke();
                return;
            }
            TryCompletionCheckForSeat(userPlayerIndex);
        }

        /// <summary>호스트/로컬: 특정 좌석 완성 체크.</summary>
        public void TryCompletionCheckForSeat(int seat)
        {
            if (manualWinDeclaration) return;
            if (IsNetworkClient) return;
            if (_gameEnded) return;
            if (_phase == GamePhase.RoundFinished || _phase == GamePhase.GameFinished) return;
            if (seat < 0 || seat >= _players.Count) return;

            CheckAndHandleCompletion(_players[seat], "사용자요청");
        }

        private IEnumerator BotDiscardCoroutine(Player player, CardData justDrawn)
        {
            if (botActionDelay > 0f)
                yield return new WaitForSeconds(botActionDelay);

            if (_phase != GamePhase.WaitingDiscard) yield break;
            if (player.Hand.Count != completionHandSize) yield break;

            CardData toDiscard = null;
            if (_botAI != null) toDiscard = _botAI.ChooseDiscard(player, _players);

            if (toDiscard == null || !player.Hand.Contains(toDiscard))
                toDiscard = justDrawn != null && player.Hand.Contains(justDrawn) ? justDrawn : player.Hand[player.Hand.Count - 1];

            Debug.Log($"[Bot] P{player.Index} 버림: {toDiscard}");
            ExecuteDiscard(player, toDiscard);
            _activeCoroutine = null;
        }

        /// <summary>
        /// 버리기 요청 (로컬 UI 또는 세션이 원격 입력을 받아 호출).
        /// 클라이언트 모드에선 세션 이벤트로 위임하고 낙관적으로 true 반환.
        /// </summary>
        public bool RequestDiscard(CardData card)
        {
            if (_phase != GamePhase.WaitingDiscard) return false;
            if (card == null) return false;

            if (IsNetworkClient)
            {
                if (_currentPlayerIndex != userPlayerIndex) return false;
                var me = _players[userPlayerIndex];
                if (!me.Hand.Contains(card)) return false;
                if (me.Hand.Count != completionHandSize) return false;
                OnClientDiscardRequested?.Invoke(card);
                return true;
            }

            var player = _players[_currentPlayerIndex];
            if (player.IsBot) return false;
            if (!player.Hand.Contains(card)) return false;
            if (player.Hand.Count != completionHandSize) return false;

            Debug.Log($"[User] P{player.Index} 버림: {card}");
            ExecuteDiscard(player, card);
            return true;
        }

        /// <summary>호스트: 특정 좌석(원격 플레이어)의 버리기 요청. 차례·소유 검증 포함.</summary>
        public bool RequestDiscardForSeat(int seat, int instanceId)
        {
            if (!IsNetworkHost) return false;
            if (seat != _currentPlayerIndex) return false;
            if (seat < 0 || seat >= _players.Count) return false;
            var card = _players[seat].FindCard(instanceId);
            if (card == null) return false;
            return RequestDiscard(card);
        }

        private void ExecuteDiscard(Player player, CardData card)
        {
            StopTimeout();
            StopBotDeclare();
            ClearWinAvailable();

            player.RemoveCard(card);
            _deck.AddToDiscard(card);
            _discardLog.Add(new DiscardRecord { Seat = player.Index, Card = card });
            CardGameEvents.RaiseCardDiscarded(player.Index, card);
            _botAI?.OnCardDiscarded(player.Index, card);

            if (card.Kind.IsSpecial && card.Kind.Special != SpecialCardType.Wild)
            {
                TriggerSpecialCard(player, card.Kind.Special);
                return;
            }

            if (!manualWinDeclaration)
            {
                CheckAndHandleCompletion(player, "버린후");
                if (_phase == GamePhase.RoundFinished || _gameEnded) return;
            }

            AdvanceToNextPlayer();
        }

        // =====================================================================
        //  특수 카드
        // =====================================================================

        private void TriggerSpecialCard(Player source, SpecialCardType type)
        {
            Debug.Log($"[Special] P{source.Index} 발동: {type}");
            _pendingSpecial = new SpecialCardEvent
            {
                SourcePlayerIndex = source.Index,
                Type = type
            };
            CardGameEvents.RaiseSpecialCardTriggered(_pendingSpecial);

            switch (type)
            {
                case SpecialCardType.Swap: HandleSwapCard(source); break;
                case SpecialCardType.Peek: HandlePeekCard(source); break;
                case SpecialCardType.Chaos: HandleChaosCard(source); break;
            }
        }

        private void HandleSwapCard(Player source)
        {
            if (source.IsBot)
            {
                _activeCoroutine = StartCoroutine(BotSwapCoroutine(source));
                return;
            }

            if (IsOnline)
            {
                // 사람: 상대만 선택. 세션이 로컬 패널 또는 TargetRpc로 라우팅 → SubmitSwapTarget.
                ChangePhase(GamePhase.WaitingSpecialAction);
                OnSwapChoiceRequested?.Invoke(source.Index);
                BeginTimer(source.Index, onlineChoiceTimeout);
                _timeoutCoroutine = StartCoroutine(SwapChoiceTimeout(source));
                return;
            }

            ChangePhase(GamePhase.WaitingSpecialAction);
            if (swapTargetPanel != null) swapTargetPanel.Show(source, _players);
            else
            {
                SpecialCardExecutor.ExecuteSwapAuto(source, _players, _rng);
                CompleteSpecialCard();
            }
        }

        private IEnumerator BotSwapCoroutine(Player source)
        {
            yield return new WaitForSeconds(specialCardDelay);
            SpecialCardExecutor.ExecuteSwapAuto(source, _players, _rng);
            CompleteSpecialCard();
            _activeCoroutine = null;
        }

        private IEnumerator SwapChoiceTimeout(Player source)
        {
            yield return new WaitForSeconds(onlineChoiceTimeout);
            _timeoutCoroutine = null;
            EndTimer();
            if (_phase != GamePhase.WaitingSpecialAction) yield break;
            if (_pendingSpecial == null || _pendingSpecial.SourcePlayerIndex != source.Index) yield break;

            Debug.LogWarning($"[CardGame] P{source.Index} Swap 선택 타임아웃 → 자동");
            SpecialCardExecutor.ExecuteSwapAuto(source, _players, _rng);
            CompleteSpecialCard();
        }

        /// <summary>
        /// 온라인(호스트): 좌석의 Swap 상대 선택 제출. targetSeat &lt; 0 이면 랜덤.
        /// 카드는 블라인드 (싱글플레이 패널과 동일: 내 첫 카드 ↔ 상대 랜덤 카드).
        /// </summary>
        public bool SubmitSwapTarget(int seat, int targetSeat)
        {
            if (IsNetworkClient) return false;
            if (_phase != GamePhase.WaitingSpecialAction) return false;
            if (_pendingSpecial == null || _pendingSpecial.SourcePlayerIndex != seat) return false;
            if (_pendingSpecial.Type != SpecialCardType.Swap) return false;
            if (seat < 0 || seat >= _players.Count) return false;

            var source = _players[seat];

            if (targetSeat < 0)
            {
                StopTimeout();
                SpecialCardExecutor.ExecuteSwapAuto(source, _players, _rng);
                CompleteSpecialCard();
                return true;
            }

            if (targetSeat >= _players.Count || targetSeat == seat) return false;
            var target = _players[targetSeat];
            if (target.Hand.Count == 0 || source.Hand.Count == 0) return false;

            StopTimeout();
            var sourceCard = source.Hand[0];
            var targetCard = target.Hand[_rng.Next(target.Hand.Count)];
            SpecialCardExecutor.ExecuteSwap(source, target, sourceCard, targetCard);
            _pendingSpecial.TargetPlayerIndex = target.Index;
            CompleteSpecialCard();
            return true;
        }

        private void HandleSwapTargetSelected(Player target, CardData sourceCard, CardData targetCard)
        {
            if (IsOnline) return;
            if (_pendingSpecial == null) return;
            var source = _players[_pendingSpecial.SourcePlayerIndex];
            SpecialCardExecutor.ExecuteSwap(source, target, sourceCard, targetCard);
            _pendingSpecial.TargetPlayerIndex = target.Index;
            CompleteSpecialCard();
        }

        private void HandlePeekCard(Player source)
        {
            if (source.IsBot)
            {
                _activeCoroutine = StartCoroutine(BotPeekCoroutine(source));
                return;
            }

            ChangePhase(GamePhase.WaitingSpecialAction);
            var topCards = SpecialCardExecutor.PeekTop(_deck, 3);

            if (topCards.Count == 0)
            {
                SpecialCardExecutor.ExecutePeekAuto(source, _deck);
                CompleteSpecialCard();
                return;
            }

            if (IsOnline)
            {
                // 세션이 로컬이면 패널 표시, 원격이면 TargetRpc 로 전달
                OnPeekChoiceRequested?.Invoke(source.Index, topCards);
                BeginTimer(source.Index, onlineChoiceTimeout);
                _timeoutCoroutine = StartCoroutine(PeekChoiceTimeout(source));
                return;
            }

            if (peekChoicePanel != null) peekChoicePanel.Show(topCards);
            else
            {
                SpecialCardExecutor.ExecutePeekAuto(source, _deck);
                CompleteSpecialCard();
            }
        }

        private IEnumerator PeekChoiceTimeout(Player source)
        {
            yield return new WaitForSeconds(onlineChoiceTimeout);
            _timeoutCoroutine = null;
            EndTimer();
            if (_phase != GamePhase.WaitingSpecialAction) yield break;
            if (_pendingSpecial == null || _pendingSpecial.SourcePlayerIndex != source.Index) yield break;

            Debug.LogWarning($"[CardGame] P{source.Index} Peek 선택 타임아웃 → 자동");
            SpecialCardExecutor.ExecutePeekAuto(source, _deck);
            CompleteSpecialCard();
        }

        private IEnumerator BotPeekCoroutine(Player source)
        {
            yield return new WaitForSeconds(specialCardDelay);
            SpecialCardExecutor.ExecutePeekAuto(source, _deck);
            CompleteSpecialCard();
            _activeCoroutine = null;
        }

        /// <summary>로컬 패널 이벤트 (Local 모드 전용). 온라인은 세션이 SubmitPeekChoice 호출.</summary>
        private void HandlePeekCardSelectedFromPanel(CardData chosen)
        {
            if (IsOnline) return;
            ApplyPeekChoice(chosen);
        }

        /// <summary>온라인(호스트): 좌석의 Peek 선택 제출. 검증 후 적용.</summary>
        public bool SubmitPeekChoice(int seat, CardData chosen)
        {
            if (IsNetworkClient) return false;
            if (_phase != GamePhase.WaitingSpecialAction) return false;
            if (_pendingSpecial == null || _pendingSpecial.SourcePlayerIndex != seat) return false;
            if (_pendingSpecial.Type != SpecialCardType.Peek) return false;
            if (chosen == null) return false;

            StopTimeout();
            ApplyPeekChoice(chosen);
            return true;
        }

        /// <summary>온라인(호스트): InstanceId로 Peek 선택 제출.</summary>
        public bool SubmitPeekChoiceForSeat(int seat, int instanceId)
        {
            var card = _deck.FindInDrawPile(instanceId);
            return SubmitPeekChoice(seat, card);
        }

        private void ApplyPeekChoice(CardData chosen)
        {
            if (_pendingSpecial == null) return;
            var source = _players[_pendingSpecial.SourcePlayerIndex];
            SpecialCardExecutor.ExecutePeekChoice(source, _deck, chosen);
            _pendingSpecial.ChosenCard = chosen;
            CompleteSpecialCard();
        }

        private void HandleChaosCard(Player source)
        {
            _activeCoroutine = StartCoroutine(ChaosCoroutine());
        }

        private IEnumerator ChaosCoroutine()
        {
            yield return new WaitForSeconds(specialCardDelay);
            SpecialCardExecutor.ExecuteChaos(_players);
            yield return new WaitForSeconds(specialCardDelay);
            CompleteSpecialCard();
            _activeCoroutine = null;
        }

        private void CompleteSpecialCard()
        {
            if (_pendingSpecial == null) return;

            var ev = _pendingSpecial;
            _pendingSpecial = null;
            var source = _players[ev.SourcePlayerIndex];

            if (IsOnline)
            {
                // 타임아웃/이탈로 자동 처리된 경우 열려 있던 호스트 쪽 선택창 정리
                StopTimeout();
                swapTargetPanel?.Hide();
                peekChoicePanel?.Hide();
            }

            CardGameEvents.RaiseSpecialCardCompleted(ev);

            if (!manualWinDeclaration)
            {
                CheckAndHandleCompletion(source, "특수카드후");
                if (_phase == GamePhase.RoundFinished || _gameEnded) return;
            }

            if (source.Hand.Count == completionHandSize)
            {
                var last = source.Hand[source.Hand.Count - 1];
                EnterDiscardWindow(source, last);
            }
            else
            {
                AdvanceToNextPlayer();
            }
        }

        // =====================================================================
        //  완성 / 종료
        // =====================================================================

        private void HandleCompletion(Player player, CompletionResult result)
        {
            Debug.Log($"[CardGame] HandleCompletion: P{player.Index}, Pattern={result.Pattern}, +{result.Points}점");
            StopTimeout();
            StopBotDeclare();
            StopActiveCoroutine();
            _winAvailable = false;

            player.LastRoundPattern = result.Pattern;
            player.TotalScore += result.Points;
            Debug.Log($"[CardGame] P{player.Index} 총점: {player.TotalScore}/{winningScore}");

            CardGameEvents.RaiseCompletion(player.Index, result);
            ChangePhase(GamePhase.RoundFinished);

            if (player.TotalScore >= winningScore)
            {
                EndGame(player.Index);
                return;
            }

            if (roundResultPanel != null)
                roundResultPanel.Show(player, result, _players, winningScore);

            if (IsOnline)
                _activeCoroutine = StartCoroutine(OnlineAutoNextRound());
            else if (roundResultPanel == null)
                _activeCoroutine = StartCoroutine(AutoNextRoundCoroutine());
        }

        private IEnumerator OnlineAutoNextRound()
        {
            yield return new WaitForSeconds(onlineRoundResultDuration);
            _activeCoroutine = null;
            if (_gameEnded) yield break;
            roundResultPanel?.Hide();
            AdvanceRound();
        }

        private IEnumerator AutoNextRoundCoroutine()
        {
            yield return new WaitForSeconds(roundEndDelay);
            _activeCoroutine = null;
            AdvanceRound();
        }

        private void EndGame(int winnerIndex)
        {
            _gameEnded = true;
            _gameRunning = false;
            StopActiveCoroutine();
            StopTimeout();
            ChangePhase(GamePhase.GameFinished);

            Debug.Log($"[CardGame] === 게임 종료! 승자: P{winnerIndex} ===");
            foreach (var p in _players) Debug.Log($"  {p}");

            CardGameEvents.RaiseGameFinished(winnerIndex);

            if (finalResultPanel != null)
                finalResultPanel.Show(_players, winnerIndex);
            else
                Debug.LogWarning("[CardGame] FinalResultPanel NULL! Inspector 연결 필요.");
        }

        private void AdvanceToNextPlayer()
        {
            _currentPlayerIndex = (_currentPlayerIndex + 1) % _players.Count;
            BeginTurn();
        }

        private void ChangePhase(GamePhase next)
        {
            if (_phase == next) return;
            var prev = _phase;
            _phase = next;
            CardGameEvents.RaisePhaseChanged(prev, next);
        }

        // =====================================================================
        //  클라이언트 복제본 (NetworkClient 전용) — NetworkGameSession이 호출
        //  각 메서드는 상태를 반영한 뒤 기존 UI가 구독하는 CardGameEvents를 재발행.
        // =====================================================================

        private void BuildClientReplica()
        {
            _gameEnded = false;
            _gameRunning = true;
            _roundNumber = 0;
            _cardRegistry.Clear();
            CreatePlayers();
            _deck.ClearReplica();
        }

        /// <summary>클라이언트: NetCard → 동일 CardData 객체 (UI가 참조 비교하므로 캐시 필수).</summary>
        public CardData ResolveCard(int defIndex, int instanceId)
        {
            if (instanceId < 0) return null;
            if (_cardRegistry.TryGetValue(instanceId, out var existing)) return existing;

            var def = cardDatabase != null ? cardDatabase.GetDefinitionByIndex(defIndex) : null;
            if (def == null)
            {
                Debug.LogWarning($"[CardGame] ResolveCard: defIndex {defIndex} 없음");
                return null;
            }
            var card = new CardData(def) { InstanceId = instanceId };
            _cardRegistry[instanceId] = card;
            return card;
        }

        /// <summary>호스트: NetCard 직렬화용 defIndex 조회.</summary>
        public int GetDefIndex(CardData card)
        {
            if (card == null || card.Definition == null || cardDatabase == null) return -1;
            return cardDatabase.GetDefinitionIndex(card.Definition);
        }

        public void ClientApplyRoundStart(int round, int startingSeat)
        {
            StopActiveCoroutine();
            StopTimeout();
            _pendingSpecial = null;
            _roundNumber = round;
            _startingPlayerIndex = startingSeat;
            _currentPlayerIndex = startingSeat;
            _cardRegistry.Clear();

            foreach (var p in _players) p.ClearHand();
            _deck.ClearReplica();
            roundResultPanel?.Hide();

            Debug.Log($"[CardGame/Client] 라운드 {round} 시작 (선 P{startingSeat})");
            ChangePhase(GamePhase.Dealing);
            CardGameEvents.RaiseDealStarted();
        }

        /// <summary>클라이언트: 내 손패 스냅샷 (순서 보존 병합 — 드래그 정렬 유지).</summary>
        public void ClientApplyHandSnapshot(int seat, List<CardData> cards)
        {
            if (seat < 0 || seat >= _players.Count || cards == null) return;
            var hand = _players[seat].Hand;

            for (int i = hand.Count - 1; i >= 0; i--)
                if (hand[i] == null || hand[i].IsPlaceholder || !cards.Contains(hand[i]))
                    hand.RemoveAt(i);

            foreach (var c in cards)
                if (c != null && !hand.Contains(c)) hand.Add(c);

            CardGameEvents.RaiseHandChanged(seat);
        }

        /// <summary>클라이언트: 상대 손패 장수만 맞춤 (플레이스홀더).</summary>
        public void ClientApplyHiddenHandCount(int seat, int count)
        {
            if (seat < 0 || seat >= _players.Count) return;
            if (seat == userPlayerIndex) return;
            var hand = _players[seat].Hand;

            bool changed = false;
            while (hand.Count > count) { hand.RemoveAt(hand.Count - 1); changed = true; }
            while (hand.Count < count) { hand.Add(CardData.CreatePlaceholder()); changed = true; }

            if (changed) CardGameEvents.RaiseHandChanged(seat);
        }

        public void ClientApplyDealCompleted()
        {
            CardGameEvents.RaiseDealCompleted();
        }

        public void ClientApplyPhase(GamePhase phase) => ChangePhase(phase);

        public void ClientApplyTurnStarted(int seat)
        {
            if (_winAvailable) { _winAvailable = false; OnWinAvailableChanged?.Invoke(_currentPlayerIndex, false); }
            _currentPlayerIndex = seat;
            CardGameEvents.RaiseTurnStarted(seat);
        }

        public void ClientApplyDeckCount(int drawCount)
        {
            _deck.ReplicaDrawCount = Mathf.Max(0, drawCount);
            CardGameEvents.RaiseDeckCountChanged(_deck.DrawPileCount);
        }

        /// <summary>클라이언트: 카드 뽑음. 내 카드면 실제 카드, 남의 카드면 null(플레이스홀더 추가).</summary>
        public void ClientApplyCardDrawn(int seat, CardData cardOrNull)
        {
            if (seat < 0 || seat >= _players.Count) return;
            var p = _players[seat];

            CardData shown;
            if (cardOrNull != null)
            {
                if (!p.Hand.Contains(cardOrNull)) p.AddCard(cardOrNull);
                shown = cardOrNull;
            }
            else
            {
                shown = CardData.CreatePlaceholder();
                p.AddCard(shown);
            }

            CardGameEvents.RaiseCardDrawn(seat, shown);
        }

        public void ClientApplyCardDiscarded(int seat, CardData card)
        {
            if (_winAvailable) { _winAvailable = false; OnWinAvailableChanged?.Invoke(seat, false); }
            if (seat < 0 || seat >= _players.Count || card == null) return;
            var p = _players[seat];

            if (!p.RemoveCard(card))
            {
                // 남의 패: 플레이스홀더 하나 제거
                for (int i = p.Hand.Count - 1; i >= 0; i--)
                {
                    if (p.Hand[i] != null && p.Hand[i].IsPlaceholder)
                    {
                        p.Hand.RemoveAt(i);
                        CardGameEvents.RaiseHandChanged(seat);
                        break;
                    }
                }
            }

            _deck.AddToDiscard(card);
            CardGameEvents.RaiseCardDiscarded(seat, card);
        }

        public void ClientApplyScores(int[] scores)
        {
            if (scores == null) return;
            for (int i = 0; i < scores.Length && i < _players.Count; i++)
                _players[i].TotalScore = scores[i];
        }

        /// <summary>클라이언트: 호스트가 보낸 "지금 승리 선언 가능" 신호 (TargetRpc, 본인에게만).</summary>
        public void ClientApplyWinAvailable(int seat, bool available)
        {
            _winAvailable = available;
            OnWinAvailableChanged?.Invoke(seat, available);
        }

        public void ClientApplyCompletion(int seat, CompletionPattern pattern, int points, int[] scores)
        {
            if (seat < 0 || seat >= _players.Count) return;
            _winAvailable = false;
            OnWinAvailableChanged?.Invoke(seat, false);
            StopActiveCoroutine();
            StopTimeout();

            ClientApplyScores(scores);
            var p = _players[seat];
            p.LastRoundPattern = pattern;

            var result = new CompletionResult { Pattern = pattern, Points = points };
            CardGameEvents.RaiseCompletion(seat, result);
            ChangePhase(GamePhase.RoundFinished);

            if (p.TotalScore >= winningScore) return;  // GameFinished RPC가 따로 옴

            if (roundResultPanel != null)
                roundResultPanel.Show(p, result, _players, winningScore);
        }

        public void ClientApplyGameFinished(int winner, int[] scores)
        {
            ClientApplyScores(scores);
            _gameEnded = true;
            _gameRunning = false;
            StopActiveCoroutine();
            StopTimeout();
            ChangePhase(GamePhase.GameFinished);
            roundResultPanel?.Hide();

            CardGameEvents.RaiseGameFinished(winner);
            if (finalResultPanel != null) finalResultPanel.Show(_players, winner);
        }

        public void ClientApplySpecialTriggered(int seat, SpecialCardType type)
        {
            var ev = new SpecialCardEvent { SourcePlayerIndex = seat, Type = type };
            _pendingSpecial = ev;
            CardGameEvents.RaiseSpecialCardTriggered(ev);
        }

        public void ClientApplySpecialCompleted(int seat, SpecialCardType type)
        {
            var ev = _pendingSpecial ?? new SpecialCardEvent { SourcePlayerIndex = seat, Type = type };
            _pendingSpecial = null;
            swapTargetPanel?.Hide();
            peekChoicePanel?.Hide();
            CardGameEvents.RaiseSpecialCardCompleted(ev);
        }

        /// <summary>클라이언트: 좌석이 봇으로 전환됨 (이탈).</summary>
        public void ClientApplySeatBecameBot(int seat)
        {
            if (seat < 0 || seat >= _players.Count) return;
            _players[seat].Type = PlayerType.Bot;
            _players[seat].DisplayName = $"BOT {seat + 1} (이탈)";
        }

        /// <summary>클라이언트: 좌석에 사람이 복귀함 (재접속).</summary>
        public void ClientApplySeatBecameHuman(int seat, string displayName)
        {
            if (seat < 0 || seat >= _players.Count) return;
            _players[seat].Type = seat == userPlayerIndex ? PlayerType.Human : PlayerType.RemoteHuman;
            if (!string.IsNullOrEmpty(displayName)) _players[seat].DisplayName = displayName;
        }

        /// <summary>클라이언트: 턴 타이머 (seat &lt; 0 이면 종료). remaining은 남은 시간.</summary>
        public void ClientApplyTurnTimer(int seat, float remaining)
        {
            if (seat < 0 || remaining <= 0f) EndTimer();
            else BeginTimer(seat, remaining);
        }

        /// <summary>
        /// 클라이언트: 진행 중인 판의 전체 상태 (재접속 직후 1회).
        /// 뷰를 초기화한 뒤 버림 기록 → 손패 → 덱 → 차례 순으로 다시 채움.
        /// </summary>
        public void ClientApplyFullState(
            int round, int startingSeat, int currentSeat, GamePhase phase,
            int[] scores, int[] handCounts, List<CardData> myHand,
            int[] discardSeats, List<CardData> discardCards, int deckCount)
        {
            StopActiveCoroutine();
            StopTimeout();
            _pendingSpecial = null;
            _winAvailable = false;
            _gameEnded = false;
            _gameRunning = true;
            _roundNumber = round;
            _startingPlayerIndex = startingSeat;
            _currentPlayerIndex = currentSeat;

            foreach (var p in _players) p.ClearHand();
            _deck.ClearReplica();
            roundResultPanel?.Hide();

            Debug.Log($"[CardGame/Client] 전체 상태 수신 — 라운드 {round}, 차례 P{currentSeat}, {phase}");

            // 1) 뷰 초기화
            ChangePhase(GamePhase.Dealing);
            CardGameEvents.RaiseDealStarted();

            ClientApplyScores(scores);

            // 2) 버림 기록 재생 (중앙 더미·열람 패널 복구)
            if (discardSeats != null && discardCards != null)
            {
                int n = Mathf.Min(discardSeats.Length, discardCards.Count);
                for (int i = 0; i < n; i++)
                {
                    if (discardCards[i] == null) continue;
                    _deck.AddToDiscard(discardCards[i]);
                    CardGameEvents.RaiseCardDiscarded(discardSeats[i], discardCards[i]);
                }
            }

            // 3) 손패
            if (myHand != null) ClientApplyHandSnapshot(userPlayerIndex, myHand);
            if (handCounts != null)
                for (int s = 0; s < handCounts.Length; s++)
                    if (s != userPlayerIndex) ClientApplyHiddenHandCount(s, handCounts[s]);

            ClientApplyDeckCount(deckCount);
            CardGameEvents.RaiseDealCompleted();

            // 4) 진행 상태
            if (phase == GamePhase.GameFinished)
            {
                int winner = 0;
                for (int i = 1; i < _players.Count; i++)
                    if (_players[i].TotalScore > _players[winner].TotalScore) winner = i;
                ClientApplyGameFinished(winner, scores);
                return;
            }

            CardGameEvents.RaiseTurnStarted(currentSeat);
            ChangePhase(phase);
        }

        // =====================================================================
        //  Debug
        // =====================================================================

        [ContextMenu("Debug: Print Players")]
        public void DebugPrintPlayers()
        {
            foreach (var p in _players)
                Debug.Log($"{p}, 손패: {string.Join(",", p.Hand)}");
        }

        [ContextMenu("Debug: Force Completion Check")]
        public void DebugForceCompletionCheck() => TryUserCompletionCheck();
    }
}

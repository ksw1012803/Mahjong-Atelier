using System.Collections;
using System.Collections.Generic;
using Mirror;
using UnityEngine;
using CardGame.Core;
using CardGame.UI;

namespace CardGame.Network
{
    /// <summary>
    /// CardGameManager ↔ Mirror 브릿지. CardGameScene 진입 시 호스트가 스폰 (sessionPrefab).
    ///
    /// 호스트:
    ///   - NetworkGamePlayer 목록으로 좌석 구성 → CardGameManager.ConfigureNetwork(NetworkHost)
    ///   - CardGameEvents / 매니저 이벤트를 구독해 RPC로 중계 (각 클라이언트에는 자기 패만)
    ///   - 클라이언트 Command(버리기/완성체크/Peek선택)를 검증 후 매니저에 전달
    /// 클라이언트:
    ///   - 같은 좌석 구성으로 CardGameManager.ConfigureNetwork(NetworkClient)
    ///   - RPC 수신 → ClientApply*() 호출 → 기존 UI가 그대로 반응
    ///   - 로컬 UI 입력 이벤트 → Command 전송
    ///
    /// v2 추가:
    ///   - 턴 타이머 중계 (RpcTurnTimer), 라운드 시작 시 점수 동기화 (RpcScores)
    ///   - Swap 상대 선택 라우팅 (호스트 본인 → 로컬 패널, 원격 → TargetSwapChoice → CmdSwapTarget)
    ///   - 재접속: 게임 시작 뒤에 준비 신호가 온 클라이언트에게 전체 상태(TargetFullState)를 보내고 자리를 사람으로 복구
    ///   - 호스트 이전: 새 호스트의 세션이 CardGameNetworkManager.ConsumeResumeData()로 점수·라운드를 이어받음
    ///
    /// 프리팹: 빈 GameObject + NetworkIdentity + NetworkGameSession. Registered Spawnable Prefabs에 등록.
    /// </summary>
    public class NetworkGameSession : NetworkBehaviour
    {
        public static NetworkGameSession Instance { get; private set; }

        [Header("Settings")]
        [Tooltip("모든 클라이언트 준비를 기다리는 최대 시간(초). 초과 시 그냥 시작.")]
        [SerializeField] private float readyTimeout = 20f;

        [SyncVar] private int _seatCount = 4;
        [SyncVar] private int _humanCount = 1;
        [SyncVar] private int _modeIndex = 0;
        /// <summary>봇 캐릭터 랜덤 배정용 시드 — 호스트가 정하고 클라이언트와 공유 (모두 같은 봇 캐릭터를 보도록).</summary>
        [SyncVar] private int _botSeed = 1;

        private CardGameManager _gm;
        private CardGameNetworkManager _netMgr;

        // 서버
        private readonly HashSet<int> _readyConnIds = new HashSet<int>();
        private bool _serverHooked;
        private bool _startIssued;
        private int _hostPeekSeat = -1;
        private int _hostSwapSeat = -1;

        // 클라이언트
        private bool _clientHooked;
        private bool _clientPeekPanelHooked;
        private bool _clientSwapPanelHooked;
        private bool _clientConfigured;

        /// <summary>클라이언트 복제본이 구성을 마쳐 RPC를 반영할 수 있는 상태 (호스트 자신은 항상 false).</summary>
        private bool ClientLive => !isServer && _gm != null && _clientConfigured;

        // =====================================================================
        //  Lifecycle
        // =====================================================================

        private void Awake()
        {
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            UnhookServer();
            UnhookClient();
        }

        public override void OnStartServer()
        {
            Instance = this;
            _netMgr = CardGameNetworkManager.singleton;

            _seatCount = _netMgr != null ? _netMgr.MaxSeats : 4;
            _humanCount = CountServerHumans();
            _modeIndex = SceneContext.Get<int>(SceneContextKeys.SelectedModeIndex, 0);
            _botSeed = Random.Range(1, int.MaxValue);

            StartCoroutine(ServerBootstrap());
        }

        public override void OnStartClient()
        {
            Instance = this;
            if (isServer) return;   // 호스트는 서버 경로만
            StartCoroutine(ClientBootstrap());
        }

        public override void OnStopServer()
        {
            UnhookServer();
        }

        public override void OnStopClient()
        {
            if (!isServer) UnhookClient();
        }

        // =====================================================================
        //  SERVER
        // =====================================================================

        private int CountServerHumans()
        {
            int n = 0;
            if (_netMgr == null) return 1;
            foreach (var kv in _netMgr.ServerPlayers) if (kv.Value != null) n++;
            return Mathf.Max(1, n);
        }

        private IEnumerator ServerBootstrap()
        {
            // 매니저 대기 (같은 씬, 보통 즉시)
            while (_gm == null)
            {
                _gm = FindObjectOfType<CardGameManager>();
                if (_gm == null) yield return null;
            }

            int hostSeat = _netMgr != null ? _netMgr.GetSeatForConnection(NetworkServer.localConnection) : 0;
            if (hostSeat < 0) hostSeat = 0;

            var cfg = BuildServerConfig(hostSeat);
            _gm.ConfigureNetwork(GameRunMode.NetworkHost, cfg);

            // 호스트 이전으로 열린 세션이면 점수·라운드를 이어받음
            var resume = _netMgr != null ? _netMgr.ConsumeResumeData() : null;
            if (resume != null)
            {
                _gm.SetResumeData(resume);
                Debug.Log($"[Session/Server] 이어하기 데이터 적용 — 라운드 {resume.Round}");
            }

            HookServer();

            // 호스트 자신은 즉시 준비
            _readyConnIds.Add(NetworkServer.localConnection.connectionId);
            Debug.Log($"[Session/Server] 구성 완료 — 좌석 {cfg.SeatCount}, 사람 {_humanCount}, 호스트 자리 {hostSeat}");

            float t = 0f;
            while (!AllClientsReady() && t < readyTimeout)
            {
                t += Time.unscaledDeltaTime;
                yield return null;
            }
            if (!AllClientsReady())
                Debug.LogWarning($"[Session/Server] 준비 타임아웃 ({_readyConnIds.Count}/{NetworkServer.connections.Count}) — 그냥 시작");

            IssueStart();
        }

        private GameSessionConfig BuildServerConfig(int hostSeat)
        {
            var cfg = new GameSessionConfig
            {
                LocalSeat = hostSeat,
                Seed = Random.Range(1, int.MaxValue),
                ModeIndex = _modeIndex,
            };

            var bySeat = new Dictionary<int, NetworkGamePlayer>();
            if (_netMgr != null)
                foreach (var kv in _netMgr.ServerPlayers)
                    if (kv.Value != null) bySeat[kv.Value.seatIndex] = kv.Value;

            for (int s = 0; s < _seatCount; s++)
            {
                var sc = new SeatConfig { Seat = s };
                if (bySeat.TryGetValue(s, out var np))
                {
                    sc.Type = (s == hostSeat) ? PlayerType.Human : PlayerType.RemoteHuman;
                    sc.DisplayName = np.displayName;
                    sc.SteamId = np.steamId;
                    sc.CharacterIndex = np.characterIndex;
                }
                else sc.Type = PlayerType.Bot;
                cfg.Seats.Add(sc);
            }
            SeatIdentity.AssignBots(cfg, _botSeed);
            return cfg;
        }

        private bool AllClientsReady()
        {
            int total = NetworkServer.connections.Count;
            return _readyConnIds.Count >= total;
        }

        private void IssueStart()
        {
            if (_startIssued || _gm == null) return;
            _startIssued = true;
            Debug.Log("[Session/Server] 게임 시작 요청");
            _gm.RequestStartGame();
        }

        private void HookServer()
        {
            if (_serverHooked || _gm == null) return;
            _serverHooked = true;

            _gm.OnRoundStarted += ServerOnRoundStarted;
            _gm.OnPeekChoiceRequested += ServerOnPeekChoiceRequested;
            _gm.OnWinAvailableChanged += ServerOnWinAvailableChanged;
            _gm.OnTurnTimerChanged += ServerOnTurnTimerChanged;
            _gm.OnSwapChoiceRequested += ServerOnSwapChoiceRequested;

            CardGameEvents.OnDealCompleted += ServerOnDealCompleted;
            CardGameEvents.OnPhaseChanged += ServerOnPhaseChanged;
            CardGameEvents.OnTurnStarted += ServerOnTurnStarted;
            CardGameEvents.OnCardDrawn += ServerOnCardDrawn;
            CardGameEvents.OnCardDiscarded += ServerOnCardDiscarded;
            CardGameEvents.OnCompletion += ServerOnCompletion;
            CardGameEvents.OnGameFinished += ServerOnGameFinished;
            CardGameEvents.OnSpecialCardTriggered += ServerOnSpecialTriggered;
            CardGameEvents.OnSpecialCardCompleted += ServerOnSpecialCompleted;

            if (_gm.PeekPanel != null) _gm.PeekPanel.OnCardSelected += HostPeekPanelSelected;
            if (_gm.SwapPanel != null) _gm.SwapPanel.OnTargetSelected += HostSwapPanelSelected;
            if (_gm.SwapPanel != null) _gm.SwapPanel.OnSkipped += HostSwapPanelSkipped;
        }

        private void UnhookServer()
        {
            if (!_serverHooked) return;
            _serverHooked = false;

            if (_gm != null)
            {
                _gm.OnRoundStarted -= ServerOnRoundStarted;
                _gm.OnPeekChoiceRequested -= ServerOnPeekChoiceRequested;
                _gm.OnWinAvailableChanged -= ServerOnWinAvailableChanged;
                _gm.OnTurnTimerChanged -= ServerOnTurnTimerChanged;
                _gm.OnSwapChoiceRequested -= ServerOnSwapChoiceRequested;
                if (_gm.PeekPanel != null) _gm.PeekPanel.OnCardSelected -= HostPeekPanelSelected;
                if (_gm.SwapPanel != null) _gm.SwapPanel.OnTargetSelected -= HostSwapPanelSelected;
                if (_gm.SwapPanel != null) _gm.SwapPanel.OnSkipped -= HostSwapPanelSkipped;
            }

            CardGameEvents.OnDealCompleted -= ServerOnDealCompleted;
            CardGameEvents.OnPhaseChanged -= ServerOnPhaseChanged;
            CardGameEvents.OnTurnStarted -= ServerOnTurnStarted;
            CardGameEvents.OnCardDrawn -= ServerOnCardDrawn;
            CardGameEvents.OnCardDiscarded -= ServerOnCardDiscarded;
            CardGameEvents.OnCompletion -= ServerOnCompletion;
            CardGameEvents.OnGameFinished -= ServerOnGameFinished;
            CardGameEvents.OnSpecialCardTriggered -= ServerOnSpecialTriggered;
            CardGameEvents.OnSpecialCardCompleted -= ServerOnSpecialCompleted;
        }

        // ---- 서버 유틸 ----

        private delegate void RemoteAction(NetworkConnectionToClient conn, int seat);

        /// <summary>호스트 자신을 제외한 모든 원격 클라이언트에 대해 실행.</summary>
        private void ForEachRemote(RemoteAction action)
        {
            if (_netMgr == null) return;
            foreach (var kv in _netMgr.ServerPlayers)
            {
                var conn = kv.Key;
                var p = kv.Value;
                if (conn == null || p == null) continue;
                if (conn == NetworkServer.localConnection) continue;
                if (!conn.isReady) continue;
                action(conn, p.seatIndex);
            }
        }

        private NetCard[] ToNetCards(IReadOnlyList<CardData> cards)
        {
            var arr = new NetCard[cards.Count];
            for (int i = 0; i < cards.Count; i++) arr[i] = NetCard.From(_gm, cards[i]);
            return arr;
        }

        private int[] HandCounts()
        {
            var players = _gm.Players;
            var arr = new int[players.Count];
            for (int i = 0; i < players.Count; i++) arr[i] = players[i].Hand.Count;
            return arr;
        }

        private int[] Scores()
        {
            var players = _gm.Players;
            var arr = new int[players.Count];
            for (int i = 0; i < players.Count; i++) arr[i] = players[i].TotalScore;
            return arr;
        }

        private void BroadcastHandsAndCounts()
        {
            var counts = HandCounts();
            ForEachRemote((conn, seat) =>
            {
                if (seat >= 0 && seat < _gm.Players.Count)
                    TargetHandSnapshot(conn, seat, ToNetCards(_gm.Players[seat].Hand));
            });
            RpcHandCounts(counts);
        }

        // ---- 서버 이벤트 → RPC ----

        private void ServerOnRoundStarted(int round, int startingSeat)
        {
            RpcRoundStart(round, startingSeat);
            RpcScores(Scores());   // 호스트 이전 직후에도 점수판이 맞도록 매 라운드 동기화
        }

        private void ServerOnTurnTimerChanged(int seat, float duration)
        {
            RpcTurnTimer(seat, duration);
        }

        private void ServerOnSwapChoiceRequested(int seat)
        {
            if (seat == _gm.UserPlayerIndex)
            {
                // 호스트 본인 → 로컬 패널
                _hostSwapSeat = seat;
                if (_gm.SwapPanel != null) _gm.SwapPanel.Show(_gm.Players[seat], _gm.Players);
                else _gm.SubmitSwapTarget(seat, -1);
                return;
            }

            var conn = _netMgr != null ? _netMgr.GetConnectionForSeat(seat) : null;
            if (conn == null) return;   // 연결 없음 → 매니저 타임아웃이 자동 처리
            TargetSwapChoice(conn);
        }

        private void HostSwapPanelSelected(Player target, CardData sourceCard, CardData targetCard)
        {
            if (!isServer || _hostSwapSeat < 0) return;
            int seat = _hostSwapSeat;
            _hostSwapSeat = -1;
            if (!_gm.SubmitSwapTarget(seat, target != null ? target.Index : -1))
                _gm.SubmitSwapTarget(seat, -1);
        }

        private void HostSwapPanelSkipped()
        {
            if (!isServer || _hostSwapSeat < 0) return;
            int seat = _hostSwapSeat;
            _hostSwapSeat = -1;
            _gm.SubmitSwapTarget(seat, CardGameManager.SwapTargetSkip);
        }

        private void ServerOnDealCompleted()
        {
            BroadcastHandsAndCounts();
            RpcDeckCount(_gm.Deck.DrawPileCount);
            RpcDealCompleted();
        }

        private void ServerOnPhaseChanged(GamePhase prev, GamePhase next)
        {
            RpcPhase(next);
        }

        private void ServerOnTurnStarted(int seat)
        {
            RpcTurnStarted(seat);
        }

        private void ServerOnCardDrawn(int seat, CardData card)
        {
            var nc = NetCard.From(_gm, card);
            ForEachRemote((conn, cseat) =>
            {
                TargetCardDrawn(conn, seat, cseat == seat ? nc : NetCard.None);
            });
            RpcDeckCount(_gm.Deck.DrawPileCount);
        }

        private void ServerOnCardDiscarded(int seat, CardData card)
        {
            RpcCardDiscarded(seat, NetCard.From(_gm, card));
            RpcDeckCount(_gm.Deck.DrawPileCount);
        }

        private void ServerOnCompletion(int seat, CompletionResult result)
        {
            // 승자 손패 공개 (결과창 표시용) 후 완성 통지
            if (seat >= 0 && seat < _gm.Players.Count)
                RpcRevealHand(seat, ToNetCards(_gm.Players[seat].Hand));
            RpcCompletion(seat, (int)result.Pattern, result.Points, Scores());
        }

        private void ServerOnGameFinished(int winner)
        {
            RpcGameFinished(winner, Scores());
        }

        private void ServerOnSpecialTriggered(SpecialCardEvent ev)
        {
            RpcSpecialTriggered(ev.SourcePlayerIndex, (int)ev.Type);
        }

        private void ServerOnSpecialCompleted(SpecialCardEvent ev)
        {
            // Swap/Chaos/Peek 으로 손패가 바뀌었을 수 있음 → 스냅샷 재전송
            BroadcastHandsAndCounts();
            RpcDeckCount(_gm.Deck.DrawPileCount);
            RpcSpecialCompleted(ev.SourcePlayerIndex, (int)ev.Type);
            _hostPeekSeat = -1;
            _hostSwapSeat = -1;
        }

        private void ServerOnWinAvailableChanged(int seat, bool available)
        {
            // 호스트 본인은 로컬 이벤트로 이미 UI가 반응함. 원격 좌석에만 전송.
            if (seat == _gm.UserPlayerIndex) return;
            var conn = _netMgr != null ? _netMgr.GetConnectionForSeat(seat) : null;
            if (conn == null) return;
            TargetWinAvailable(conn, seat, available);
        }

        private void ServerOnPeekChoiceRequested(int seat, List<CardData> topCards)
        {
            int hostSeat = _gm.UserPlayerIndex;
            if (seat == hostSeat)
            {
                // 호스트 본인 → 로컬 패널
                _hostPeekSeat = seat;
                if (_gm.PeekPanel != null) _gm.PeekPanel.Show(topCards);
                else _gm.SubmitPeekChoice(seat, topCards[0]);
                return;
            }

            var conn = _netMgr != null ? _netMgr.GetConnectionForSeat(seat) : null;
            if (conn == null)
            {
                // 연결 없음 → 매니저 타임아웃이 자동 처리
                return;
            }
            TargetPeekChoice(conn, ToNetCards(topCards));
        }

        private void HostPeekPanelSelected(CardData chosen)
        {
            if (!isServer || _hostPeekSeat < 0) return;
            int seat = _hostPeekSeat;
            _hostPeekSeat = -1;
            _gm.PeekPanel?.Hide();
            _gm.SubmitPeekChoice(seat, chosen);
        }

        /// <summary>서버: 플레이어 이탈 → 그 자리 봇 전환.</summary>
        [Server]
        public void ServerHandleDisconnect(int seat)
        {
            if (_gm != null) _gm.ConvertSeatToBot(seat);
            RpcSeatBecameBot(seat);
            ServerRefreshHumanCount();
            // 준비 대기 중이었다면 재평가는 ServerBootstrap 루프가 처리
        }

        /// <summary>서버: 접속 중인 사람 수 갱신 (클라이언트 부트스트랩이 이 수만큼 플레이어 객체를 기다림).</summary>
        [Server]
        public void ServerRefreshHumanCount()
        {
            _humanCount = CountServerHumans();
        }

        /// <summary>
        /// 서버: 게임이 이미 진행 중일 때 준비 신호를 보낸 클라이언트(재접속·늦은 로딩)를 현재 판에 합류시킴.
        /// 전체 상태를 먼저 보내고, 그 다음 자리를 사람으로 되돌린다 (순서 중요: 타이머·승리 신호가 상태 뒤에 도착).
        /// </summary>
        [Server]
        private void ServerSyncLateJoiner(NetworkConnectionToClient conn)
        {
            if (_gm == null || _netMgr == null) return;
            int seat = _netMgr.GetSeatForConnection(conn);
            if (seat < 0 || seat >= _gm.Players.Count) return;

            var np = _netMgr.GetPlayerForConnection(conn);
            string name = np != null ? np.displayName : null;

            var log = _gm.DiscardLog;
            var discardSeats = new int[log.Count];
            var discardCards = new NetCard[log.Count];
            for (int i = 0; i < log.Count; i++)
            {
                discardSeats[i] = log[i].Seat;
                discardCards[i] = NetCard.From(_gm, log[i].Card);
            }

            TargetFullState(conn,
                _gm.RoundNumber, _gm.StartingPlayerIndex, _gm.CurrentPlayerIndex, (int)_gm.CurrentPhase,
                Scores(), HandCounts(), ToNetCards(_gm.Players[seat].Hand),
                discardSeats, discardCards, _gm.Deck.DrawPileCount);

            if (_gm.TurnTimerSeat >= 0)
                TargetTurnTimer(conn, _gm.TurnTimerSeat, _gm.TurnTimerRemaining);

            _gm.RestoreSeatToHuman(seat, name);
            RpcSeatBecameHuman(seat, name ?? "");
            ServerRefreshHumanCount();
            Debug.Log($"[Session/Server] 좌석 {seat} 합류 동기화 완료 ({name})");
        }

        // =====================================================================
        //  COMMANDS (client → server)
        // =====================================================================

        [Command(requiresAuthority = false)]
        private void CmdClientReady(NetworkConnectionToClient sender = null)
        {
            if (sender == null) return;
            _readyConnIds.Add(sender.connectionId);
            Debug.Log($"[Session/Server] 클라이언트 준비: conn {sender.connectionId} ({_readyConnIds.Count}/{NetworkServer.connections.Count})");

            // 이미 게임이 돌고 있으면 (재접속 / 준비 타임아웃 뒤에 도착) 현재 상태를 통째로 보냄
            if (_startIssued && _gm != null && _gm.Players.Count > 0)
                ServerSyncLateJoiner(sender);
        }

        [Command(requiresAuthority = false)]
        private void CmdSwapTarget(int targetSeat, NetworkConnectionToClient sender = null)
        {
            int seat = _netMgr != null ? _netMgr.GetSeatForConnection(sender) : -1;
            if (seat < 0 || _gm == null) return;
            bool ok = _gm.SubmitSwapTarget(seat, targetSeat);
            if (!ok) Debug.LogWarning($"[Session/Server] Swap 대상 거부: seat {seat} → {targetSeat}");
        }

        /// <summary>클라이언트: Swap 카드를 버리면서 미리 고른 교환 상대를 함께 보냄.</summary>
        [Command(requiresAuthority = false)]
        private void CmdDiscardSwap(int instanceId, int targetSeat, NetworkConnectionToClient sender = null)
        {
            int seat = _netMgr != null ? _netMgr.GetSeatForConnection(sender) : -1;
            if (seat < 0 || _gm == null) return;
            bool ok = _gm.RequestDiscardForSeat(seat, instanceId, targetSeat);
            if (!ok) Debug.LogWarning($"[Session/Server] Swap 버리기 거부: seat {seat}, id {instanceId} → {targetSeat}");
        }

        [Command(requiresAuthority = false)]
        private void CmdDiscard(int instanceId, NetworkConnectionToClient sender = null)
        {
            int seat = _netMgr != null ? _netMgr.GetSeatForConnection(sender) : -1;
            if (seat < 0 || _gm == null) return;
            bool ok = _gm.RequestDiscardForSeat(seat, instanceId);
            if (!ok) Debug.LogWarning($"[Session/Server] 버리기 거부: seat {seat}, id {instanceId}");
        }

        [Command(requiresAuthority = false)]
        private void CmdDeclareWin(NetworkConnectionToClient sender = null)
        {
            int seat = _netMgr != null ? _netMgr.GetSeatForConnection(sender) : -1;
            if (seat < 0 || _gm == null) return;
            bool ok = _gm.DeclareWinForSeat(seat);
            if (!ok) Debug.LogWarning($"[Session/Server] 승리 선언 거부: seat {seat}");
        }

        [Command(requiresAuthority = false)]
        private void CmdCompletionCheck(NetworkConnectionToClient sender = null)
        {
            int seat = _netMgr != null ? _netMgr.GetSeatForConnection(sender) : -1;
            if (seat < 0 || _gm == null) return;
            _gm.TryCompletionCheckForSeat(seat);
        }

        [Command(requiresAuthority = false)]
        private void CmdPeekChoice(int instanceId, NetworkConnectionToClient sender = null)
        {
            int seat = _netMgr != null ? _netMgr.GetSeatForConnection(sender) : -1;
            if (seat < 0 || _gm == null) return;
            bool ok = _gm.SubmitPeekChoiceForSeat(seat, instanceId);
            if (!ok) Debug.LogWarning($"[Session/Server] Peek 선택 거부: seat {seat}, id {instanceId}");
        }

        // =====================================================================
        //  CLIENT
        // =====================================================================

        private IEnumerator ClientBootstrap()
        {
            _netMgr = CardGameNetworkManager.singleton;

            // 1) 로컬 플레이어 + 좌석 + 모든 플레이어 객체 도착 대기
            float t = 0f;
            while (t < 10f)
            {
                var local = NetworkGamePlayer.Local;
                bool ok = local != null && local.seatIndex >= 0 && NetworkGamePlayer.All.Count >= _humanCount;
                if (ok) break;
                t += Time.unscaledDeltaTime;
                yield return null;
            }

            // 2) 매니저 + DB 준비
            while (_gm == null)
            {
                _gm = FindObjectOfType<CardGameManager>();
                if (_gm == null) yield return null;
            }
            while (!_gm.IsDatabaseReady) yield return null;

            // 3) 구성
            int mySeat = NetworkGamePlayer.Local != null ? NetworkGamePlayer.Local.seatIndex : 0;
            var cfg = BuildClientConfig(mySeat);
            _gm.ConfigureNetwork(GameRunMode.NetworkClient, cfg);
            _clientConfigured = true;
            HookClient();

            Debug.Log($"[Session/Client] 구성 완료 — 내 자리 {mySeat}, 좌석 {cfg.SeatCount}");
            CmdClientReady();
        }

        private GameSessionConfig BuildClientConfig(int mySeat)
        {
            var cfg = new GameSessionConfig { LocalSeat = mySeat, Seed = 0, ModeIndex = _modeIndex };
            for (int s = 0; s < _seatCount; s++)
            {
                var sc = new SeatConfig { Seat = s };
                var np = NetworkGamePlayer.FindBySeat(s);
                if (np != null)
                {
                    sc.Type = np.isLocalPlayer ? PlayerType.Human : PlayerType.RemoteHuman;
                    sc.DisplayName = np.displayName;
                    sc.SteamId = np.steamId;
                    sc.CharacterIndex = np.characterIndex;
                }
                else sc.Type = PlayerType.Bot;
                cfg.Seats.Add(sc);
            }
            SeatIdentity.AssignBots(cfg, _botSeed);
            return cfg;
        }

        private void HookClient()
        {
            if (_clientHooked || _gm == null) return;
            _clientHooked = true;
            _gm.OnClientDiscardRequested += ClientOnDiscardRequested;
            _gm.OnClientSwapDiscardRequested += ClientOnSwapDiscardRequested;
            _gm.OnClientCompletionCheckRequested += ClientOnCompletionCheckRequested;
            _gm.OnClientWinDeclareRequested += ClientOnWinDeclareRequested;
        }

        private void UnhookClient()
        {
            if (!_clientHooked) return;
            _clientHooked = false;
            if (_gm != null)
            {
                _gm.OnClientDiscardRequested -= ClientOnDiscardRequested;
                _gm.OnClientSwapDiscardRequested -= ClientOnSwapDiscardRequested;
                _gm.OnClientCompletionCheckRequested -= ClientOnCompletionCheckRequested;
                _gm.OnClientWinDeclareRequested -= ClientOnWinDeclareRequested;
                if (_clientPeekPanelHooked && _gm.PeekPanel != null)
                    _gm.PeekPanel.OnCardSelected -= ClientPeekPanelSelected;
                if (_clientSwapPanelHooked && _gm.SwapPanel != null)
                {
                    _gm.SwapPanel.OnTargetSelected -= ClientSwapPanelSelected;
                    _gm.SwapPanel.OnSkipped -= ClientSwapPanelSkipped;
                }
            }
        }

        private void ClientOnDiscardRequested(CardData card)
        {
            if (card == null || card.InstanceId < 0) return;
            CmdDiscard(card.InstanceId);
        }

        private void ClientOnSwapDiscardRequested(CardData card, int targetSeat)
        {
            if (card == null || card.InstanceId < 0) return;
            CmdDiscardSwap(card.InstanceId, targetSeat);
        }

        private void ClientOnCompletionCheckRequested()
        {
            CmdCompletionCheck();
        }

        private void ClientOnWinDeclareRequested()
        {
            CmdDeclareWin();
        }

        private void ClientPeekPanelSelected(CardData chosen)
        {
            if (isServer || chosen == null) return;
            _gm.PeekPanel?.Hide();
            CmdPeekChoice(chosen.InstanceId);
        }

        private void ClientSwapPanelSelected(Player target, CardData sourceCard, CardData targetCard)
        {
            if (isServer) return;
            if (_gm.CurrentPhase != GamePhase.WaitingSpecialAction) return;   // 버리기 전 선택은 매니저가 처리
            CmdSwapTarget(target != null ? target.Index : -1);
        }

        private void ClientSwapPanelSkipped()
        {
            if (isServer) return;
            if (_gm.CurrentPhase != GamePhase.WaitingSpecialAction) return;
            CmdSwapTarget(CardGameManager.SwapTargetSkip);
        }

        private List<CardData> ResolveAll(NetCard[] cards)
        {
            var list = new List<CardData>(cards != null ? cards.Length : 0);
            if (cards == null) return list;
            foreach (var nc in cards)
            {
                var c = nc.Resolve(_gm);
                if (c != null) list.Add(c);
            }
            return list;
        }

        // =====================================================================
        //  RPCs (server → clients) — 호스트 자신은 isServer 가드로 무시
        // =====================================================================

        [ClientRpc]
        private void RpcRoundStart(int round, int startingSeat)
        {
            if (!ClientLive) return;
            _gm.ClientApplyRoundStart(round, startingSeat);
        }

        [TargetRpc]
        private void TargetHandSnapshot(NetworkConnectionToClient target, int seat, NetCard[] cards)
        {
            if (!ClientLive) return;
            _gm.ClientApplyHandSnapshot(seat, ResolveAll(cards));
        }

        [ClientRpc]
        private void RpcHandCounts(int[] counts)
        {
            if (!ClientLive || counts == null) return;
            for (int s = 0; s < counts.Length; s++)
                if (s != _gm.UserPlayerIndex) _gm.ClientApplyHiddenHandCount(s, counts[s]);
        }

        [ClientRpc]
        private void RpcDealCompleted()
        {
            if (!ClientLive) return;
            _gm.ClientApplyDealCompleted();
        }

        [ClientRpc]
        private void RpcPhase(GamePhase phase)
        {
            if (!ClientLive) return;
            _gm.ClientApplyPhase(phase);
        }

        [ClientRpc]
        private void RpcTurnStarted(int seat)
        {
            if (!ClientLive) return;
            _gm.ClientApplyTurnStarted(seat);
        }

        [ClientRpc]
        private void RpcDeckCount(int drawCount)
        {
            if (!ClientLive) return;
            _gm.ClientApplyDeckCount(drawCount);
        }

        [TargetRpc]
        private void TargetCardDrawn(NetworkConnectionToClient target, int seat, NetCard card)
        {
            if (!ClientLive) return;
            _gm.ClientApplyCardDrawn(seat, card.IsValid ? card.Resolve(_gm) : null);
        }

        [ClientRpc]
        private void RpcCardDiscarded(int seat, NetCard card)
        {
            if (!ClientLive) return;
            var c = card.Resolve(_gm);
            if (c != null) _gm.ClientApplyCardDiscarded(seat, c);
        }

        [ClientRpc]
        private void RpcRevealHand(int seat, NetCard[] cards)
        {
            if (!ClientLive) return;
            _gm.ClientApplyHandSnapshot(seat, ResolveAll(cards));
        }

        [TargetRpc]
        private void TargetWinAvailable(NetworkConnectionToClient target, int seat, bool available)
        {
            if (!ClientLive) return;
            _gm.ClientApplyWinAvailable(seat, available);
        }

        [ClientRpc]
        private void RpcCompletion(int seat, int pattern, int points, int[] scores)
        {
            if (!ClientLive) return;
            _gm.ClientApplyCompletion(seat, (CompletionPattern)pattern, points, scores);
        }

        [ClientRpc]
        private void RpcGameFinished(int winner, int[] scores)
        {
            if (!ClientLive) return;
            _gm.ClientApplyGameFinished(winner, scores);
        }

        [ClientRpc]
        private void RpcSpecialTriggered(int seat, int type)
        {
            if (!ClientLive) return;
            _gm.ClientApplySpecialTriggered(seat, (SpecialCardType)type);
        }

        [ClientRpc]
        private void RpcSpecialCompleted(int seat, int type)
        {
            if (!ClientLive) return;
            _gm.ClientApplySpecialCompleted(seat, (SpecialCardType)type);
        }

        [ClientRpc]
        private void RpcSeatBecameBot(int seat)
        {
            if (!ClientLive) return;
            _gm.ClientApplySeatBecameBot(seat);
        }

        [ClientRpc]
        private void RpcSeatBecameHuman(int seat, string displayName)
        {
            if (!ClientLive) return;
            _gm.ClientApplySeatBecameHuman(seat, displayName);
        }

        [ClientRpc]
        private void RpcScores(int[] scores)
        {
            if (!ClientLive) return;
            _gm.ClientApplyScores(scores);
        }

        [ClientRpc]
        private void RpcTurnTimer(int seat, float duration)
        {
            if (!ClientLive) return;
            _gm.ClientApplyTurnTimer(seat, duration);
        }

        [TargetRpc]
        private void TargetTurnTimer(NetworkConnectionToClient target, int seat, float remaining)
        {
            if (!ClientLive) return;
            _gm.ClientApplyTurnTimer(seat, remaining);
        }

        [TargetRpc]
        private void TargetFullState(NetworkConnectionToClient target,
            int round, int startingSeat, int currentSeat, int phase,
            int[] scores, int[] handCounts, NetCard[] myHand,
            int[] discardSeats, NetCard[] discardCards, int deckCount)
        {
            if (!ClientLive) return;

            // 버림 기록은 순서·개수가 좌석 배열과 1:1이어야 하므로 실패한 카드도 자리를 유지
            var discards = new List<CardData>(discardCards != null ? discardCards.Length : 0);
            if (discardCards != null)
                foreach (var nc in discardCards) discards.Add(nc.Resolve(_gm));

            _gm.ClientApplyFullState(round, startingSeat, currentSeat, (GamePhase)phase,
                scores, handCounts, ResolveAll(myHand), discardSeats, discards, deckCount);
        }

        [TargetRpc]
        private void TargetSwapChoice(NetworkConnectionToClient target)
        {
            if (!ClientLive) return;

            var panel = _gm.SwapPanel;
            int mySeat = _gm.UserPlayerIndex;
            if (panel == null || mySeat < 0 || mySeat >= _gm.Players.Count)
            {
                CmdSwapTarget(-1);
                return;
            }

            if (!_clientSwapPanelHooked)
            {
                panel.OnTargetSelected += ClientSwapPanelSelected;
                panel.OnSkipped += ClientSwapPanelSkipped;
                _clientSwapPanelHooked = true;
            }
            panel.Show(_gm.Players[mySeat], _gm.Players);
        }

        [TargetRpc]
        private void TargetPeekChoice(NetworkConnectionToClient target, NetCard[] topCards)
        {
            if (!ClientLive) return;
            var list = ResolveAll(topCards);
            if (list.Count == 0) return;

            var panel = _gm.PeekPanel;
            if (panel == null)
            {
                CmdPeekChoice(list[0].InstanceId);
                return;
            }

            if (!_clientPeekPanelHooked)
            {
                panel.OnCardSelected += ClientPeekPanelSelected;
                _clientPeekPanelHooked = true;
            }
            panel.Show(list);
        }
    }
}

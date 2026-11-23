using System;
using System.Collections;
using System.Collections.Generic;
using Mirror;
using UnityEngine;
using UnityEngine.SceneManagement;
using CardGame.Core;
using CardGame.UI;
using Steamworks;

namespace CardGame.Network
{
    /// <summary>호스트 → 전원: 로딩씬으로 이동해 게임 데이터를 불러오라.</summary>
    public struct PrepareGameMessage : NetworkMessage { }

    /// <summary>클라이언트 → 호스트: 로딩 끝 (DB 준비 완료).</summary>
    public struct GameAssetsLoadedMessage : NetworkMessage { }

    /// <summary>
    /// SEVEN MAID 전용 NetworkManager (Mirror) v2 — 재접속 + 호스트 이전.
    ///
    /// 배치: BootstrapScene. 같은 GameObject에 FizzySteamworks(Transport) 컴포넌트.
    /// Inspector:
    ///   - Player Prefab   : NetworkGamePlayer 프리팹
    ///   - Offline Scene   : (비움)  ★ v2: 로비 복귀는 이 스크립트가 직접 처리 (Lobby Scene Name)
    ///   - Online Scene    : (비움)
    ///   - Dont Destroy On Load : ✓
    ///
    /// Steam 로비 이벤트 → Mirror 연결 글루:
    ///   SteamLobbyManager.OnLobbyEntered(owner=true)  → StartHost()
    ///   SteamLobbyManager.OnLobbyEntered(owner=false) → networkAddress = 호스트 SteamID → StartClient()
    ///
    /// ■ 재접속 (같은 호스트)
    ///   게임 중 플레이어가 끊기면 그 자리는 봇이 되고 SteamID → 좌석이 기록된다.
    ///   같은 SteamID가 다시 접속하면 원래 자리를 돌려받고, 세션이 전체 상태를 보내 진행 중인 판에 복귀한다.
    ///   끊긴 쪽은 자동으로 재시도하며(복구 루프), 게임을 껐다 켠 경우엔 온라인 패널의 [재접속] 버튼으로 돌아온다.
    ///
    /// ■ 호스트 이전
    ///   호스트가 사라지면 Steam이 로비 오너를 다른 멤버에게 넘긴다. 새 오너가 호스트를 열고
    ///   (점수·라운드 번호·좌석 유지) 현재 라운드를 처음부터 다시 시작한다. 나머지는 새 호스트에 자동 접속.
    ///   클라이언트는 남의 손패와 덱 순서를 모르기 때문에 "진행 중이던 판 그대로"는 이어갈 수 없다.
    ///
    /// ■ 게임 시작 로딩
    ///   [게임 시작] → 호스트가 PrepareGameMessage 전송 → 전원 LoadScene으로 이동해 CardDatabase 로드
    ///   → 각자 GameAssetsLoadedMessage 회신 → 전원 완료(또는 시간 초과) 시 호스트가 게임 씬으로 전환.
    ///   로비/방 화면에서는 DB를 불러오지 않는다.
    /// </summary>
    public class CardGameNetworkManager : NetworkManager
    {
        public static new CardGameNetworkManager singleton => (CardGameNetworkManager)NetworkManager.singleton;

        [Header("SEVEN MAID")]
        [SerializeField] private int maxSeats = 4;
        [SerializeField] private string gameSceneName = "CardGameScene";
        [Tooltip("세션이 끝났을 때 돌아갈 씬 (Mirror의 Offline Scene 대신 사용).")]
        [SerializeField] private string lobbySceneName = "LobbyScene";
        [SerializeField] private float fadeDuration = 0.3f;
        [Tooltip("CardGameScene 진입 시 호스트가 스폰하는 세션 프리팹 (NetworkIdentity + NetworkGameSession). Registered Spawnable Prefabs에도 등록.")]
        [SerializeField] private GameObject sessionPrefab;

        [Header("재접속 / 호스트 이전")]
        [Tooltip("게임 중 끊긴 플레이어가 같은 자리로 돌아올 수 있게 함.")]
        [SerializeField] private bool enableReconnect = true;
        [Tooltip("호스트가 나가면 다른 플레이어가 호스트를 이어받음 (Steam 로비 필요).")]
        [SerializeField] private bool enableHostMigration = true;
        [Tooltip("연결이 끊긴 뒤 복구를 시도하는 최대 시간(초). 넘으면 로비로 복귀.")]
        [SerializeField, Range(10f, 120f)] private float recoveryTimeout = 45f;
        [Tooltip("같은 호스트로 재접속을 다시 시도하는 간격(초).")]
        [SerializeField, Range(2f, 20f)] private float reconnectRetryInterval = 5f;
        [Tooltip("새 호스트가 다른 플레이어의 접속을 기다리는 시간(초). 늦게 온 사람은 재접속 경로로 합류.")]
        [SerializeField, Range(1f, 20f)] private float migrationGatherTime = 6f;

        [Header("게임 시작 로딩")]
        [Tooltip("게임 시작 시 거쳐 가는 로딩 씬 (LoadingSceneController가 DB를 불러옴).")]
        [SerializeField] private string loadingSceneName = "LoadScene";
        [Tooltip("로딩 화면 최소 표시 시간(초).")]
        [SerializeField, Range(0f, 10f)] private float minLoadingTime = 2f;
        [Tooltip("전원 로딩 완료를 기다리는 최대 시간(초). 넘으면 늦은 사람을 기다리지 않고 시작.")]
        [SerializeField, Range(10f, 120f)] private float loadingTimeout = 60f;

        /// <summary>세션 상태 변화 알림 (UI용).</summary>
        public event Action<string> OnStatus;
        public event Action<string> OnError;
        public event Action OnSessionEnded;
        /// <summary>연결 복구(재접속·호스트 이전) 안내 표시 (active, message). ConnectionRecoveryOverlay가 구독.</summary>
        public event Action<bool, string> OnRecoveryStateChanged;

        public bool IsHostMode => NetworkServer.active && NetworkClient.active;
        public bool IsClientOnly => !NetworkServer.active && NetworkClient.active;
        public bool InSession => NetworkServer.active || NetworkClient.active;
        public bool GameStarted { get; private set; }
        public int MaxSeats => maxSeats;

        /// <summary>복구 루프가 도는 중 (연결 끊김 → 재접속/이전 시도).</summary>
        public bool IsRecovering { get; private set; }
        /// <summary>복구 안내를 화면에 띄워야 하는지 (복구 루프 + 새 세션 씬 로드 대기 포함).</summary>
        public bool RecoveryOverlayActive { get; private set; }
        public string RecoveryMessage { get; private set; } = "";

        // 서버: 접속 → 플레이어 객체
        private readonly Dictionary<NetworkConnectionToClient, NetworkGamePlayer> _serverPlayers =
            new Dictionary<NetworkConnectionToClient, NetworkGamePlayer>();

        // 서버: 돌아올 수 있는 자리 (SteamID → 좌석). SteamID가 없는 전송(LAN 테스트)은 _reclaimAnySeats.
        private readonly Dictionary<ulong, int> _reclaimSeats = new Dictionary<ulong, int>();
        private readonly List<int> _reclaimAnySeats = new List<int>();

        private bool _steamHooked;
        private bool _leaving;
        private bool _clientInGame;
        private bool _playerRequested;

        // 복구
        private Coroutine _recoverRoutine;
        private Coroutine _overlayFailsafe;
        private ulong _oldHostId;
        private GameResumeData _pendingResume;   // 끊긴 순간의 진행 상황 (새 호스트가 될 때 사용)
        private int _pendingLocalSeat = -1;
        private GameResumeData _resumeData;      // 새 호스트: 다음 세션이 가져갈 데이터

        // 게임 시작 로딩
        private readonly HashSet<int> _loadedConnIds = new HashSet<int>();
        private Coroutine _serverPrepareRoutine;
        private Coroutine _clientLoadRoutine;
        private bool _clientPreparing;

        /// <summary>게임 시작 로딩 중 (로딩씬 표시 중).</summary>
        public bool IsPreparingGame => _clientPreparing;

        private static SteamLobbyManager Lobby => SteamLobbyManager.Instance;

        // =====================================================================
        //  Unity / Mirror lifecycle
        // =====================================================================

        public override void Awake()
        {
            // 로비 복귀는 직접 처리한다. Mirror의 Offline Scene이 설정돼 있으면 세션 종료 시 Mirror가
            // 이 매니저를 DontDestroyOnLoad에서 빼내 파괴하므로(다음 온라인 세션 불가) 강제로 비운다.
            if (!string.IsNullOrWhiteSpace(offlineScene))
            {
                Debug.LogWarning("[NetMgr] Offline Scene은 비워 두세요 — 로비 복귀는 Lobby Scene Name으로 처리합니다. (자동으로 비움)");
                offlineScene = "";
            }
            if (!string.IsNullOrWhiteSpace(onlineScene)) onlineScene = "";

            base.Awake();
            maxConnections = maxSeats;
            SceneManager.sceneLoaded += HandleSceneLoaded;
        }

        public override void Start()
        {
            base.Start();
            HookSteam();
        }

        private void HookSteam()
        {
            if (_steamHooked) return;
            var lobby = Lobby;
            if (lobby == null) return;
            lobby.OnLobbyEntered += HandleLobbyEntered;
            lobby.OnLobbyEnterFailed += HandleLobbyEnterFailed;
            _steamHooked = true;
        }

        public override void OnDestroy()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            var lobby = Lobby;
            if (lobby != null)
            {
                lobby.OnLobbyEntered -= HandleLobbyEntered;
                lobby.OnLobbyEnterFailed -= HandleLobbyEnterFailed;
            }
            base.OnDestroy();
        }

        private void HandleLobbyEnterFailed(string msg) => OnError?.Invoke(msg);

        // =====================================================================
        //  Steam → Mirror 글루
        // =====================================================================

        private void HandleLobbyEntered(CSteamID lobbyId, bool isOwner)
        {
            HookSteam();
            if (IsRecovering) return;   // 복구 루프가 연결을 관리

            if (isOwner)
            {
                // 비어 있던 예전 방에 다시 들어와 오너가 된 경우 → 새 방처럼 초기화
                var lobby = Lobby;
                if (lobby != null && lobby.GetHostSteamId() != lobby.LocalSteamId)
                {
                    lobby.SetLobbyData(SteamLobbyManager.KEY_HOST, lobby.LocalSteamId.ToString());
                    lobby.SetLobbyState("lobby");
                    lobby.ClearRejoinInfo();
                }

                if (!NetworkServer.active)
                {
                    OnStatus?.Invoke("호스트 시작...");
                    StartHost();
                }
                return;
            }

            ulong host = Lobby.GetHostSteamId();
            if (host == 0UL)
            {
                // 로비 데이터 아직 전파 안 됨 → 잠시 후 재시도
                StartCoroutine(RetryJoinWhenHostKnown());
                return;
            }

            ConnectToHost(host);
        }

        private IEnumerator RetryJoinWhenHostKnown()
        {
            float t = 0f;
            while (t < 5f)
            {
                yield return new WaitForSecondsRealtime(0.25f);
                t += 0.25f;
                ulong host = Lobby != null ? Lobby.GetHostSteamId() : 0UL;
                if (host != 0UL) { ConnectToHost(host); yield break; }
            }
            OnError?.Invoke("호스트 정보를 받지 못했습니다.");
            Lobby?.LeaveLobby();
        }

        private void ConnectToHost(ulong hostSteamId)
        {
            if (NetworkClient.active || NetworkServer.active) return;
            networkAddress = hostSteamId.ToString();
            _playerRequested = false;
            OnStatus?.Invoke("호스트에 연결 중...");
            StartClient();
        }

        // =====================================================================
        //  Public API (UI에서 호출)
        // =====================================================================

        /// <summary>호스트: 로비 → 게임 씬 전환. 모두 준비됐을 때만.</summary>
        public bool StartGameScene()
        {
            if (!NetworkServer.active) return false;
            if (GameStarted) return false;
            if (!NetworkGamePlayer.AllReady())
            {
                OnError?.Invoke("모든 플레이어가 준비되지 않았습니다.");
                return false;
            }

            GameStarted = true;
            _reclaimSeats.Clear();
            _reclaimAnySeats.Clear();
            _resumeData = null;

            // 재접속을 허용하면 로비는 참가 가능으로 두되 상태만 "ingame" (방 찾기 목록에는 안 나옴).
            // 게임 중 새로 들어오는 접속은 OnServerConnect에서 "돌아올 자리가 있는 SteamID"만 받는다.
            if (enableReconnect) Lobby?.SetLobbyState("ingame");
            else Lobby?.SetLobbyJoinable(false);

            SceneContext.Set(SceneContextKeys.OnlineSession, true);

            // 전원 로딩씬으로 → 로딩 완료 후 게임 씬
            _loadedConnIds.Clear();
            if (_serverPrepareRoutine != null) StopCoroutine(_serverPrepareRoutine);
            NetworkServer.SendToAll(new PrepareGameMessage());
            _serverPrepareRoutine = StartCoroutine(ServerWaitForLoading());
            return true;
        }

        // =====================================================================
        //  게임 시작 로딩 (로딩씬에서 DB 로드)
        // =====================================================================

        private IEnumerator ServerWaitForLoading()
        {
            float t = 0f;
            while (t < loadingTimeout)
            {
                if (!NetworkServer.active) { _serverPrepareRoutine = null; yield break; }

                bool all = true;
                foreach (var id in NetworkServer.connections.Keys)
                    if (!_loadedConnIds.Contains(id)) { all = false; break; }
                if (all) break;

                t += Time.unscaledDeltaTime;
                yield return null;
            }

            _serverPrepareRoutine = null;
            if (!NetworkServer.active) yield break;
            if (t >= loadingTimeout) Debug.LogWarning("[NetMgr] 로딩 대기 시간 초과 — 완료된 인원으로 게임 시작");
            Debug.Log($"[NetMgr] 전원 로딩 완료 ({_loadedConnIds.Count}명) — 게임 씬으로 전환");
            ServerChangeScene(gameSceneName);
        }

        private void OnServerAssetsLoaded(NetworkConnectionToClient conn, GameAssetsLoadedMessage msg)
        {
            if (conn == null) return;
            _loadedConnIds.Add(conn.connectionId);
            Debug.Log($"[NetMgr] 로딩 완료 수신: conn {conn.connectionId} ({_loadedConnIds.Count}/{NetworkServer.connections.Count})");
        }

        private void OnClientPrepareGame(PrepareGameMessage msg)
        {
            if (_clientPreparing) return;
            _clientPreparing = true;

            SceneContext.Set(SceneContextKeys.OnlineSession, true);
            // LoadingSceneController는 "로비에서 왔을 때만" DB를 불러온다
            SceneContext.PreviousScene = GameScene.LobbyScene;

            if (_clientLoadRoutine != null) StopCoroutine(_clientLoadRoutine);
            _clientLoadRoutine = StartCoroutine(ClientLoadingRoutine());
        }

        private IEnumerator ClientLoadingRoutine()
        {
            if (FadeController.Instance != null)
                yield return FadeController.Instance.FadeOutRoutine(fadeDuration);

            var op = SceneManager.LoadSceneAsync(loadingSceneName, LoadSceneMode.Single);
            if (op != null) while (!op.isDone) yield return null;
            yield return null;   // LoadingSceneController.Awake/Start

            if (FadeController.Instance != null)
                yield return FadeController.Instance.FadeInRoutine(fadeDuration);

            float t = 0f;
            while ((t < minLoadingTime || !LoadingSceneController.IsLoadComplete) && t < loadingTimeout)
            {
                t += Time.unscaledDeltaTime;
                yield return null;
            }

            _clientLoadRoutine = null;
            if (NetworkClient.isConnected)
            {
                Debug.Log("[NetMgr] 로딩 완료 — 호스트에 알림");
                NetworkClient.Send(new GameAssetsLoadedMessage());
            }
            // 이후 호스트의 씬 전환 메시지로 게임 씬 로드 (로딩 화면은 그때까지 유지)
        }

        private void StopClientPreparing()
        {
            _clientPreparing = false;
            if (_clientLoadRoutine != null) { StopCoroutine(_clientLoadRoutine); _clientLoadRoutine = null; }
        }

        /// <summary>호스트/클라이언트 공통: 스스로 세션을 나감 → 로비 씬으로 복귀.</summary>
        public void LeaveSession()
        {
            _leaving = true;
            bool wasInGame = IsInGameFlow();
            StopClientPreparing();

            StopRecovery();
            SetRecovery(false, "");
            GameStarted = false;
            _clientInGame = false;
            SceneContext.Set(SceneContextKeys.OnlineSession, false);

            if (NetworkServer.active && NetworkClient.isConnected) StopHost();
            else if (NetworkClient.isConnected || NetworkClient.active) StopClient();
            else if (NetworkServer.active) StopServer();

            Lobby?.ClearRejoinInfo();
            Lobby?.LeaveLobby();
            _leaving = false;

            if (wasInGame) ReturnToLobbyScene();
        }

        /// <summary>세션(NetworkGameSession)이 호스트 이전 후 이어받을 진행 상황을 가져감. 없으면 null.</summary>
        public GameResumeData ConsumeResumeData()
        {
            var d = _resumeData;
            _resumeData = null;
            return d;
        }

        private bool IsInGameScene() => SceneManager.GetActiveScene().name == gameSceneName;
        private bool IsInLoadingScene() => SceneManager.GetActiveScene().name == loadingSceneName;
        /// <summary>게임 씬이거나 게임 시작 로딩 중 — 세션이 끊기면 로비로 돌려보내야 하는 상태.</summary>
        private bool IsInGameFlow() => IsInGameScene() || IsInLoadingScene();

        private bool _returningToLobby;

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode) => _returningToLobby = false;

        private void ReturnToLobbyScene()
        {
            if (!IsInGameFlow() || _returningToLobby) return;
            _returningToLobby = true;
            StartCoroutine(ReturnToLobbyRoutine());
        }

        private IEnumerator ReturnToLobbyRoutine()
        {
            // Mirror 종료 처리가 끝난 다음 프레임에 전환
            yield return null;
            if (!IsInGameFlow() || InSession) { _returningToLobby = false; yield break; }

            LoadLobbySceneNow();
        }

        private void LoadLobbySceneNow()
        {
            GameScene scene;
            bool known = Enum.TryParse(lobbySceneName, out scene);
            if (known && SceneLoader.Instance != null && !SceneLoader.Instance.IsLoading)
                SceneLoader.Instance.Load(scene);   // 페이드 포함
            else
                SceneManager.LoadScene(lobbySceneName);
        }

        // =====================================================================
        //  Server callbacks
        // =====================================================================

        public override void OnServerConnect(NetworkConnectionToClient conn)
        {
            bool isLocal = conn == NetworkServer.localConnection;

            if (GameStarted && !isLocal)
            {
                // 게임 중: 돌아올 자리가 있는 플레이어만 받음
                ulong id = GetSteamIdForConnection(conn);
                KickStaleConnection(id, conn);

                if (!enableReconnect || !HasReclaimSeat(id))
                {
                    Debug.LogWarning($"[NetMgr] 게임 진행 중 접속 거부 (steam {id})");
                    conn.Disconnect();
                    return;
                }
            }
            else if (!GameStarted && numPlayers >= maxSeats)
            {
                Debug.LogWarning("[NetMgr] 좌석 꽉 참, 접속 거부");
                conn.Disconnect();
                return;
            }

            base.OnServerConnect(conn);
        }

        public override void OnServerAddPlayer(NetworkConnectionToClient conn)
        {
            int seat;
            bool rejoin = GameStarted;

            if (rejoin)
            {
                seat = TakeReclaimSeat(GetSteamIdForConnection(conn));
                if (seat < 0)
                {
                    Debug.LogWarning("[NetMgr] 돌아올 자리가 없는 접속 — 거부");
                    conn.Disconnect();
                    return;
                }
            }
            else seat = FindFreeSeat();

            var go = Instantiate(playerPrefab);
            go.name = $"NetworkGamePlayer [conn {conn.connectionId}]";

            var player = go.GetComponent<NetworkGamePlayer>();
            if (player == null)
            {
                Debug.LogError("[NetMgr] playerPrefab에 NetworkGamePlayer 없음");
                Destroy(go);
                return;
            }

            bool isHostConn = conn == NetworkServer.localConnection;
            player.ServerAssignSeat(seat, isHostConn);
            if (rejoin) player.isReady = true;
            _serverPlayers[conn] = player;

            NetworkServer.AddPlayerForConnection(conn, go);
            Debug.Log($"[NetMgr] 플레이어 추가: conn={conn.connectionId}, seat={seat}, host={isHostConn}, rejoin={rejoin}");

            if (rejoin) NetworkGameSession.Instance?.ServerRefreshHumanCount();
        }

        private int FindFreeSeat()
        {
            var used = new HashSet<int>();
            foreach (var p in _serverPlayers.Values) if (p != null) used.Add(p.seatIndex);
            for (int s = 0; s < maxSeats; s++) if (!used.Contains(s)) return s;
            return maxSeats - 1;
        }

        public override void OnServerDisconnect(NetworkConnectionToClient conn)
        {
            if (_serverPlayers.TryGetValue(conn, out var player))
                HandlePlayerLeft(conn, player);
            base.OnServerDisconnect(conn);
        }

        /// <summary>서버: 플레이어가 떠남 → 자리 기록(재접속용) + 봇 전환.</summary>
        private void HandlePlayerLeft(NetworkConnectionToClient conn, NetworkGamePlayer player)
        {
            _serverPlayers.Remove(conn);
            if (player == null) return;

            int seat = player.seatIndex;
            Debug.Log($"[NetMgr] 플레이어 이탈: seat={seat}, steam={player.steamId}");

            if (GameStarted && enableReconnect && seat >= 0)
            {
                if (player.steamId != 0UL) _reclaimSeats[player.steamId] = seat;
                else if (!_reclaimAnySeats.Contains(seat)) _reclaimAnySeats.Add(seat);
            }

            NetworkGameSession.Instance?.ServerHandleDisconnect(seat);
        }

        /// <summary>같은 SteamID의 예전 연결이 아직 살아 있으면(서버가 끊김을 늦게 감지) 먼저 정리.</summary>
        private void KickStaleConnection(ulong steamId, NetworkConnectionToClient newConn)
        {
            if (steamId == 0UL) return;

            NetworkConnectionToClient stale = null;
            NetworkGamePlayer stalePlayer = null;
            foreach (var kv in _serverPlayers)
            {
                if (kv.Key == newConn || kv.Value == null) continue;
                if (kv.Value.steamId == steamId) { stale = kv.Key; stalePlayer = kv.Value; break; }
            }
            if (stale == null) return;

            Debug.Log($"[NetMgr] 같은 계정의 이전 연결 정리: conn={stale.connectionId}");
            HandlePlayerLeft(stale, stalePlayer);
            stale.Disconnect();
        }

        private ulong GetSteamIdForConnection(NetworkConnectionToClient conn)
        {
            if (conn == null) return 0UL;
            if (conn == NetworkServer.localConnection) return Lobby != null ? Lobby.LocalSteamId : 0UL;
            // FizzySteamworks는 클라이언트 주소로 SteamID를 돌려준다. KCP 등은 IP라 파싱 실패 → 0.
            return ulong.TryParse(conn.address, out var id) ? id : 0UL;
        }

        private bool SeatOccupied(int seat)
        {
            foreach (var p in _serverPlayers.Values) if (p != null && p.seatIndex == seat) return true;
            return false;
        }

        private bool HasReclaimSeat(ulong steamId)
        {
            if (steamId != 0UL) return _reclaimSeats.ContainsKey(steamId);
            return _reclaimAnySeats.Count > 0;
        }

        private int TakeReclaimSeat(ulong steamId)
        {
            if (steamId != 0UL)
            {
                if (!_reclaimSeats.TryGetValue(steamId, out var seat)) return -1;
                _reclaimSeats.Remove(steamId);
                return SeatOccupied(seat) ? -1 : seat;
            }

            for (int i = 0; i < _reclaimAnySeats.Count; i++)
            {
                int seat = _reclaimAnySeats[i];
                _reclaimAnySeats.RemoveAt(i);
                if (!SeatOccupied(seat)) return seat;
                i--;
            }
            return -1;
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            NetworkServer.ReplaceHandler<GameAssetsLoadedMessage>(OnServerAssetsLoaded);
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            NetworkClient.ReplaceHandler<PrepareGameMessage>(OnClientPrepareGame);
        }

        public override void OnStopServer()
        {
            if (_serverPrepareRoutine != null) { StopCoroutine(_serverPrepareRoutine); _serverPrepareRoutine = null; }
            _loadedConnIds.Clear();
            _serverPlayers.Clear();
            _reclaimSeats.Clear();
            _reclaimAnySeats.Clear();
            GameStarted = false;
            base.OnStopServer();
        }

        public override void OnServerSceneChanged(string sceneName)
        {
            base.OnServerSceneChanged(sceneName);
            Debug.Log($"[NetMgr] 서버 씬 전환 완료: {sceneName}");

            if (sceneName == gameSceneName || IsInGameScene())
                SpawnSession();
        }

        private void SpawnSession()
        {
            if (NetworkGameSession.Instance != null) return;
            if (sessionPrefab == null)
            {
                Debug.LogError("[NetMgr] sessionPrefab 미설정 — NetworkGameSession 프리팹을 Inspector에 연결하고 Registered Spawnable Prefabs에도 등록하세요.");
                return;
            }
            var go = Instantiate(sessionPrefab);
            go.name = "NetworkGameSession";
            NetworkServer.Spawn(go);
            Debug.Log("[NetMgr] NetworkGameSession 스폰");
        }

        /// <summary>서버: 좌석 → 접속 매핑 (NetworkGameSession이 사용).</summary>
        public NetworkConnectionToClient GetConnectionForSeat(int seat)
        {
            foreach (var kv in _serverPlayers)
                if (kv.Value != null && kv.Value.seatIndex == seat) return kv.Key;
            return null;
        }

        public int GetSeatForConnection(NetworkConnectionToClient conn)
        {
            if (conn == null) return -1;
            return _serverPlayers.TryGetValue(conn, out var p) && p != null ? p.seatIndex : -1;
        }

        public NetworkGamePlayer GetPlayerForConnection(NetworkConnectionToClient conn)
        {
            if (conn == null) return null;
            return _serverPlayers.TryGetValue(conn, out var p) ? p : null;
        }

        public IEnumerable<KeyValuePair<NetworkConnectionToClient, NetworkGamePlayer>> ServerPlayers => _serverPlayers;

        // =====================================================================
        //  Client callbacks
        // =====================================================================

        public override void OnClientConnect()
        {
            // base와 같은 동작이지만 AddPlayer 요청 여부를 기억한다.
            // (게임 중인 호스트에 접속하면 곧바로 씬 로드가 이어지는데, 로드 후 AddPlayer가 한 번 더 나가는 것을 막음)
            if (!clientLoadedScene)
            {
                if (!NetworkClient.ready) NetworkClient.Ready();
                if (autoCreatePlayer && !_playerRequested)
                {
                    NetworkClient.AddPlayer();
                    _playerRequested = true;
                }
            }

            OnStatus?.Invoke("연결됨");

            if (IsRecovering && !NetworkServer.active)
            {
                // 복구 성공 — 곧 호스트가 게임 씬을 다시 불러오게 함
                IsRecovering = false;
                StopRecoverRoutine();
                SetRecovery(true, "연결되었습니다. 게임을 다시 불러오는 중...");
                StartOverlayFailsafe();
            }
        }

        public override void OnClientDisconnect()
        {
            base.OnClientDisconnect();
            _playerRequested = false;

            // 호스트 자신의 로컬 클라이언트가 멈춘 경우 → OnStopHost에서 처리
            if (NetworkServer.active)
            {
                NetworkGamePlayer.All.Clear();
                return;
            }

            // 복구 루프 중의 접속 실패 → 루프가 계속 시도
            if (IsRecovering)
            {
                NetworkGamePlayer.All.Clear();
                return;
            }

            if (ShouldRecover())
            {
                BeginRecovery();
                NetworkGamePlayer.All.Clear();
                return;
            }

            // 일반 종료
            bool wasInGame = _clientInGame || IsInGameFlow();
            bool gameFinished = IsGameFinished();
            StopClientPreparing();

            GameStarted = false;
            _clientInGame = false;
            SceneContext.Set(SceneContextKeys.OnlineSession, false);
            NetworkGamePlayer.All.Clear();
            SetRecovery(false, "");

            if (!_leaving)
            {
                if (gameFinished) Lobby?.ClearRejoinInfo();
                Lobby?.LeaveLobby();
            }

            OnStatus?.Invoke("연결이 종료되었습니다.");
            OnSessionEnded?.Invoke();

            // 게임이 끝나 최종 결과창을 보고 있는 중이면 그대로 둠 (확인 버튼 → LeaveSession → 로비)
            if (wasInGame && !gameFinished && !_leaving) ReturnToLobbyScene();
        }

        public override void OnClientChangeScene(string newSceneName, SceneOperation sceneOperation, bool customHandling)
        {
            if (newSceneName == gameSceneName || newSceneName.EndsWith("/" + gameSceneName + ".unity"))
                SceneContext.Set(SceneContextKeys.OnlineSession, true);

            if (FadeController.Instance != null)
                StartCoroutine(FadeController.Instance.FadeOutRoutine(fadeDuration));
            base.OnClientChangeScene(newSceneName, sceneOperation, customHandling);
        }

        public override void OnClientSceneChanged()
        {
            // base와 같은 동작 (Ready + 필요 시 AddPlayer). AddPlayer는 한 번만.
            if (NetworkClient.connection != null && NetworkClient.connection.isAuthenticated && !NetworkClient.ready)
                NetworkClient.Ready();

            if (autoCreatePlayer && NetworkClient.localPlayer == null && !_playerRequested)
            {
                NetworkClient.AddPlayer();
                _playerRequested = true;
            }

            if (FadeController.Instance != null)
                StartCoroutine(FadeController.Instance.FadeInRoutine(fadeDuration));

            if (IsInGameScene())
            {
                StopClientPreparing();
                _clientInGame = true;
                Lobby?.SaveRejoinInfo();

                // 결과창의 [확인/나가기] → 세션 종료. 세션 객체가 이미 사라진 뒤(호스트가 먼저 나감)에도 동작해야 하므로 여기서 연결.
                var gm = FindObjectOfType<CardGameManager>();
                if (gm != null)
                {
                    gm.OnLeaveOnlineRequested -= LeaveSession;
                    gm.OnLeaveOnlineRequested += LeaveSession;
                }
            }

            // 새 세션의 게임 씬이 올라왔으면 복구 안내 종료
            if (!IsRecovering) SetRecovery(false, "");
        }

        public override void OnStopHost()
        {
            base.OnStopHost();
            bool wasInGame = IsInGameFlow();
            StopClientPreparing();
            if (_serverPrepareRoutine != null) { StopCoroutine(_serverPrepareRoutine); _serverPrepareRoutine = null; }
            bool gameFinished = IsGameFinished();

            GameStarted = false;
            _clientInGame = false;
            SceneContext.Set(SceneContextKeys.OnlineSession, false);
            NetworkGamePlayer.All.Clear();
            OnSessionEnded?.Invoke();

            if (wasInGame && !_leaving && !gameFinished) ReturnToLobbyScene();
        }

        private static bool IsGameFinished()
        {
            var gm = FindObjectOfType<CardGameManager>();
            return gm != null && gm.CurrentPhase == GamePhase.GameFinished;
        }

        // =====================================================================
        //  연결 복구 (재접속 / 호스트 이전) — 클라이언트 측
        // =====================================================================

        private bool ShouldRecover()
        {
            if (_leaving || !_clientInGame) return false;
            if (!enableReconnect && !enableHostMigration) return false;
            var lobby = Lobby;
            if (lobby == null || !lobby.IsSteamReady || !lobby.InLobby) return false;

            var gm = FindObjectOfType<CardGameManager>();
            if (gm == null || !gm.IsNetworkClient) return false;
            if (gm.CurrentPhase == GamePhase.GameFinished) return false;
            return true;
        }

        private void BeginRecovery()
        {
            IsRecovering = true;
            _oldHostId = Lobby.GetHostSteamId();

            var gm = FindObjectOfType<CardGameManager>();
            _pendingResume = gm != null ? gm.BuildResumeData() : null;
            _pendingLocalSeat = gm != null ? gm.UserPlayerIndex : -1;
            if (gm != null) gm.NotifyConnectionLost();

            Debug.LogWarning($"[NetMgr] 호스트와 연결 끊김 — 복구 시작 (이전 호스트 {_oldHostId})");
            SetRecovery(true, "호스트와 연결이 끊겼습니다. 복구하는 중...");

            StopRecoverRoutine();
            _recoverRoutine = StartCoroutine(RecoverLoop());
        }

        private IEnumerator RecoverLoop()
        {
            var tick = new WaitForSecondsRealtime(0.5f);
            float t = 0f;
            float nextRetry = Mathf.Min(2f, reconnectRetryInterval);

            while (t < recoveryTimeout && IsRecovering)
            {
                yield return tick;
                t += 0.5f;

                var lobby = Lobby;
                if (lobby == null || !lobby.InLobby) break;
                if (NetworkClient.isConnected) yield break;   // OnClientConnect가 마무리

                bool attempting = NetworkClient.active;       // 접속 시도 중
                ulong me = lobby.LocalSteamId;
                ulong owner = lobby.GetLobbyOwnerId();
                ulong announced = lobby.GetHostSteamId();

                // 1) Steam이 나를 새 오너로 지정 → 내가 호스트를 이어받음
                if (enableHostMigration && owner == me && me != _oldHostId)
                {
                    if (attempting) { StopClient(); yield return null; }
                    _recoverRoutine = null;
                    BecomeHost();
                    yield break;
                }

                // 2) 다른 사람이 새 호스트로 공지됨 → 그쪽으로 접속
                if (enableHostMigration && announced != 0UL && announced != _oldHostId && announced != me)
                {
                    if (attempting)
                    {
                        // 사라진 이전 호스트로 재시도 중이었다면 취소하고 다음 틱에 새 호스트로
                        if (networkAddress != announced.ToString()) StopClient();
                        continue;
                    }
                    SetRecovery(true, "새 호스트에 연결하는 중...");
                    ConnectToHost(announced);
                    continue;
                }

                if (attempting) continue;

                // 3) 호스트는 그대로 (내 쪽 연결 문제) → 같은 호스트로 재접속
                if (enableReconnect && owner == _oldHostId && t >= nextRetry)
                {
                    nextRetry = t + reconnectRetryInterval;
                    SetRecovery(true, "다시 연결하는 중...");
                    ConnectToHost(_oldHostId);
                }
            }

            _recoverRoutine = null;
            if (IsRecovering) GiveUpRecovery("연결을 복구하지 못했습니다.");
        }

        private void StopRecoverRoutine()
        {
            if (_recoverRoutine != null)
            {
                StopCoroutine(_recoverRoutine);
                _recoverRoutine = null;
            }
        }

        private void StopRecovery()
        {
            IsRecovering = false;
            StopRecoverRoutine();
            if (_overlayFailsafe != null) { StopCoroutine(_overlayFailsafe); _overlayFailsafe = null; }
            _pendingResume = null;
        }

        private void GiveUpRecovery(string message)
        {
            Debug.LogWarning($"[NetMgr] 복구 포기: {message}");
            StopRecovery();

            GameStarted = false;
            _clientInGame = false;
            SceneContext.Set(SceneContextKeys.OnlineSession, false);

            if (NetworkClient.active) StopClient();
            NetworkGamePlayer.All.Clear();

            // 재접속 정보는 남겨 둠 → 온라인 패널의 [재접속] 버튼으로 다시 시도 가능
            Lobby?.LeaveLobby();

            SetRecovery(false, "");
            OnError?.Invoke(message);
            OnSessionEnded?.Invoke();
            ReturnToLobbyScene();
        }

        private void SetRecovery(bool active, string message)
        {
            if (RecoveryOverlayActive == active && RecoveryMessage == message) return;
            RecoveryOverlayActive = active;
            RecoveryMessage = message ?? "";
            OnRecoveryStateChanged?.Invoke(active, RecoveryMessage);
        }

        /// <summary>복구 후 씬 재로드 신호가 끝내 안 오는 경우를 대비한 안전장치.</summary>
        private void StartOverlayFailsafe()
        {
            if (_overlayFailsafe != null) StopCoroutine(_overlayFailsafe);
            _overlayFailsafe = StartCoroutine(OverlayFailsafe());
        }

        private IEnumerator OverlayFailsafe()
        {
            yield return new WaitForSecondsRealtime(migrationGatherTime + 15f);
            _overlayFailsafe = null;
            if (!IsRecovering) SetRecovery(false, "");
        }

        // =====================================================================
        //  호스트 이전 — 새 호스트 측
        // =====================================================================

        private void BecomeHost()
        {
            var lobby = Lobby;
            var resume = _pendingResume;
            if (lobby == null || resume == null)
            {
                GiveUpRecovery("게임 상태를 이어받지 못했습니다.");
                return;
            }

            IsRecovering = false;
            _pendingResume = null;
            ulong me = lobby.LocalSteamId;
            Debug.Log($"[NetMgr] ★ 호스트 이전 — 내가 새 호스트 (라운드 {resume.Round}부터 다시)");

            // 원래 자리 복구표: 사람이었던 모든 자리 (떠난 이전 호스트 포함 — 돌아오면 자리를 돌려줌)
            _reclaimSeats.Clear();
            _reclaimAnySeats.Clear();
            foreach (var sc in resume.Seats)
                if (sc.SteamId != 0UL) _reclaimSeats[sc.SteamId] = sc.Seat;
            if (!_reclaimSeats.ContainsKey(me) && _pendingLocalSeat >= 0)
                _reclaimSeats[me] = _pendingLocalSeat;

            _resumeData = resume;
            GameStarted = true;
            _playerRequested = false;
            SceneContext.Set(SceneContextKeys.OnlineSession, true);
            SceneContext.Set(SceneContextKeys.SelectedModeIndex, resume.ModeIndex);

            SetRecovery(true, "새 호스트가 되었습니다. 다른 플레이어를 기다리는 중...");

            StartHost();
            if (!NetworkServer.active)
            {
                GiveUpRecovery("호스트를 시작하지 못했습니다.");
                return;
            }

            // 다른 클라이언트들이 이 값을 보고 접속해 옴
            lobby.SetLobbyData(SteamLobbyManager.KEY_HOST, me.ToString());
            lobby.SetLobbyState("ingame");

            StartCoroutine(MigrationGather());
        }

        private IEnumerator MigrationGather()
        {
            float t = 0f;
            while (t < migrationGatherTime)
            {
                int expected = Lobby != null ? Lobby.GetLobbyMemberCount() : 1;
                if (_serverPlayers.Count >= expected && t >= 1f) break;
                t += Time.unscaledDeltaTime;
                yield return null;
            }

            if (!NetworkServer.active) yield break;
            Debug.Log($"[NetMgr] 호스트 이전: {_serverPlayers.Count}명 접속 — 게임 씬 다시 시작");
            StartOverlayFailsafe();
            ServerChangeScene(gameSceneName);
        }
    }
}

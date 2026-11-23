using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Steamworks;
using CardGame.Core;
using CardGame.Network;

namespace CardGame.UI
{
    /// <summary>
    /// 온라인 플레이 패널 (LobbyScene 안의 팝업). ModeSelectDialog와 같은 CanvasGroup 패턴.
    ///
    /// 뷰 3개를 토글:
    ///   menuView   : 방 만들기(공개/친구) · 방 찾기 · (개발용) LAN
    ///   browseView : 공개 로비 목록
    ///   roomView   : 좌석 4칸 · 모드 선택(호스트) · 준비 · 시작(호스트) · 초대 · 나가기
    ///
    /// CardDatabase는 여기서 불러오지 않음 — [게임 시작] 시 전원이 로딩씬에서 불러옴 (CardGameNetworkManager).
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public class OnlineLobbyPanel : MonoBehaviour
    {
        [Header("Data")]
        [SerializeField] private ModeDefinition[] modeDefinitions;

        [Header("Views")]
        [SerializeField] private GameObject menuView;
        [SerializeField] private GameObject browseView;
        [SerializeField] private GameObject roomView;

        [Header("Menu")]
        [SerializeField] private Button btnCreatePublic;
        [SerializeField] private Button btnCreateFriends;
        [SerializeField] private Button btnBrowse;
        [SerializeField] private Button btnClose;
        [Tooltip("(선택) 게임 중 튕겼을 때 같은 방으로 돌아가는 버튼. 돌아갈 방이 있을 때만 보임.")]
        [SerializeField] private Button btnRejoin;

        [Header("Menu — 개발용 LAN (Steam 없이 테스트, Transport를 KCP로 바꿨을 때)")]
        [SerializeField] private GameObject lanDevGroup;
        [SerializeField] private TMP_InputField lanIpInput;
        [SerializeField] private Button btnLanHost;
        [SerializeField] private Button btnLanJoin;

        [Header("Browse")]
        [SerializeField] private Transform lobbyListContent;
        [SerializeField] private LobbyListItem lobbyItemPrefab;
        [SerializeField] private Button btnRefresh;
        [SerializeField] private Button btnBrowseBack;
        [SerializeField] private TMP_Text browseEmptyText;

        [Header("Room — 좌석 (최대 4)")]
        [SerializeField] private TMP_Text[] slotNameTexts = new TMP_Text[4];
        [SerializeField] private GameObject[] slotReadyMarks = new GameObject[4];
        [SerializeField] private GameObject[] slotHostMarks = new GameObject[4];

        [Header("Room — 컨트롤")]
        [SerializeField] private TMP_Dropdown modeDropdown;
        [SerializeField] private Button btnReady;
        [SerializeField] private TMP_Text btnReadyLabel;
        [SerializeField] private Button btnStart;
        [SerializeField] private Button btnInvite;
        [Tooltip("친구 초대 창. 비어 있으면 Steam 오버레이 초대창을 엶.")]
        [SerializeField] private FriendInvitePanel friendInvitePanel;
        [SerializeField] private Button btnLeave;
        [SerializeField] private TMP_Text statusText;

        [Header("Fade")]
        [SerializeField] private float fadeDuration = 0.2f;

        private CanvasGroup _cg;
        private bool _isShowing;
        private bool _hooked;
        private readonly List<GameObject> _listItems = new List<GameObject>();

        private SteamLobbyManager Lobby => SteamLobbyManager.Instance;
        private CardGameNetworkManager Net => CardGameNetworkManager.singleton;

        // =====================================================================
        //  Lifecycle
        // =====================================================================

        private void Awake()
        {
            _cg = GetComponent<CanvasGroup>();
            if (_cg == null) _cg = gameObject.AddComponent<CanvasGroup>();
            HideImmediate();

            // 버튼 바인딩
            Bind(btnCreatePublic, () => CreateLobby(false));
            Bind(btnCreateFriends, () => CreateLobby(true));
            Bind(btnBrowse, OpenBrowse);
            Bind(btnClose, Hide);
            Bind(btnRejoin, RejoinLastGame);
            Bind(btnRefresh, RefreshLobbyList);
            Bind(btnBrowseBack, ShowMenuView);
            Bind(btnReady, ToggleReady);
            Bind(btnStart, StartGame);
            Bind(btnInvite, OpenInvite);
            Bind(btnLeave, LeaveRoom);
            Bind(btnLanHost, LanHost);
            Bind(btnLanJoin, LanJoin);

            if (modeDropdown != null)
            {
                modeDropdown.ClearOptions();
                var opts = new List<string>();
                if (modeDefinitions != null)
                    foreach (var m in modeDefinitions) opts.Add(m != null ? m.displayName : "-");
                if (opts.Count == 0) opts.Add("기본");
                modeDropdown.AddOptions(opts);
                modeDropdown.onValueChanged.AddListener(OnModeDropdownChanged);
            }

            if (lanDevGroup != null) lanDevGroup.SetActive(Application.isEditor || Debug.isDebugBuild);
        }

        private static void Bind(Button b, UnityEngine.Events.UnityAction a)
        {
            if (b == null) return;
            b.onClick.RemoveAllListeners();
            b.onClick.AddListener(a);
        }

        private void Start()
        {
            // 초대 수락은 패널이 닫혀 있어도 받아야 함
            if (Lobby != null)
            {
                Lobby.OnInviteReceived += HandleInviteReceived;
                if (Lobby.TryConsumePendingInvite(out var pending)) AcceptInvite(pending);
            }
        }

        private void OnDestroy()
        {
            Unhook();
            if (Lobby != null) Lobby.OnInviteReceived -= HandleInviteReceived;
        }

        private void HandleInviteReceived(CSteamID lobbyId)
        {
            if (Lobby != null && Lobby.TryConsumePendingInvite(out var id)) AcceptInvite(id);
        }

        /// <summary>친구 초대 수락 → 온라인 창을 열고 그 방에 참가.</summary>
        private void AcceptInvite(CSteamID lobbyId)
        {
            if (Lobby == null || !Lobby.IsSteamReady) return;
            if (Net != null && Net.InSession) Net.LeaveSession();   // 다른 방에 있었으면 나감
            Show();
            SetView(menuView);
            SetStatus("초대받은 방에 참가하는 중...");
            Lobby.JoinLobby(lobbyId);
        }

        private void OpenInvite()
        {
            if (friendInvitePanel != null) friendInvitePanel.Show();
            else Lobby?.OpenInviteOverlay();
        }

        private void Hook()
        {
            if (_hooked) return;
            _hooked = true;

            if (Lobby != null)
            {
                Lobby.OnLobbyEntered += OnLobbyEntered;
                Lobby.OnLobbyEnterFailed += OnLobbyFailed;
                Lobby.OnLobbyListReceived += OnLobbyListReceived;
                Lobby.OnLobbyMembersChanged += RefreshRoom;
                Lobby.OnLobbyDataChanged += OnLobbyDataChanged;
                Lobby.OnLeftLobby += OnLeftLobby;
            }
            NetworkGamePlayer.OnPlayersChanged += RefreshRoom;
            if (Net != null)
            {
                Net.OnStatus += SetStatus;
                Net.OnError += OnNetError;
                Net.OnSessionEnded += OnSessionEnded;
            }
        }

        private void Unhook()
        {
            if (!_hooked) return;
            _hooked = false;

            if (Lobby != null)
            {
                Lobby.OnLobbyEntered -= OnLobbyEntered;
                Lobby.OnLobbyEnterFailed -= OnLobbyFailed;
                Lobby.OnLobbyListReceived -= OnLobbyListReceived;
                Lobby.OnLobbyMembersChanged -= RefreshRoom;
                Lobby.OnLobbyDataChanged -= OnLobbyDataChanged;
                Lobby.OnLeftLobby -= OnLeftLobby;
            }
            NetworkGamePlayer.OnPlayersChanged -= RefreshRoom;
            if (Net != null)
            {
                Net.OnStatus -= SetStatus;
                Net.OnError -= OnNetError;
                Net.OnSessionEnded -= OnSessionEnded;
            }
        }

        // =====================================================================
        //  Show / Hide
        // =====================================================================

        public void Show()
        {
            if (_isShowing) return;
            _isShowing = true;

            SceneContext.Set(SceneContextKeys.OnlineSession, true);
            Hook();

            // 이미 세션 중이면 방 화면으로 (게임 끝나고 돌아온 경우 등)
            if (Net != null && Net.InSession) ShowRoomView();
            else ShowMenuView();

            _cg.DOKill();
            _cg.blocksRaycasts = true;
            _cg.interactable = true;
            _cg.DOFade(1f, fadeDuration).SetLink(gameObject);
        }

        public void Hide()
        {
            if (!_isShowing) return;
            _isShowing = false;

            if (Net != null && Net.InSession) Net.LeaveSession();
            else Lobby?.LeaveLobby();

            SceneContext.Set(SceneContextKeys.OnlineSession, false);

            _cg.DOKill();
            _cg.blocksRaycasts = false;
            _cg.interactable = false;
            _cg.DOFade(0f, fadeDuration).SetLink(gameObject);
        }

        private void HideImmediate()
        {
            _cg.alpha = 0f;
            _cg.blocksRaycasts = false;
            _cg.interactable = false;
            _isShowing = false;
        }

        public bool IsShowing => _isShowing;

        // =====================================================================
        //  Views
        // =====================================================================

        private void ShowMenuView()
        {
            SetView(menuView);
            if (btnRejoin != null) btnRejoin.gameObject.SetActive(Lobby != null && Lobby.IsSteamReady && Lobby.HasRejoinInfo);
            SetStatus(Lobby != null && Lobby.IsSteamReady ? $"Steam: {Lobby.LocalPersonaName}" : "Steam 미연결 (LAN 테스트만 가능)");
        }

        private void OpenBrowse()
        {
            SetView(browseView);
            RefreshLobbyList();
        }

        private void ShowRoomView()
        {
            SetView(roomView);
            RefreshRoom();
            ApplyModeFromLobbyData();
        }

        private void SetView(GameObject active)
        {
            if (menuView != null) menuView.SetActive(active == menuView);
            if (browseView != null) browseView.SetActive(active == browseView);
            if (roomView != null) roomView.SetActive(active == roomView);
        }

        private void SetStatus(string msg)
        {
            if (statusText != null) statusText.text = msg;
        }

        private void OnNetError(string msg)
        {
            SetStatus($"<color=#FF6B6B>{msg}</color>");
            Debug.LogWarning($"[OnlineLobby] {msg}");
        }

        // =====================================================================
        //  Create / Browse / Join
        // =====================================================================

        private void CreateLobby(bool friendsOnly)
        {
            if (Lobby == null || !Lobby.IsSteamReady) { OnNetError("Steam이 실행 중이 아닙니다."); return; }
            SetStatus("방 생성 중...");
            Lobby.CreateLobby(friendsOnly);
        }

        private void RejoinLastGame()
        {
            if (Lobby == null || !Lobby.IsSteamReady) { OnNetError("Steam이 실행 중이 아닙니다."); return; }
            SetStatus("이전 게임에 다시 참가하는 중...");
            if (!Lobby.RejoinLastLobby())
            {
                OnNetError("돌아갈 게임이 없습니다.");
                if (btnRejoin != null) btnRejoin.gameObject.SetActive(false);
            }
        }

        private void RefreshLobbyList()
        {
            ClearList();
            if (browseEmptyText != null) { browseEmptyText.gameObject.SetActive(true); browseEmptyText.text = "검색 중..."; }
            if (Lobby == null || !Lobby.IsSteamReady) { if (browseEmptyText != null) browseEmptyText.text = "Steam 미연결"; return; }
            Lobby.RequestLobbyList();
        }

        private void OnLobbyListReceived(List<SteamLobbyManager.LobbyInfo> lobbies)
        {
            if (!_isShowing) return;
            ClearList();

            if (lobbies.Count == 0)
            {
                if (browseEmptyText != null) { browseEmptyText.gameObject.SetActive(true); browseEmptyText.text = "열린 방이 없습니다."; }
                return;
            }
            if (browseEmptyText != null) browseEmptyText.gameObject.SetActive(false);

            if (lobbyItemPrefab == null || lobbyListContent == null) return;
            foreach (var info in lobbies)
            {
                var item = Instantiate(lobbyItemPrefab, lobbyListContent);
                item.Bind(info, JoinLobby);
                _listItems.Add(item.gameObject);
            }
        }

        private void ClearList()
        {
            foreach (var go in _listItems) if (go != null) Destroy(go);
            _listItems.Clear();
        }

        private void JoinLobby(CSteamID id)
        {
            SetStatus("방 참가 중...");
            Lobby?.JoinLobby(id);
        }

        private void OnLobbyEntered(CSteamID id, bool isOwner)
        {
            if (!_isShowing) return;
            SetStatus(isOwner ? "방 생성 완료 — 친구를 초대하세요" : "방 참가 완료");
            ShowRoomView();
        }

        private void OnLobbyFailed(string msg)
        {
            OnNetError(msg);
            if (btnRejoin != null) btnRejoin.gameObject.SetActive(Lobby != null && Lobby.HasRejoinInfo);
        }

        private void OnLeftLobby()
        {
            if (_isShowing) ShowMenuView();
        }

        private void OnSessionEnded()
        {
            if (_isShowing) ShowMenuView();
        }

        // =====================================================================
        //  Room
        // =====================================================================

        private void RefreshRoom()
        {
            if (!_isShowing || roomView == null || !roomView.activeSelf) return;

            bool isHost = Net != null && Net.IsHostMode;
            var local = NetworkGamePlayer.Local;

            for (int s = 0; s < 4; s++)
            {
                var np = NetworkGamePlayer.FindBySeat(s);
                string name;
                bool ready = false, host = false;

                if (np != null)
                {
                    name = string.IsNullOrEmpty(np.displayName) ? $"Player {s + 1}" : np.displayName;
                    if (np.isLocalPlayer) name += " (나)";
                    ready = np.isReady;
                    host = np.isHostPlayer;
                }
                else name = "<color=#888888>BOT</color>";

                if (s < slotNameTexts.Length && slotNameTexts[s] != null) slotNameTexts[s].text = name;
                if (s < slotReadyMarks.Length && slotReadyMarks[s] != null) slotReadyMarks[s].SetActive(ready);
                if (s < slotHostMarks.Length && slotHostMarks[s] != null) slotHostMarks[s].SetActive(host);
            }

            // 준비 버튼 (호스트는 항상 준비 → 비활성)
            if (btnReady != null)
            {
                bool canToggle = local != null && !local.isHostPlayer;
                btnReady.interactable = canToggle;
                if (btnReadyLabel != null)
                    btnReadyLabel.text = local != null && local.isReady ? "준비 취소" : "준비";
            }

            // 시작 버튼 (호스트 + 전원 준비)
            if (btnStart != null)
            {
                btnStart.gameObject.SetActive(isHost);
                btnStart.interactable = isHost && NetworkGamePlayer.AllReady();
            }

            if (btnInvite != null) btnInvite.interactable = Lobby != null && Lobby.InLobby;
            if (modeDropdown != null) modeDropdown.interactable = isHost;
        }

        private void ToggleReady()
        {
            var local = NetworkGamePlayer.Local;
            if (local == null) return;
            local.CmdSetReady(!local.isReady);
        }

        private void StartGame()
        {
            if (Net == null) return;
            SetStatus("게임 시작...");
            SyncSelectedModeToContext(modeDropdown != null ? modeDropdown.value : 0);
            Net.StartGameScene();
        }

        private void LeaveRoom()
        {
            if (Net != null && Net.InSession) Net.LeaveSession();
            else Lobby?.LeaveLobby();
            ShowMenuView();
        }

        // ---- 모드 동기화 (Steam 로비 데이터) ----

        private void OnModeDropdownChanged(int idx)
        {
            if (Net == null || !Net.IsHostMode) return;
            Lobby?.SetLobbyData(SteamLobbyManager.KEY_MODE, idx.ToString());
            SyncSelectedModeToContext(idx);
        }

        private void OnLobbyDataChanged()
        {
            if (!_isShowing) return;
            ApplyModeFromLobbyData();
        }

        private void ApplyModeFromLobbyData()
        {
            if (Lobby == null || !Lobby.InLobby) return;
            var s = Lobby.GetLobbyData(SteamLobbyManager.KEY_MODE);
            if (!int.TryParse(s, out var idx)) idx = 0;
            if (modeDropdown != null && modeDropdown.value != idx && idx < modeDropdown.options.Count)
                modeDropdown.SetValueWithoutNotify(idx);
            SyncSelectedModeToContext(idx);
        }

        private void SyncSelectedModeToContext(int idx)
        {
            SceneContext.Set(SceneContextKeys.SelectedModeIndex, idx);
            if (modeDefinitions != null && idx >= 0 && idx < modeDefinitions.Length && modeDefinitions[idx] != null)
                SceneContext.Set(SceneContextKeys.SelectedMode, modeDefinitions[idx]);
        }

        // =====================================================================
        //  개발용 LAN (Transport를 KCP/Telepathy로 바꿨을 때만 동작)
        // =====================================================================

        private void LanHost()
        {
            if (Net == null) return;
            SetStatus("LAN 호스트 시작");
            Net.StartHost();
            ShowRoomView();
        }

        private void LanJoin()
        {
            if (Net == null) return;
            string ip = lanIpInput != null && !string.IsNullOrWhiteSpace(lanIpInput.text) ? lanIpInput.text.Trim() : "localhost";
            Net.networkAddress = ip;
            SetStatus($"LAN 참가: {ip}");
            Net.StartClient();
            ShowRoomView();
        }
    }
}

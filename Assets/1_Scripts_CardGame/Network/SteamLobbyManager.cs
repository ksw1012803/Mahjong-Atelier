using System;
using System.Collections.Generic;
using UnityEngine;
using Steamworks;

namespace CardGame.Network
{
    /// <summary>
    /// Steam 로비 수명주기 래퍼 (Steamworks.NET).
    ///
    /// 역할: 로비 생성/참가/탈퇴/목록, 친구 초대 오버레이, 로비 데이터(호스트 SteamID·이름·모드).
    /// Mirror 연결 자체는 CardGameNetworkManager가 이 클래스의 이벤트를 받아 처리.
    ///
    /// 배치: BootstrapScene, SteamManager(Steamworks.NET 제공)와 같은 GameObject 또는 형제.
    /// </summary>
    public class SteamLobbyManager : MonoBehaviour
    {
        public static SteamLobbyManager Instance { get; private set; }

        // 로비 데이터 키
        public const string KEY_GAME = "game";
        public const string KEY_HOST = "HostAddress";
        public const string KEY_NAME = "name";
        public const string KEY_MODE = "mode";
        public const string KEY_STATE = "state";          // "lobby" | "ingame"
        public const string GAME_TAG = "SEVENMAID";

        [Header("Settings")]
        [SerializeField] private int maxMembers = 4;

        public struct LobbyInfo
        {
            public CSteamID id;
            public string name;
            public int members;
            public int maxMembers;
            public string state;
        }

        public CSteamID CurrentLobby { get; private set; } = CSteamID.Nil;
        public bool InLobby => CurrentLobby != CSteamID.Nil && CurrentLobby.IsValid();
        public bool IsSteamReady => SteamManager.Initialized;

        public bool IsLobbyOwner =>
            InLobby && SteamMatchmaking.GetLobbyOwner(CurrentLobby) == SteamUser.GetSteamID();

        // ---- 이벤트 ----
        public event Action<CSteamID> OnLobbyCreated;                 // 호스트 전용
        public event Action<CSteamID, bool> OnLobbyEntered;           // (lobby, isOwner) — 호스트/클라 모두
        public event Action<string> OnLobbyEnterFailed;
        public event Action<List<LobbyInfo>> OnLobbyListReceived;
        public event Action OnLobbyMembersChanged;
        public event Action OnLobbyDataChanged;
        public event Action OnLeftLobby;

        // ---- Steam 콜백 ----
        private Callback<LobbyCreated_t> _cbLobbyCreated;
        private Callback<GameLobbyJoinRequested_t> _cbJoinRequested;
        private Callback<LobbyEnter_t> _cbLobbyEnter;
        private Callback<LobbyChatUpdate_t> _cbChatUpdate;
        private Callback<LobbyDataUpdate_t> _cbDataUpdate;
        private CallResult<LobbyMatchList_t> _crLobbyList;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;

            Transform root = transform;
            while (root.parent != null) root = root.parent;
            DontDestroyOnLoad(root.gameObject);
        }

        private void Start()
        {
            if (!SteamManager.Initialized)
            {
                Debug.LogError("[SteamLobby] SteamManager 미초기화. steam_appid.txt / Steam 클라이언트 실행 확인.");
                return;
            }

            _cbLobbyCreated = Callback<LobbyCreated_t>.Create(HandleLobbyCreated);
            _cbJoinRequested = Callback<GameLobbyJoinRequested_t>.Create(HandleJoinRequested);
            _cbLobbyEnter = Callback<LobbyEnter_t>.Create(HandleLobbyEnter);
            _cbChatUpdate = Callback<LobbyChatUpdate_t>.Create(HandleChatUpdate);
            _cbDataUpdate = Callback<LobbyDataUpdate_t>.Create(HandleDataUpdate);
            _crLobbyList = CallResult<LobbyMatchList_t>.Create(HandleLobbyList);

            Debug.Log($"[SteamLobby] 준비 완료 — {SteamFriends.GetPersonaName()} ({SteamUser.GetSteamID()})");
        }

        // =====================================================================
        //  Public API
        // =====================================================================

        public ulong LocalSteamId => IsSteamReady ? SteamUser.GetSteamID().m_SteamID : 0UL;
        public string LocalPersonaName => IsSteamReady ? SteamFriends.GetPersonaName() : "Player";

        public void CreateLobby(bool friendsOnly)
        {
            if (!IsSteamReady) { OnLobbyEnterFailed?.Invoke("Steam 미초기화"); return; }
            if (InLobby) LeaveLobby();

            var type = friendsOnly ? ELobbyType.k_ELobbyTypeFriendsOnly : ELobbyType.k_ELobbyTypePublic;
            SteamMatchmaking.CreateLobby(type, maxMembers);
            Debug.Log($"[SteamLobby] 로비 생성 요청 ({type}, 최대 {maxMembers})");
        }

        public void JoinLobby(CSteamID lobbyId)
        {
            if (!IsSteamReady) { OnLobbyEnterFailed?.Invoke("Steam 미초기화"); return; }
            if (InLobby) LeaveLobby();
            SteamMatchmaking.JoinLobby(lobbyId);
            Debug.Log($"[SteamLobby] 로비 참가 요청: {lobbyId}");
        }

        public void LeaveLobby()
        {
            if (!InLobby) return;
            var id = CurrentLobby;
            SteamMatchmaking.LeaveLobby(id);
            CurrentLobby = CSteamID.Nil;
            Debug.Log($"[SteamLobby] 로비 탈퇴: {id}");
            OnLeftLobby?.Invoke();
        }

        public void RequestLobbyList()
        {
            if (!IsSteamReady) return;
            SteamMatchmaking.AddRequestLobbyListStringFilter(KEY_GAME, GAME_TAG, ELobbyComparison.k_ELobbyComparisonEqual);
            SteamMatchmaking.AddRequestLobbyListStringFilter(KEY_STATE, "lobby", ELobbyComparison.k_ELobbyComparisonEqual);
            SteamMatchmaking.AddRequestLobbyListDistanceFilter(ELobbyDistanceFilter.k_ELobbyDistanceFilterWorldwide);
            SteamMatchmaking.AddRequestLobbyListResultCountFilter(50);
            var call = SteamMatchmaking.RequestLobbyList();
            _crLobbyList.Set(call);
        }

        public void OpenInviteOverlay()
        {
            if (!InLobby) return;
            SteamFriends.ActivateGameOverlayInviteDialog(CurrentLobby);
        }

        public void SetLobbyData(string key, string value)
        {
            if (!InLobby || !IsLobbyOwner) return;
            SteamMatchmaking.SetLobbyData(CurrentLobby, key, value);
        }

        public string GetLobbyData(string key)
        {
            if (!InLobby) return string.Empty;
            return SteamMatchmaking.GetLobbyData(CurrentLobby, key);
        }

        public void SetLobbyJoinable(bool joinable)
        {
            if (!InLobby || !IsLobbyOwner) return;
            SteamMatchmaking.SetLobbyJoinable(CurrentLobby, joinable);
            SteamMatchmaking.SetLobbyData(CurrentLobby, KEY_STATE, joinable ? "lobby" : "ingame");
        }

        /// <summary>참가 가능 여부는 그대로 두고 상태 문자열만 변경 ("lobby" | "ingame"). 목록 검색은 "lobby"만 노출.</summary>
        public void SetLobbyState(string state)
        {
            if (!InLobby || !IsLobbyOwner) return;
            SteamMatchmaking.SetLobbyData(CurrentLobby, KEY_STATE, state);
        }

        /// <summary>현재 Steam 로비 오너의 SteamID. 오너가 나가면 Steam이 다른 멤버에게 자동 이전.</summary>
        public ulong GetLobbyOwnerId()
        {
            if (!InLobby) return 0UL;
            return SteamMatchmaking.GetLobbyOwner(CurrentLobby).m_SteamID;
        }

        // ---- 재접속 정보 (게임 중 튕겼을 때 같은 방으로 돌아가기) ----
        private const string PREF_REJOIN = "SM_RejoinLobby";

        public bool HasRejoinInfo => !string.IsNullOrEmpty(PlayerPrefs.GetString(PREF_REJOIN, ""));

        public void SaveRejoinInfo()
        {
            if (!InLobby) return;
            PlayerPrefs.SetString(PREF_REJOIN, CurrentLobby.m_SteamID.ToString());
            PlayerPrefs.Save();
        }

        public void ClearRejoinInfo()
        {
            if (!PlayerPrefs.HasKey(PREF_REJOIN)) return;
            PlayerPrefs.DeleteKey(PREF_REJOIN);
            PlayerPrefs.Save();
        }

        /// <summary>마지막으로 게임하던 방에 다시 참가 시도. 방이 없어졌으면 OnLobbyEnterFailed.</summary>
        public bool RejoinLastLobby()
        {
            if (!IsSteamReady) return false;
            if (!ulong.TryParse(PlayerPrefs.GetString(PREF_REJOIN, ""), out var id) || id == 0UL) return false;
            _rejoinAttempt = true;
            JoinLobby(new CSteamID(id));
            return true;
        }

        private bool _rejoinAttempt;

        public ulong GetHostSteamId()
        {
            var s = GetLobbyData(KEY_HOST);
            return ulong.TryParse(s, out var id) ? id : 0UL;
        }

        public int GetLobbyMemberCount() => InLobby ? SteamMatchmaking.GetNumLobbyMembers(CurrentLobby) : 0;

        public List<(CSteamID id, string name)> GetLobbyMembers()
        {
            var list = new List<(CSteamID, string)>();
            if (!InLobby) return list;
            int n = SteamMatchmaking.GetNumLobbyMembers(CurrentLobby);
            for (int i = 0; i < n; i++)
            {
                var m = SteamMatchmaking.GetLobbyMemberByIndex(CurrentLobby, i);
                list.Add((m, SteamFriends.GetFriendPersonaName(m)));
            }
            return list;
        }

        // =====================================================================
        //  Steam Callbacks
        // =====================================================================

        private void HandleLobbyCreated(LobbyCreated_t cb)
        {
            if (cb.m_eResult != EResult.k_EResultOK)
            {
                Debug.LogError($"[SteamLobby] 로비 생성 실패: {cb.m_eResult}");
                OnLobbyEnterFailed?.Invoke($"로비 생성 실패: {cb.m_eResult}");
                return;
            }

            CurrentLobby = new CSteamID(cb.m_ulSteamIDLobby);
            SteamMatchmaking.SetLobbyData(CurrentLobby, KEY_GAME, GAME_TAG);
            SteamMatchmaking.SetLobbyData(CurrentLobby, KEY_HOST, SteamUser.GetSteamID().ToString());
            SteamMatchmaking.SetLobbyData(CurrentLobby, KEY_NAME, $"{SteamFriends.GetPersonaName()}의 방");
            SteamMatchmaking.SetLobbyData(CurrentLobby, KEY_STATE, "lobby");
            SteamMatchmaking.SetLobbyData(CurrentLobby, KEY_MODE, "0");

            Debug.Log($"[SteamLobby] 로비 생성됨: {CurrentLobby}");
            OnLobbyCreated?.Invoke(CurrentLobby);
            // LobbyEnter_t 도 곧바로 뒤따라 옴 (owner=true)
        }

        private void HandleJoinRequested(GameLobbyJoinRequested_t cb)
        {
            Debug.Log($"[SteamLobby] 초대/참가 요청 수락: {cb.m_steamIDLobby}");
            JoinLobby(cb.m_steamIDLobby);
        }

        private void HandleLobbyEnter(LobbyEnter_t cb)
        {
            var response = (EChatRoomEnterResponse)cb.m_EChatRoomEnterResponse;
            if (response != EChatRoomEnterResponse.k_EChatRoomEnterResponseSuccess)
            {
                Debug.LogError($"[SteamLobby] 로비 입장 실패: {response}");
                CurrentLobby = CSteamID.Nil;
                if (_rejoinAttempt) { _rejoinAttempt = false; ClearRejoinInfo(); }
                OnLobbyEnterFailed?.Invoke($"입장 실패: {response}");
                return;
            }

            _rejoinAttempt = false;
            CurrentLobby = new CSteamID(cb.m_ulSteamIDLobby);
            bool owner = IsLobbyOwner;
            Debug.Log($"[SteamLobby] 로비 입장: {CurrentLobby} (owner={owner})");
            OnLobbyEntered?.Invoke(CurrentLobby, owner);
        }

        private void HandleChatUpdate(LobbyChatUpdate_t cb)
        {
            if (!InLobby || cb.m_ulSteamIDLobby != CurrentLobby.m_SteamID) return;
            OnLobbyMembersChanged?.Invoke();
        }

        private void HandleDataUpdate(LobbyDataUpdate_t cb)
        {
            if (!InLobby || cb.m_ulSteamIDLobby != CurrentLobby.m_SteamID) return;
            OnLobbyDataChanged?.Invoke();
        }

        private void HandleLobbyList(LobbyMatchList_t cb, bool ioFailure)
        {
            var result = new List<LobbyInfo>();
            if (ioFailure)
            {
                Debug.LogWarning("[SteamLobby] 로비 목록 IO 실패");
                OnLobbyListReceived?.Invoke(result);
                return;
            }

            for (int i = 0; i < cb.m_nLobbiesMatching; i++)
            {
                var id = SteamMatchmaking.GetLobbyByIndex(i);
                result.Add(new LobbyInfo
                {
                    id = id,
                    name = SteamMatchmaking.GetLobbyData(id, KEY_NAME),
                    members = SteamMatchmaking.GetNumLobbyMembers(id),
                    maxMembers = SteamMatchmaking.GetLobbyMemberLimit(id),
                    state = SteamMatchmaking.GetLobbyData(id, KEY_STATE),
                });
            }

            Debug.Log($"[SteamLobby] 로비 목록 {result.Count}개");
            OnLobbyListReceived?.Invoke(result);
        }

        private void OnApplicationQuit()
        {
            if (InLobby) LeaveLobby();
        }
    }
}

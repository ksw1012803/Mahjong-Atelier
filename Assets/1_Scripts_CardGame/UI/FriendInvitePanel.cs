using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Steamworks;
using CardGame.Network;

namespace CardGame.UI
{
    /// <summary>
    /// Steam 친구 초대 창 — 친구 목록(프로필 사진·이름·상태)과 [초대] 버튼.
    /// Steam 오버레이 없이 SteamMatchmaking.InviteUserToLobby로 초대하므로 오버레이를 꺼도 동작.
    /// 초대받은 친구는 Steam 친구 채팅/알림에서 '참가'를 누르면 게임이 그 방으로 들어옴 (SteamLobbyManager.OnInviteReceived).
    ///
    /// 정렬: 이 게임 플레이 중 → 온라인 → 자리 비움 등 → 오프라인 (오프라인은 초대 불가).
    /// CanvasGroup 패턴. 행은 itemTemplate(비활성) 복제.
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public class FriendInvitePanel : MonoBehaviour
    {
        [Header("목록")]
        [SerializeField] private Transform listContent;
        [Tooltip("행 템플릿 (비활성). 자식: Avatar(RawImage), NameText, StatusText, InviteButton(Button, 자식 Label)")]
        [SerializeField] private GameObject itemTemplate;
        [SerializeField] private TMP_Text emptyText;
        [SerializeField] private TMP_Text headerCountText;

        [Header("버튼")]
        [SerializeField] private Button refreshButton;
        [SerializeField] private Button overlayButton;
        [SerializeField] private Button closeButton;
        [SerializeField] private Button dimButton;

        [Header("색")]
        [SerializeField] private Color playingColor = new Color(0.85f, 0.71f, 0.42f);
        [SerializeField] private Color onlineColor = new Color(0.45f, 0.85f, 0.5f);
        [SerializeField] private Color awayColor = new Color(0.75f, 0.68f, 0.55f);
        [SerializeField] private Color offlineColor = new Color(0.55f, 0.5f, 0.48f);

        [SerializeField] private int maxFriends = 150;
        [SerializeField] private float fadeDuration = 0.18f;

        private CanvasGroup _cg;
        private bool _isShowing;
        private int _shownFrame = -1;
        private readonly List<GameObject> _rows = new List<GameObject>();
        private readonly HashSet<ulong> _invited = new HashSet<ulong>();
        private CSteamID _invitedLobby = CSteamID.Nil;
        private readonly Dictionary<ulong, RawImage> _pendingAvatars = new Dictionary<ulong, RawImage>();
        private readonly Dictionary<RawImage, Color> _avatarTint = new Dictionary<RawImage, Color>();
        private static readonly Dictionary<int, Texture2D> AvatarCache = new Dictionary<int, Texture2D>();
        private Callback<AvatarImageLoaded_t> _cbAvatar;

        public bool IsShowing => _isShowing;
        private SteamLobbyManager Lobby => SteamLobbyManager.Instance;

        private struct Friend
        {
            public CSteamID id;
            public string name;
            public EPersonaState state;
            public bool playingThis;
            public int Rank => playingThis ? 0 : state == EPersonaState.k_EPersonaStateOnline ? 1
                             : state == EPersonaState.k_EPersonaStateOffline ? 3 : 2;
        }

        private void Awake()
        {
            _cg = GetComponent<CanvasGroup>();
            _cg.alpha = 0f;
            _cg.blocksRaycasts = false;
            _cg.interactable = false;

            if (itemTemplate != null) itemTemplate.SetActive(false);
            if (refreshButton != null) refreshButton.onClick.AddListener(Rebuild);
            if (overlayButton != null) overlayButton.onClick.AddListener(() => Lobby?.OpenInviteOverlay());
            if (closeButton != null) closeButton.onClick.AddListener(Hide);
            if (dimButton != null) dimButton.onClick.AddListener(Hide);
        }

        public void Show()
        {
            if (_isShowing) return;
            _isShowing = true;
            _shownFrame = Time.frameCount;

            // 다른 방으로 옮겼으면 '초대함' 기록 초기화
            var lobby = Lobby != null ? Lobby.CurrentLobby : CSteamID.Nil;
            if (lobby != _invitedLobby) { _invited.Clear(); _invitedLobby = lobby; }

            Rebuild();
            _cg.DOKill();
            _cg.blocksRaycasts = true;
            _cg.interactable = true;
            _cg.DOFade(1f, fadeDuration).SetLink(gameObject);
        }

        public void Hide()
        {
            if (!_isShowing) return;
            _isShowing = false;
            _cg.DOKill();
            _cg.blocksRaycasts = false;
            _cg.interactable = false;
            _cg.DOFade(0f, fadeDuration).SetLink(gameObject);
        }

        // =====================================================================

        private void Rebuild()
        {
            foreach (var r in _rows) if (r != null) Destroy(r);
            _rows.Clear();
            _pendingAvatars.Clear();
            _avatarTint.Clear();

            if (!SteamManager.Initialized)
            {
                SetEmpty("Steam이 실행 중이 아닙니다.");
                return;
            }
            if (Lobby == null || !Lobby.InLobby)
            {
                SetEmpty("방에 들어간 뒤 초대할 수 있습니다.");
                return;
            }
            if (_cbAvatar == null) _cbAvatar = Callback<AvatarImageLoaded_t>.Create(OnAvatarLoaded);

            var friends = CollectFriends();
            int online = 0;
            foreach (var f in friends) if (f.state != EPersonaState.k_EPersonaStateOffline) online++;
            if (headerCountText != null) headerCountText.text = $"온라인 {online} / 전체 {friends.Count}";

            if (friends.Count == 0) { SetEmpty("Steam 친구가 없습니다."); return; }
            if (emptyText != null) emptyText.gameObject.SetActive(false);

            foreach (var f in friends) _rows.Add(CreateRow(f));
        }

        private List<Friend> CollectFriends()
        {
            var list = new List<Friend>();
            var flags = EFriendFlags.k_EFriendFlagImmediate;
            int count = SteamFriends.GetFriendCount(flags);
            var myApp = SteamUtils.GetAppID();

            for (int i = 0; i < count; i++)
            {
                var id = SteamFriends.GetFriendByIndex(i, flags);
                bool playing = SteamFriends.GetFriendGamePlayed(id, out var game) && game.m_gameID.AppID() == myApp;
                list.Add(new Friend
                {
                    id = id,
                    name = SteamFriends.GetFriendPersonaName(id),
                    state = SteamFriends.GetFriendPersonaState(id),
                    playingThis = playing,
                });
            }

            list.Sort((a, b) =>
            {
                int c = a.Rank.CompareTo(b.Rank);
                return c != 0 ? c : string.Compare(a.name, b.name, System.StringComparison.CurrentCultureIgnoreCase);
            });
            if (list.Count > maxFriends) list.RemoveRange(maxFriends, list.Count - maxFriends);
            return list;
        }

        private GameObject CreateRow(Friend f)
        {
            var row = Instantiate(itemTemplate, listContent);
            row.name = "Friend_" + f.name;
            row.SetActive(true);

            bool offline = f.state == EPersonaState.k_EPersonaStateOffline;

            var nameT = Find<TMP_Text>(row, "NameText");
            if (nameT != null) { nameT.text = f.name; nameT.alpha = offline ? 0.5f : 1f; }

            var statusT = Find<TMP_Text>(row, "StatusText");
            if (statusT != null)
            {
                statusT.text = f.playingThis ? "SEVEN MAID 플레이 중" : StateLabel(f.state);
                statusT.color = f.playingThis ? playingColor : f.state == EPersonaState.k_EPersonaStateOnline ? onlineColor
                              : offline ? offlineColor : awayColor;
            }

            var avatar = Find<RawImage>(row, "Avatar");
            if (avatar != null)
            {
                _avatarTint[avatar] = offline ? new Color(1f, 1f, 1f, 0.45f) : Color.white;
                avatar.color = Color.clear;   // 사진이 오기 전엔 숨김 (뒤의 빈 배경이 보임)
                SetAvatar(f.id, avatar);
            }

            var btn = Find<Button>(row, "InviteButton");
            if (btn != null)
            {
                var label = btn.GetComponentInChildren<TMP_Text>(true);
                bool already = _invited.Contains(f.id.m_SteamID);
                btn.interactable = !offline && !already;
                if (label != null) label.text = already ? "초대함" : "초대";
                var id = f.id;
                btn.onClick.AddListener(() =>
                {
                    if (Lobby == null || !Lobby.InviteFriend(id)) return;
                    _invited.Add(id.m_SteamID);
                    btn.interactable = false;
                    if (label != null) label.text = "초대함";
                });
            }
            return row;
        }

        private static string StateLabel(EPersonaState s) => s switch
        {
            EPersonaState.k_EPersonaStateOnline => "온라인",
            EPersonaState.k_EPersonaStateAway => "자리 비움",
            EPersonaState.k_EPersonaStateSnooze => "자리 비움",
            EPersonaState.k_EPersonaStateBusy => "다른 용무 중",
            EPersonaState.k_EPersonaStateLookingToPlay => "게임 찾는 중",
            EPersonaState.k_EPersonaStateLookingToTrade => "온라인",
            EPersonaState.k_EPersonaStateInvisible => "오프라인",
            _ => "오프라인",
        };

        private void SetEmpty(string msg)
        {
            if (headerCountText != null) headerCountText.text = "";
            if (emptyText == null) return;
            emptyText.gameObject.SetActive(true);
            emptyText.text = msg;
        }

        private static T Find<T>(GameObject root, string child) where T : Component
        {
            var t = root.transform.Find(child);
            return t != null ? t.GetComponent<T>() : null;
        }

        // ---- 프로필 사진 ----

        private void SetAvatar(CSteamID id, RawImage target)
        {
            int handle = SteamFriends.GetSmallFriendAvatar(id);
            if (handle == -1) { _pendingAvatars[id.m_SteamID] = target; return; }   // 로딩 중 → 콜백에서
            ApplyAvatar(target, LoadAvatar(handle));
        }

        private void ApplyAvatar(RawImage target, Texture2D tex)
        {
            if (target == null || tex == null) return;
            target.texture = tex;
            target.color = _avatarTint.TryGetValue(target, out var tint) ? tint : Color.white;
        }

        private void OnAvatarLoaded(AvatarImageLoaded_t cb)
        {
            if (!_pendingAvatars.TryGetValue(cb.m_steamID.m_SteamID, out var img)) return;
            _pendingAvatars.Remove(cb.m_steamID.m_SteamID);
            if (img == null) return;
            ApplyAvatar(img, LoadAvatar(cb.m_iImage));
        }

        private static Texture2D LoadAvatar(int handle)
        {
            if (handle <= 0) return null;
            if (AvatarCache.TryGetValue(handle, out var cached) && cached != null) return cached;
            if (!SteamUtils.GetImageSize(handle, out uint w, out uint h) || w == 0 || h == 0) return null;

            var data = new byte[w * h * 4];
            if (!SteamUtils.GetImageRGBA(handle, data, data.Length)) return null;

            // Steam은 위→아래, Unity 텍스처는 아래→위 → 세로 뒤집기
            var flipped = new byte[data.Length];
            int stride = (int)w * 4;
            for (int y = 0; y < h; y++)
                System.Buffer.BlockCopy(data, y * stride, flipped, ((int)h - 1 - y) * stride, stride);

            var tex = new Texture2D((int)w, (int)h, TextureFormat.RGBA32, false);
            tex.LoadRawTextureData(flipped);
            tex.Apply();
            AvatarCache[handle] = tex;
            return tex;
        }

        private void Update()
        {
            if (!_isShowing || Time.frameCount == _shownFrame) return;
#if ENABLE_INPUT_SYSTEM
            bool esc = UnityEngine.InputSystem.Keyboard.current != null &&
                       UnityEngine.InputSystem.Keyboard.current.escapeKey.wasPressedThisFrame;
#else
            bool esc = Input.GetKeyDown(KeyCode.Escape);
#endif
            if (esc) Hide();
        }

        private void OnDestroy()
        {
            _cbAvatar?.Dispose();
            _cbAvatar = null;
        }
    }
}

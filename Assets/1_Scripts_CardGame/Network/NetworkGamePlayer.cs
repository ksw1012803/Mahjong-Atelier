using System;
using System.Collections.Generic;
using Mirror;
using UnityEngine;
using CardGame.Core;

namespace CardGame.Network
{
    /// <summary>
    /// 접속한 사람 1명당 1개 (Mirror playerPrefab). 로비 → 게임 씬까지 살아남음 (DontDestroyOnLoad).
    /// 좌석·닉네임·SteamID·준비 상태를 SyncVar로 공유.
    ///
    /// 프리팹 구성: 빈 GameObject + NetworkIdentity + NetworkGamePlayer. 시각 요소 없음.
    /// </summary>
    public class NetworkGamePlayer : NetworkBehaviour
    {
        /// <summary>현재 클라이언트가 알고 있는 모든 플레이어 (좌석 순 정렬).</summary>
        public static readonly List<NetworkGamePlayer> All = new List<NetworkGamePlayer>();
        public static NetworkGamePlayer Local { get; private set; }
        public static event Action OnPlayersChanged;

        [SyncVar(hook = nameof(OnSeatChangedHook))] public int seatIndex = -1;
        [SyncVar(hook = nameof(OnNameChangedHook))] public string displayName = "";
        [SyncVar] public ulong steamId;
        [SyncVar(hook = nameof(OnCharacterChangedHook))] public int characterIndex;
        [SyncVar(hook = nameof(OnReadyChangedHook))] public bool isReady;
        [SyncVar] public bool isHostPlayer;

        public bool IsLocal => isLocalPlayer;

        // ---------------- lifecycle ----------------

        public override void OnStartServer()
        {
            DontDestroyOnLoad(gameObject);
        }

        public override void OnStartClient()
        {
            DontDestroyOnLoad(gameObject);
            if (!All.Contains(this)) All.Add(this);
            SortAll();
            OnPlayersChanged?.Invoke();
        }

        public override void OnStopClient()
        {
            if (isLocalPlayer) PlayerProfile.OnCharacterChanged -= HandleLocalCharacterChanged;
            All.Remove(this);
            if (Local == this) Local = null;
            OnPlayersChanged?.Invoke();
        }

        public override void OnStartLocalPlayer()
        {
            Local = this;

            ulong id = 0UL;
            string name = "Player";
            var lobby = SteamLobbyManager.Instance;
            if (lobby != null && lobby.IsSteamReady)
            {
                id = lobby.LocalSteamId;
                name = lobby.LocalPersonaName;
            }
            if (string.IsNullOrWhiteSpace(name)) name = PlayerProfile.Nickname;
            CmdSetIdentity(id, name, PlayerProfile.CharacterIndex);
            PlayerProfile.OnCharacterChanged += HandleLocalCharacterChanged;
        }

        private void HandleLocalCharacterChanged(int index)
        {
            if (isLocalPlayer && NetworkClient.isConnected) CmdSetCharacter(index);
        }

        // ---------------- commands (client → server) ----------------

        [Command]
        private void CmdSetIdentity(ulong id, string name, int character)
        {
            steamId = id;
            displayName = string.IsNullOrWhiteSpace(name) ? $"Player {seatIndex + 1}" : name.Trim();
            characterIndex = Mathf.Max(0, character);
        }

        [Command]
        private void CmdSetCharacter(int character)
        {
            characterIndex = Mathf.Max(0, character);
        }

        [Command]
        public void CmdSetReady(bool ready)
        {
            isReady = ready;
        }

        // ---------------- server helpers ----------------

        [Server]
        public void ServerAssignSeat(int seat, bool host)
        {
            seatIndex = seat;
            isHostPlayer = host;
            if (host) isReady = true;  // 호스트는 항상 준비 상태
        }

        // ---------------- hooks ----------------

        private void OnSeatChangedHook(int _, int __) { SortAll(); OnPlayersChanged?.Invoke(); }
        private void OnNameChangedHook(string _, string __) => OnPlayersChanged?.Invoke();
        private void OnReadyChangedHook(bool _, bool __) => OnPlayersChanged?.Invoke();
        private void OnCharacterChangedHook(int _, int __) => OnPlayersChanged?.Invoke();

        private static void SortAll()
        {
            All.RemoveAll(p => p == null);
            All.Sort((a, b) => a.seatIndex.CompareTo(b.seatIndex));
        }

        public static NetworkGamePlayer FindBySeat(int seat)
        {
            foreach (var p in All) if (p != null && p.seatIndex == seat) return p;
            return null;
        }

        public static bool AllReady()
        {
            if (All.Count == 0) return false;
            foreach (var p in All) if (p != null && !p.isReady) return false;
            return true;
        }

        public override string ToString() => $"[{seatIndex}] {displayName} ({steamId}) ready={isReady} host={isHostPlayer}";
    }
}

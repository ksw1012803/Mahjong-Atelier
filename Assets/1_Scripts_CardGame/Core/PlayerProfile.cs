using System;
using UnityEngine;
using CardGame.Network;

namespace CardGame.Core
{
    /// <summary>
    /// 로컬 플레이어 프로필 — 선택한 캐릭터와 닉네임. PlayerPrefs에 저장되어 다음 실행에도 유지.
    ///   - 닉네임: Steam이 켜져 있으면 Steam 닉네임, 아니면 저장된 닉네임(기본 "플레이어")
    ///   - 캐릭터: CharacterDatabase의 index
    /// </summary>
    public static class PlayerProfile
    {
        private const string KEY_CHARACTER = "SM_Character";
        private const string KEY_NICKNAME = "SM_Nickname";
        public const string DefaultNickname = "플레이어";

        /// <summary>캐릭터가 바뀌면 (새 index). 로비 캐릭터·프로필 이미지가 구독.</summary>
        public static event Action<int> OnCharacterChanged;

        public static int CharacterIndex
        {
            get
            {
                var db = CharacterDatabase.Instance;
                int v = PlayerPrefs.GetInt(KEY_CHARACTER, 0);
                return db != null ? db.ClampIndex(v) : Mathf.Max(0, v);
            }
        }

        public static CharacterDefinition Character
        {
            get
            {
                var db = CharacterDatabase.Instance;
                return db != null ? db.Get(CharacterIndex) : null;
            }
        }

        public static void SetCharacter(int index)
        {
            var db = CharacterDatabase.Instance;
            if (db != null) index = db.ClampIndex(index);
            if (index == CharacterIndex && PlayerPrefs.HasKey(KEY_CHARACTER)) return;

            PlayerPrefs.SetInt(KEY_CHARACTER, index);
            PlayerPrefs.Save();
            OnCharacterChanged?.Invoke(index);
        }

        public static string Nickname
        {
            get
            {
                var lobby = SteamLobbyManager.Instance;
                if (lobby != null && lobby.IsSteamReady && !string.IsNullOrWhiteSpace(lobby.LocalPersonaName))
                    return lobby.LocalPersonaName.Trim();

                string saved = PlayerPrefs.GetString(KEY_NICKNAME, "");
                return string.IsNullOrWhiteSpace(saved) ? DefaultNickname : saved.Trim();
            }
        }

        /// <summary>Steam이 없을 때 쓸 닉네임 저장.</summary>
        public static void SetNickname(string name)
        {
            PlayerPrefs.SetString(KEY_NICKNAME, name ?? "");
            PlayerPrefs.Save();
        }

        /// <summary>봇 이름: BOT1, BOT2 ... (봇끼리 1부터 순서대로).</summary>
        public static string BotName(int botNumber) => $"BOT{botNumber}";
    }
}

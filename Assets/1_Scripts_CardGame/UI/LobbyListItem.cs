using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Steamworks;
using CardGame.Network;

namespace CardGame.UI
{
    /// <summary>
    /// 공개 로비 목록의 한 줄. 프리팹: Button + 자식 TMP_Text 2개 (방 이름 / 인원).
    /// </summary>
    public class LobbyListItem : MonoBehaviour
    {
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text countText;
        [SerializeField] private Button button;

        private CSteamID _id;
        private Action<CSteamID> _onClick;

        public void Bind(SteamLobbyManager.LobbyInfo info, Action<CSteamID> onClick)
        {
            _id = info.id;
            _onClick = onClick;

            if (nameText != null) nameText.text = string.IsNullOrEmpty(info.name) ? "이름 없는 방" : info.name;
            if (countText != null) countText.text = $"{info.members} / {info.maxMembers}";

            if (button == null) button = GetComponent<Button>();
            if (button != null)
            {
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() => _onClick?.Invoke(_id));
                button.interactable = info.members < info.maxMembers;
            }
        }
    }
}

using TMPro;
using UnityEngine;
using UnityEngine.UI;
using CardGame.Core;

namespace CardGame.UI
{
    /// <summary>
    /// 좌상단 플레이어 정보 + 우상단 설정 (Lobby/ModeSelect/Game 공통).
    /// 각 씬에 Prefab으로 배치하거나 DontDestroyOnLoad로 1개 유지.
    /// </summary>
    public class CommonHUD : MonoBehaviour
    {
        [Header("Player Info (좌상단)")]
        [SerializeField] private Image avatarImage;
        [SerializeField] private TMP_Text playerNameText;
        [SerializeField] private TMP_Text playerIdText;
        [SerializeField] private TMP_Text statusText;

        [Header("Settings (우상단)")]
        [SerializeField] private Button settingsButton;
        [SerializeField] private GameObject settingsDialog;

        [Header("Default Player Data")]
        [SerializeField] private string defaultName = "플레이어";
        [SerializeField] private string defaultId = "ID: 00001";
        [SerializeField] private string defaultStatus = "오늘 3전";
        [SerializeField] private Sprite defaultAvatar;

        private void Awake()
        {
            if (settingsButton != null) settingsButton.onClick.AddListener(ToggleSettings);
            if (settingsDialog != null) settingsDialog.SetActive(false);
            UpdateDisplay();
        }

        private void OnDestroy()
        {
            if (settingsButton != null) settingsButton.onClick.RemoveListener(ToggleSettings);
        }

        public void SetPlayerInfo(string name, string id, string status, Sprite avatar = null)
        {
            if (playerNameText != null) playerNameText.text = name;
            if (playerIdText != null) playerIdText.text = id;
            if (statusText != null) statusText.text = status;
            if (avatar != null && avatarImage != null) avatarImage.sprite = avatar;
        }

        private void UpdateDisplay()
        {
            SetPlayerInfo(defaultName, defaultId, defaultStatus, defaultAvatar);
        }

        private void ToggleSettings()
        {
            if (settingsDialog == null) return;
            settingsDialog.SetActive(!settingsDialog.activeSelf);
        }
    }
}

using UnityEngine;
using UnityEngine.UI;
using CardGame.Core;

namespace CardGame.UI
{
    /// <summary>
    /// Lobby 씬 v2 — 싱글 / 온라인 분기.
    ///   싱글플레이 버튼 → ModeSelectDialog.Show()  (기존 흐름 그대로)
    ///   온라인플레이 버튼 → OnlineLobbyPanel.Show()
    ///   뒤로 → TitleScene
    ///
    /// 기존 LobbySceneController를 쓰고 있다면 이 파일 대신
    /// 온라인 버튼 OnClick에 OnlineLobbyPanel.Show() 만 연결해도 됨.
    /// </summary>
    public class LobbySceneController : MonoBehaviour
    {
        [Header("Buttons")]
        [SerializeField] private Button singlePlayButton;
        [SerializeField] private Button onlinePlayButton;
        [SerializeField] private Button backButton;

        [Header("Panels")]
        [SerializeField] private ModeSelectDialog modeSelectDialog;
        [SerializeField] private OnlineLobbyPanel onlineLobbyPanel;

        private void Start()
        {
            if (singlePlayButton != null) singlePlayButton.onClick.AddListener(OnSinglePlayClicked);
            if (onlinePlayButton != null) onlinePlayButton.onClick.AddListener(OnOnlinePlayClicked);
            if (backButton != null) backButton.onClick.AddListener(OnBackClicked);

            // 로비 진입 시 온라인 플래그 초기화 (온라인 게임 끝나고 돌아온 경우 대비)
            SceneContext.Set(SceneContextKeys.OnlineSession, false);
        }

        private void OnSinglePlayClicked()
        {
            if (onlineLobbyPanel != null && onlineLobbyPanel.IsShowing) onlineLobbyPanel.Hide();
            modeSelectDialog?.Show();
        }

        private void OnOnlinePlayClicked()
        {
            if (modeSelectDialog != null && modeSelectDialog.IsShowing) modeSelectDialog.Hide();
            onlineLobbyPanel?.Show();
        }

        private void OnBackClicked()
        {
            if (onlineLobbyPanel != null && onlineLobbyPanel.IsShowing) onlineLobbyPanel.Hide();
            SceneLoader.Instance?.Load(GameScene.TitleScene, useLoadingScreen: false);
        }
    }
}

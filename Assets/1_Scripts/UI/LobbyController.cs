using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using MahjongAtelier.Core;

namespace MahjongAtelier.UI
{
    /// <summary>
    /// 로비 씬 컨트롤러.
    /// 
    /// 현재 메뉴:
    ///   - 등급전 (랭킹 마작) → 게임 씬으로
    ///   - (추후) 친선전, 설정, 통계 등
    ///   - 종료
    /// 
    /// 동작:
    ///   1) 페이드인으로 진입 (FadeController)
    ///   2) 버튼 클릭 시 페이드아웃 → 해당 씬으로
    /// </summary>
    public class LobbyController : MonoBehaviour
    {
        [Header("Buttons")]
        [Tooltip("등급전 버튼")]
        [SerializeField] private Button rankedMatchButton;

        [Tooltip("종료 버튼 (선택)")]
        [SerializeField] private Button quitButton;

        [Header("Optional Info")]
        [SerializeField] private TMP_Text playerNameText;
        [SerializeField] private TMP_Text rankText;

        [Header("Fade")]
        [SerializeField] private FadeController fadeController;

        [Header("Scene Targets")]
        [Tooltip("등급전 시작 시 이동할 씬 (보통 Loading 또는 Game)")]
        [SerializeField] private SceneReference rankedMatchScene;
        [SerializeField] private string rankedMatchSceneFallback = "Loading";

        private bool _isTransitioning = false;

        private void Awake()
        {
            if (rankedMatchButton != null)
                rankedMatchButton.onClick.AddListener(HandleRankedMatchClicked);
            if (quitButton != null)
                quitButton.onClick.AddListener(HandleQuitClicked);
        }

        private void OnDestroy()
        {
            if (rankedMatchButton != null)
                rankedMatchButton.onClick.RemoveListener(HandleRankedMatchClicked);
            if (quitButton != null)
                quitButton.onClick.RemoveListener(HandleQuitClicked);
        }

        private void Start()
        {
            // 추후 플레이어 정보 로드해서 표시
            if (playerNameText != null && string.IsNullOrEmpty(playerNameText.text))
                playerNameText.text = "Player";
            if (rankText != null && string.IsNullOrEmpty(rankText.text))
                rankText.text = "-";
        }

        private void HandleRankedMatchClicked()
        {
            if (_isTransitioning) return;
            _isTransitioning = true;

            Debug.Log("[Lobby] 등급전 시작");
            DisableButtons();

            string targetScene = (rankedMatchScene != null && rankedMatchScene.IsValid)
                ? rankedMatchScene.SceneName
                : rankedMatchSceneFallback;

            if (fadeController != null)
            {
                fadeController.FadeOut(onComplete: () =>
                {
                    Debug.Log($"[Lobby] → {targetScene}");
                    SceneManager.LoadScene(targetScene);
                });
            }
            else
            {
                SceneManager.LoadScene(targetScene);
            }
        }

        private void HandleQuitClicked()
        {
            if (_isTransitioning) return;
            Debug.Log("[Lobby] 종료 버튼 클릭");

#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private void DisableButtons()
        {
            if (rankedMatchButton != null) rankedMatchButton.interactable = false;
            if (quitButton != null) quitButton.interactable = false;
        }
    }
}

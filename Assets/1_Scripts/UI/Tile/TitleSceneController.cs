using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using MahjongAtelier.Core;

namespace MahjongAtelier.UI
{
    /// <summary>
    /// 타이틀 씬 컨트롤러.
    /// 
    /// 동작:
    ///   1) Scene 진입 시 페이드인 (FadeController가 처리)
    ///   2) 시작 버튼 클릭 → 페이드아웃 → 다음 씬 (로비)으로 이동
    ///   3) 페이드 중에는 버튼 입력 차단
    /// 
    /// 씬 이름은 Inspector에서 직접 지정 (SceneReference).
    /// </summary>
    public class TitleSceneController : MonoBehaviour
    {
        [Header("Buttons")]
        [SerializeField] private Button startButton;
        [SerializeField] private Button quitButton;

        [Header("Optional Info")]
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text versionText;
        [SerializeField] private TMP_Text pressAnyText;

        [Header("Fade")]
        [SerializeField] private FadeController fadeController;

        [Header("Next Scene")]
        [Tooltip("시작 버튼 클릭 시 이동할 씬 (Project에서 .unity 파일 드래그)")]
        [SerializeField] private SceneReference nextScene;

        [Tooltip("nextScene 비어있을 때 대체 씬 이름")]
        [SerializeField] private string fallbackSceneName = "Lobby";

        private bool _isTransitioning = false;

        private void Awake()
        {
            if (startButton != null) startButton.onClick.AddListener(HandleStartClicked);
            if (quitButton != null) quitButton.onClick.AddListener(HandleQuitClicked);
        }

        private void OnDestroy()
        {
            if (startButton != null) startButton.onClick.RemoveListener(HandleStartClicked);
            if (quitButton != null) quitButton.onClick.RemoveListener(HandleQuitClicked);
        }

        private void Start()
        {
            if (titleText != null && string.IsNullOrEmpty(titleText.text))
                titleText.text = "Mahjong Atelier";
            if (versionText != null && string.IsNullOrEmpty(versionText.text))
                versionText.text = $"v{Application.version}";
        }

        private void HandleStartClicked()
        {
            if (_isTransitioning) return;
            _isTransitioning = true;

            Debug.Log("[Title] 시작 버튼 클릭");

            // 버튼들 비활성화 (중복 클릭 방지)
            if (startButton != null) startButton.interactable = false;
            if (quitButton != null) quitButton.interactable = false;

            // 페이드아웃 후 씬 전환
            if (fadeController != null)
            {
                fadeController.FadeOut(onComplete: TransitionToNextScene);
            }
            else
            {
                TransitionToNextScene();
            }
        }

        private void TransitionToNextScene()
        {
            string sceneName = nextScene != null && nextScene.IsValid
                ? nextScene.SceneName
                : fallbackSceneName;

            Debug.Log($"[Title] → {sceneName}");
            SceneManager.LoadScene(sceneName);
        }

        private void HandleQuitClicked()
        {
            if (_isTransitioning) return;
            Debug.Log("[Title] 종료 버튼 클릭");

#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}

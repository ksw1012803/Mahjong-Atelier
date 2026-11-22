using UnityEngine;
using UnityEngine.UI;
using TMPro;
using MahjongAtelier.Core;

namespace MahjongAtelier.UI
{
    /// <summary>
    /// 메인 로비 컨트롤러.
    /// 
    /// 현재 기능:
    ///   - 게임 시작 버튼 → Loading Scene으로 이동
    ///   - 종료 버튼 (선택, 빌드에서만 의미 있음)
    /// 
    /// 추후 추가 가능:
    ///   - 설정 메뉴
    ///   - 통계/기록
    ///   - 캐릭터 선택
    ///   - 모드 선택 (동풍전/반장전)
    /// </summary>
    public class MainLobbyController : MonoBehaviour
    {
        [Header("Buttons")]
        [SerializeField] private Button startGameButton;
        [SerializeField] private Button quitButton;

        [Header("Optional Info")]
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text versionText;

        private void Awake()
        {
            if (startGameButton != null)
                startGameButton.onClick.AddListener(HandleStartGameClicked);
            if (quitButton != null)
                quitButton.onClick.AddListener(HandleQuitClicked);
        }

        private void OnDestroy()
        {
            if (startGameButton != null)
                startGameButton.onClick.RemoveListener(HandleStartGameClicked);
            if (quitButton != null)
                quitButton.onClick.RemoveListener(HandleQuitClicked);
        }

        private void Start()
        {
            if (titleText != null && string.IsNullOrEmpty(titleText.text))
                titleText.text = "Mahjong Atelier";
            if (versionText != null && string.IsNullOrEmpty(versionText.text))
                versionText.text = $"v{Application.version}";
        }

        private void HandleStartGameClicked()
        {
            Debug.Log("[MainLobby] 게임 시작 버튼 클릭");
            SceneFlow.GoToLoading();
        }

        private void HandleQuitClicked()
        {
            Debug.Log("[MainLobby] 종료 버튼 클릭");

#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}

using UnityEngine;
using UnityEngine.SceneManagement;

namespace MahjongAtelier.Core
{
    /// <summary>
    /// Scene 전환 헬퍼.
    /// 
    /// 사용:
    ///   SceneFlow.GoToMainLobby();
    ///   SceneFlow.GoToLoading();
    ///   SceneFlow.GoToGame();
    /// 
    /// Scene 이름은 한 곳에서 관리. Build Settings에 모두 추가되어야 함.
    /// </summary>
    public static class SceneFlow
    {
        // Scene 이름 — Build Settings의 Scene 이름과 정확히 일치해야 함
        public const string MainLobbyScene = "MainLobby";
        public const string LoadingScene = "Loading";
        public const string GameScene = "GameScene";

        public static void GoToMainLobby()
        {
            Debug.Log("[SceneFlow] → MainLobby");
            SceneManager.LoadScene(MainLobbyScene);
        }

        public static void GoToLoading()
        {
            Debug.Log("[SceneFlow] → Loading");
            SceneManager.LoadScene(LoadingScene);
        }

        public static void GoToGame()
        {
            Debug.Log("[SceneFlow] → GameScene");
            SceneManager.LoadScene(GameScene);
        }
    }
}

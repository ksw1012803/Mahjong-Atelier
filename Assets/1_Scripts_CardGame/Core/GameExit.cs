using UnityEngine;
using UnityEngine.SceneManagement;
using CardGame.Network;

namespace CardGame.Core
{
    /// <summary>
    /// 게임에서 나가기 / 게임 종료.
    ///   - LeaveToLobby: 온라인이면 세션을 나감(내 자리는 봇이 이어받음) → 로비, 싱글이면 바로 로비
    ///   - QuitGame: 프로그램 종료 (에디터에서는 플레이 모드 종료)
    /// </summary>
    public static class GameExit
    {
        /// <summary>지금 게임(카드 게임 씬) 안인지.</summary>
        public static bool IsInGame => Object.FindAnyObjectByType<CardGameManager>() != null;

        /// <summary>온라인 세션 중인지.</summary>
        public static bool IsOnline
        {
            get
            {
                var nm = NetworkManagerOrNull();
                return nm != null && nm.InSession;
            }
        }

        public static void LeaveToLobby()
        {
            var nm = NetworkManagerOrNull();
            if (nm != null && nm.InSession)
            {
                nm.LeaveSession();   // 세션 정리 + 로비 씬 전환 (페이드 포함)
                return;
            }

            if (SceneLoader.Instance != null && !SceneLoader.Instance.IsLoading)
                SceneLoader.Instance.Load(GameScene.LobbyScene);
            else
                SceneManager.LoadScene(GameScene.LobbyScene.ToString());
        }

        public static void QuitGame()
        {
            var nm = NetworkManagerOrNull();
            if (nm != null && nm.InSession) nm.LeaveSession();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private static CardGameNetworkManager NetworkManagerOrNull()
        {
            try { return Mirror.NetworkManager.singleton as CardGameNetworkManager; }
            catch { return null; }
        }
    }
}

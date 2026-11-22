using UnityEngine;
using CardGame.Core;
using CardGame.UI;

namespace CardGame
{
    /// <summary>
    /// Bootstrap 씬 진입점 v2 — Scene 내 GameObject 참조 방식.
    /// 
    /// Prefab 없이 Bootstrap 씬에 다음 3개 GameObject 배치:
    ///   1. FadeController (CardGame.UI.FadeController 컴포넌트 포함)
    ///   2. SceneLoader (CardGame.Core.SceneLoader 컴포넌트 포함)
    ///   3. GameBootstrap (이 컴포넌트 포함)
    /// 
    /// Inspector에서 Fade Controller / Scene Loader 필드에 Scene 내 GameObject 드래그.
    /// </summary>
    public class GameBootstrap : MonoBehaviour
    {
        [Header("Scene References")]
        [Tooltip("Bootstrap 씬에 배치된 FadeController GameObject")]
        [SerializeField] private FadeController fadeController;

        [Tooltip("Bootstrap 씬에 배치된 SceneLoader GameObject")]
        [SerializeField] private SceneLoader sceneLoader;

        [Header("Start")]
        [SerializeField] private GameScene firstScene = GameScene.TitleScene;
        [SerializeField] private bool startBlack = true;

        private void Awake()
        {
            // 각 컴포넌트가 DontDestroyOnLoad 처리 (각 컴포넌트 Awake에서 자동)
            // 초기 상태 설정
            if (startBlack && fadeController != null)
                fadeController.SetBlackImmediate();
        }

        private void Start()
        {
            if (sceneLoader == null)
            {
                sceneLoader = SceneLoader.Instance;
            }

            if (sceneLoader != null)
            {
                sceneLoader.Load(firstScene);
            }
            else
            {
                Debug.LogError("[GameBootstrap] SceneLoader 참조 없음!");
            }
        }
    }
}
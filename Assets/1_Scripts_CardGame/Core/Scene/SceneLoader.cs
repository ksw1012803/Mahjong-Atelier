using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using CardGame.UI;

namespace CardGame.Core
{
    /// <summary>
    /// 씬 로딩 매니저 v4 — LoadingSceneController 완료 신호 대기.
    ///
    /// v4 변경:
    ///   - useLoadingScreen 흐름에서 minLoadingScreenTime AND LoadingSceneController.IsLoadComplete
    ///     둘 다 만족할 때까지 대기 (DB 로드가 min보다 길면 DB 완료까지 유지).
    ///   - DB 로드 완료 + progress 100%까지 전환 안 함.
    ///   - minLoadingScreenTime 기본 4초로 상향.
    /// </summary>
    public class SceneLoader : MonoBehaviour
    {
        public static SceneLoader Instance { get; private set; }

        [Header("Fade")]
        [SerializeField] private float fadeOutDuration = 0.3f;
        [SerializeField] private float fadeInDuration = 0.3f;
        [Tooltip("LoadScene 최소 체류 시간 (초). 이보다 DB 로드가 길면 완료까지 대기.")]
        [SerializeField] private float minLoadingScreenTime = 4f;
        [Tooltip("LoadingSceneController의 완료 신호(IsLoadComplete)도 기다릴지 여부.")]
        [SerializeField] private bool waitForLoadingComplete = true;
        [Tooltip("LoadingSceneController 완료 대기의 최대 상한 (초). 완료 신호가 안 와도 이 시간이면 강제 전환.")]
        [SerializeField] private float maxWaitForComplete = 60f;

        public GameScene CurrentScene { get; private set; }
        public bool IsLoading { get; private set; }

        public event Action<GameScene> OnSceneLoadStarted;
        public event Action<GameScene> OnSceneLoaded;
        public event Action<float> OnLoadProgress;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            if (!gameObject.activeSelf)
                gameObject.SetActive(true);

            Transform root = transform;
            while (root.parent != null) root = root.parent;
            if (!root.gameObject.activeSelf) root.gameObject.SetActive(true);
            DontDestroyOnLoad(root.gameObject);

            if (System.Enum.TryParse<GameScene>(SceneManager.GetActiveScene().name, out var current))
                CurrentScene = current;
        }

        public void Load(GameScene scene, bool useLoadingScreen = false)
        {
            if (IsLoading)
            {
                Debug.LogWarning($"[SceneLoader] 이미 로딩 중, 요청 무시: {scene}");
                return;
            }

            if (!gameObject.activeInHierarchy)
            {
                Debug.LogWarning("[SceneLoader] GameObject 비활성, 자동 활성화");
                gameObject.SetActive(true);
            }

            StartCoroutine(LoadRoutine(scene, useLoadingScreen));
        }

        private IEnumerator LoadRoutine(GameScene target, bool useLoadingScreen)
        {
            IsLoading = true;
            SceneContext.PreviousScene = CurrentScene;
            OnSceneLoadStarted?.Invoke(target);

            yield return FadeController.Instance?.FadeOutRoutine(fadeOutDuration);

            if (useLoadingScreen && target != GameScene.LoadScene)
            {
                // LoadScene 진입
                yield return LoadSceneRaw(GameScene.LoadScene);

                // LoadingSceneController가 Start()에서 IsLoadComplete = false로 초기화할 시간
                yield return null;

                yield return FadeController.Instance?.FadeInRoutine(fadeInDuration);

                // ★ v4: 최소 시간 AND LoadingSceneController 완료 둘 다 만족할 때까지 대기
                float waitTimer = 0f;
                while (true)
                {
                    waitTimer += Time.unscaledDeltaTime;

                    bool minTimePassed = waitTimer >= minLoadingScreenTime;
                    bool loadComplete = !waitForLoadingComplete || LoadingSceneController.IsLoadComplete;
                    bool maxTimeout = waitTimer >= maxWaitForComplete;

                    if ((minTimePassed && loadComplete) || maxTimeout)
                    {
                        if (maxTimeout && !loadComplete)
                            Debug.LogWarning($"[SceneLoader] LoadingSceneController 완료 신호 timeout ({maxWaitForComplete}s), 강제 전환");
                        break;
                    }

                    yield return null;
                }

                yield return FadeController.Instance?.FadeOutRoutine(fadeOutDuration);
            }

            yield return LoadSceneRaw(target);
            CurrentScene = target;

            yield return FadeController.Instance?.FadeInRoutine(fadeInDuration);

            IsLoading = false;
            OnSceneLoaded?.Invoke(target);
        }

        private IEnumerator LoadSceneRaw(GameScene scene)
        {
            var op = SceneManager.LoadSceneAsync(scene.ToString(), LoadSceneMode.Single);
            if (op == null)
            {
                Debug.LogError($"[SceneLoader] 로드 실패: {scene} (Build Settings 확인!)");
                yield break;
            }

            op.allowSceneActivation = false;

            while (op.progress < 0.9f)
            {
                OnLoadProgress?.Invoke(op.progress / 0.9f);
                yield return null;
            }

            OnLoadProgress?.Invoke(1f);
            op.allowSceneActivation = true;

            while (!op.isDone) yield return null;
        }

        public void Reload() => Load(CurrentScene);
        public void Back() => Load(SceneContext.PreviousScene);
    }
}
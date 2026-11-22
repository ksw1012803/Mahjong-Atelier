using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using MahjongAtelier.Core;

namespace MahjongAtelier.UI
{
    /// <summary>
    /// 로딩 씬 컨트롤러 v2 — 씬 이름 Inspector 지정.
    /// 
    /// 동작:
    ///   1) Scene 진입 즉시 페이드인 + 로딩 시작
    ///   2) 진행 바 채우기
    ///   3) 완료 후 페이드아웃하면서 다음 씬으로
    /// </summary>
    public class LoadingController : MonoBehaviour
    {
        [Header("Loading Display")]
        [SerializeField] private Slider progressBar;
        [SerializeField] private TMP_Text progressText;
        [SerializeField] private TMP_Text statusText;

        [Header("Fade")]
        [SerializeField] private FadeController fadeController;

        [Header("Timing")]
        [SerializeField, Range(0.5f, 5f)]
        private float loadingDuration = 1.5f;

        [SerializeField, Range(0f, 1f)]
        private float postLoadDelay = 0.2f;

        [Header("Messages")]
        [SerializeField]
        private string[] statusMessages = new[]
        {
            "패산을 섞는 중...",
            "캐릭터 준비 중...",
            "게임 준비 완료"
        };

        [Header("Next Scene")]
        [Tooltip("로딩 완료 후 이동할 씬")]
        [SerializeField] private SceneReference nextScene;

        [SerializeField] private string fallbackSceneName = "GameScene";

        private void Start()
        {
            if (progressBar != null) progressBar.value = 0f;
            if (progressText != null) progressText.text = "0%";

            StartCoroutine(LoadingCoroutine());
        }

        private IEnumerator LoadingCoroutine()
        {
            float elapsed = 0f;

            while (elapsed < loadingDuration)
            {
                elapsed += Time.deltaTime;
                float progress = Mathf.Clamp01(elapsed / loadingDuration);

                if (progressBar != null) progressBar.value = progress;
                if (progressText != null) progressText.text = $"{Mathf.RoundToInt(progress * 100)}%";

                if (statusText != null && statusMessages != null && statusMessages.Length > 0)
                {
                    int msgIdx = Mathf.Clamp(
                        Mathf.FloorToInt(progress * statusMessages.Length),
                        0, statusMessages.Length - 1);
                    statusText.text = statusMessages[msgIdx];
                }

                yield return null;
            }

            // 완료 상태
            if (progressBar != null) progressBar.value = 1f;
            if (progressText != null) progressText.text = "100%";
            if (statusText != null && statusMessages.Length > 0)
                statusText.text = statusMessages[statusMessages.Length - 1];

            if (postLoadDelay > 0f)
                yield return new WaitForSeconds(postLoadDelay);

            // 페이드아웃 후 다음 씬
            string targetScene = (nextScene != null && nextScene.IsValid)
                ? nextScene.SceneName
                : fallbackSceneName;

            if (fadeController != null)
            {
                fadeController.FadeOut(onComplete: () =>
                {
                    Debug.Log($"[Loading] → {targetScene}");
                    SceneManager.LoadScene(targetScene);
                });
            }
            else
            {
                SceneManager.LoadScene(targetScene);
            }
        }
    }
}

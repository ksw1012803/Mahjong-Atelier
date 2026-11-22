using System.Collections;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace CardGame.UI
{
    /// <summary>
    /// 전역 페이드 컨트롤러 (DontDestroyOnLoad).
    /// Bootstrap에서 1회 Instantiate → 모든 씬에서 공유.
    /// 
    /// 사용:
    ///   yield return FadeController.Instance.FadeOutRoutine(0.3f);
    ///   await FadeController.Instance.FadeOutAsync(0.3f);
    /// </summary>
    [RequireComponent(typeof(Canvas))]
    public class FadeController : MonoBehaviour
    {
        public static FadeController Instance { get; private set; }

        [SerializeField] private CanvasGroup fadeCanvasGroup;
        [SerializeField] private Image fadeImage;
        [SerializeField] private Color fadeColor = Color.black;
        [SerializeField] private int sortingOrder = 9999;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            var canvas = GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;

            if (fadeImage != null) fadeImage.color = fadeColor;
            if (fadeCanvasGroup != null)
            {
                fadeCanvasGroup.alpha = 0f;
                fadeCanvasGroup.blocksRaycasts = false;
                fadeCanvasGroup.interactable = false;
            }
        }

        /// <summary>즉시 알파 1 설정 (게임 시작 시 암전 상태로).</summary>
        public void SetBlackImmediate()
        {
            if (fadeCanvasGroup != null)
            {
                fadeCanvasGroup.alpha = 1f;
                fadeCanvasGroup.blocksRaycasts = true;
            }
        }

        public IEnumerator FadeOutRoutine(float duration)
        {
            if (fadeCanvasGroup == null) yield break;
            fadeCanvasGroup.blocksRaycasts = true;
            fadeCanvasGroup.DOKill();
            yield return fadeCanvasGroup
                .DOFade(1f, duration)
                .SetUpdate(true)
                .WaitForCompletion();
        }

        public IEnumerator FadeInRoutine(float duration)
        {
            if (fadeCanvasGroup == null) yield break;
            fadeCanvasGroup.DOKill();
            yield return fadeCanvasGroup
                .DOFade(0f, duration)
                .SetUpdate(true)
                .WaitForCompletion();
            fadeCanvasGroup.blocksRaycasts = false;
        }

        // async/await 버전
        public async System.Threading.Tasks.Task FadeOutAsync(float duration)
        {
            if (fadeCanvasGroup == null) return;
            fadeCanvasGroup.blocksRaycasts = true;
            fadeCanvasGroup.DOKill();
            await fadeCanvasGroup.DOFade(1f, duration).SetUpdate(true).AsyncWaitForCompletion();
        }

        public async System.Threading.Tasks.Task FadeInAsync(float duration)
        {
            if (fadeCanvasGroup == null) return;
            fadeCanvasGroup.DOKill();
            await fadeCanvasGroup.DOFade(0f, duration).SetUpdate(true).AsyncWaitForCompletion();
            fadeCanvasGroup.blocksRaycasts = false;
        }

        private void OnDestroy()
        {
            if (fadeCanvasGroup != null) fadeCanvasGroup.DOKill();
        }
    }
}

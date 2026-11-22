using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace MahjongAtelier.UI
{
    /// <summary>
    /// 페이드인/아웃 컨트롤러.
    /// 
    /// 사용:
    ///   - Scene 진입 시: FadeIn (검정 → 투명)
    ///   - Scene 떠날 시: FadeOut (투명 → 검정) 후 콜백
    /// 
    /// Inspector 구조:
    ///   FadeController (이 컴포넌트)
    ///   └── FadeImage (Image, 검정 전체 덮개, 처음엔 alpha=1)
    /// 
    /// Canvas에서 가장 마지막 자식으로 두면 모든 UI 위에 덮임 (페이드 효과 정확).
    /// </summary>
    public class FadeController : MonoBehaviour
    {
        [Header("Fade Image")]
        [Tooltip("검정/단색 전체 덮개 이미지")]
        [SerializeField] private Image fadeImage;

        [Header("Timing")]
        [SerializeField, Range(0.1f, 3f)]
        private float defaultDuration = 0.6f;

        [Header("On Awake")]
        [Tooltip("Scene 진입 시 자동으로 FadeIn 실행")]
        [SerializeField] private bool fadeInOnAwake = true;

        private Coroutine _activeFade;

        private void Awake()
        {
            // 초기 상태: 완전 불투명 (Scene 시작 시 검정 화면)
            if (fadeImage != null)
            {
                var c = fadeImage.color;
                c.a = 1f;
                fadeImage.color = c;
                fadeImage.raycastTarget = true; // 페이드 중엔 클릭 차단
            }
        }

        private void Start()
        {
            if (fadeInOnAwake)
                FadeIn();
        }

        /// <summary>페이드인 (검정 → 투명). Scene 진입 시.</summary>
        public void FadeIn(float? duration = null, Action onComplete = null)
        {
            if (_activeFade != null) StopCoroutine(_activeFade);
            _activeFade = StartCoroutine(FadeCoroutine(1f, 0f, duration ?? defaultDuration, onComplete, hideAfter: true));
        }

        /// <summary>페이드아웃 (투명 → 검정). Scene 떠날 시.</summary>
        public void FadeOut(float? duration = null, Action onComplete = null)
        {
            if (_activeFade != null) StopCoroutine(_activeFade);
            _activeFade = StartCoroutine(FadeCoroutine(0f, 1f, duration ?? defaultDuration, onComplete, hideAfter: false));
        }

        private IEnumerator FadeCoroutine(float from, float to, float duration, Action onComplete, bool hideAfter)
        {
            if (fadeImage == null)
            {
                onComplete?.Invoke();
                yield break;
            }

            fadeImage.raycastTarget = true;

            float elapsed = 0f;
            var c = fadeImage.color;
            c.a = from;
            fadeImage.color = c;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                c.a = Mathf.Lerp(from, to, t);
                fadeImage.color = c;
                yield return null;
            }

            // 완료 상태 보장
            c.a = to;
            fadeImage.color = c;

            // FadeIn 완료 시엔 클릭 차단 해제
            if (hideAfter)
                fadeImage.raycastTarget = false;

            _activeFade = null;
            onComplete?.Invoke();
        }
    }
}

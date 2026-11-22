using System.Collections;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using CardGame.Core;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace CardGame.UI
{
    /// <summary>
    /// Title 씬: 로고 펄스 + Press Any Key.
    ///
    /// 변경:
    ///   - useLoadingScreen 기본 true
    ///   - delayBeforeAcceptInput 0.1초로 단축 (거의 즉시 활성화)
    ///   - Release 대기를 마우스/터치만 체크 (키보드 Caps/NumLock 등 토글 상태 영향 제거)
    ///   - Release 대기 최대 0.3초 timeout — 무한 대기 방지
    /// </summary>
    public class TitleSceneController : MonoBehaviour
    {
        [Header("Visual")]
        [SerializeField] private RectTransform logoTransform;
        [SerializeField] private TMP_Text pressAnyKeyText;
        [SerializeField] private CanvasGroup pressAnyKeyGroup;

        [Header("Animation")]
        [SerializeField] private float logoScalePulse = 1.05f;
        [SerializeField] private float logoPulseDuration = 1.5f;
        [SerializeField] private float textBlinkDuration = 0.8f;
        [SerializeField] private float textMinAlpha = 0.3f;

        [Header("Transition")]
        [Tooltip("LoadScene을 거쳐 Lobby로 전환. true 권장.")]
        [SerializeField] private bool useLoadingScreen = true;
        [Tooltip("Scene 진입 후 입력 수신까지 지연 (초). Fade in 안정화를 위한 최소값.")]
        [SerializeField] private float delayBeforeAcceptInput = 0.1f;
        [Tooltip("마우스/터치가 눌린 상태로 진입했을 때 release 대기 최대 시간 (초). timeout 걸리면 강제 활성화.")]
        [SerializeField] private float maxReleaseWait = 0.3f;

        private bool _acceptInput = false;
        private bool _transitioning = false;

        private void Start()
        {
            if (logoTransform != null)
            {
                logoTransform.DOScale(logoScalePulse, logoPulseDuration)
                    .SetEase(Ease.InOutSine)
                    .SetLoops(-1, LoopType.Yoyo);
            }

            if (pressAnyKeyGroup != null)
            {
                pressAnyKeyGroup.DOFade(textMinAlpha, textBlinkDuration)
                    .SetEase(Ease.InOutSine)
                    .SetLoops(-1, LoopType.Yoyo);
            }

            StartCoroutine(EnableInputAfterDelay());
        }

        private IEnumerator EnableInputAfterDelay()
        {
            yield return new WaitForSeconds(delayBeforeAcceptInput);

            // ★ 마우스/터치만 체크 (키보드 토글 키 영향 배제) + timeout
            float waitStart = Time.unscaledTime;
            while (IsPointerInputHeld() && Time.unscaledTime - waitStart < maxReleaseWait)
            {
                yield return null;
            }

            _acceptInput = true;
            Debug.Log("[TitleScene] 입력 수신 활성화");
        }

        private void Update()
        {
            if (!_acceptInput || _transitioning) return;
            if (IsAnyInputPressed()) GoToLobby();
        }

        /// <summary>그 프레임에 새로 눌린 입력이 있는가.</summary>
        private bool IsAnyInputPressed()
        {
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame) return true;
            if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame) return true;
            if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame) return true;
            return false;
#else
            if (Input.anyKeyDown) return true;
            if (Input.GetMouseButtonDown(0)) return true;
            return false;
#endif
        }

        /// <summary>마우스 좌클릭 / 터치가 눌려있는 상태인가. (키보드 제외)</summary>
        private bool IsPointerInputHeld()
        {
#if ENABLE_INPUT_SYSTEM
            if (Mouse.current != null && Mouse.current.leftButton.isPressed) return true;
            if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.isPressed) return true;
            return false;
#else
            if (Input.GetMouseButton(0)) return true;
            return false;
#endif
        }

        private void GoToLobby()
        {
            _transitioning = true;
            if (logoTransform != null) logoTransform.DOKill();
            if (pressAnyKeyGroup != null) pressAnyKeyGroup.DOKill();
            SceneLoader.Instance?.Load(GameScene.LobbyScene, useLoadingScreen);
        }
    }
}
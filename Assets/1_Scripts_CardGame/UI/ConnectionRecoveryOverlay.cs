using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using CardGame.Network;

namespace CardGame.UI
{
    /// <summary>
    /// 연결 복구 안내 — 호스트와 끊겼을 때 "다시 연결하는 중 / 호스트 이전 중" 메시지를 띄우고 입력을 막음.
    ///
    ///   - CardGameNetworkManager.OnRecoveryStateChanged(active, message) 구독
    ///   - 복구에 성공하면 게임 씬이 다시 로드되면서 자연스럽게 사라짐
    ///   - [나가기] 버튼(선택)으로 기다리지 않고 로비로 복귀 가능
    ///   - CanvasGroup 패턴: CardGameScene Canvas 최상단에 활성 상태로 배치. Awake에서 숨김
    ///
    /// 계층 (예):
    ///   ConnectionRecoveryOverlay (CanvasGroup + 이 컴포넌트, stretch)
    ///   ├─ Dim (Image 반투명 검정, stretch, Raycast Target ✓)
    ///   ├─ Spinner (Image, 선택)       → spinner
    ///   ├─ Message (TMP)               → messageText
    ///   └─ Btn_Leave (선택) "나가기"    → leaveButton
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public class ConnectionRecoveryOverlay : MonoBehaviour
    {
        [SerializeField] private TMP_Text messageText;
        [SerializeField] private Transform spinner;
        [SerializeField] private float spinnerSpeed = 180f;
        [SerializeField] private Button leaveButton;
        [SerializeField] private float fadeDuration = 0.2f;

        private CanvasGroup _cg;
        private bool _visible;
        private CardGameNetworkManager _net;

        private void Awake()
        {
            _cg = GetComponent<CanvasGroup>();
            _cg.alpha = 0f;
            _cg.blocksRaycasts = false;
            _cg.interactable = false;
            if (leaveButton != null) leaveButton.onClick.AddListener(OnLeaveClicked);
        }

        private void OnEnable()
        {
            _net = CardGameNetworkManager.singleton;
            if (_net == null) return;   // 싱글플레이 등 NetworkManager가 없는 경우
            _net.OnRecoveryStateChanged += HandleState;
            HandleState(_net.RecoveryOverlayActive, _net.RecoveryMessage);
        }

        private void OnDisable()
        {
            if (_net != null) _net.OnRecoveryStateChanged -= HandleState;
            _net = null;
            _cg.DOKill();
        }

        private void Update()
        {
            if (_visible && spinner != null)
                spinner.Rotate(0f, 0f, -spinnerSpeed * Time.unscaledDeltaTime);
        }

        private void HandleState(bool active, string message)
        {
            if (messageText != null && !string.IsNullOrEmpty(message)) messageText.text = message;

            if (active == _visible) return;
            _visible = active;

            _cg.DOKill();
            _cg.blocksRaycasts = active;
            _cg.interactable = active;
            _cg.DOFade(active ? 1f : 0f, fadeDuration).SetUpdate(true).SetLink(gameObject);
        }

        private void OnLeaveClicked()
        {
            if (_net != null) _net.LeaveSession();
        }
    }
}

using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using CardGame.Core;

namespace CardGame.UI
{
    /// <summary>
    /// [승리 선언] 버튼 — 자기 차례에 카드를 뽑은 직후 손패가 완성이면 나타남.
    ///
    ///   - CardGameManager.OnWinAvailableChanged(seat, available) 구독. seat == UserPlayerIndex 일 때만 반응
    ///   - 나타날 때 팝 애니메이션 + 은은한 펄스, 사라질 때 축소
    ///   - 클릭 → CardGameManager.DeclareWin() (로컬/호스트는 즉시 판정, 클라이언트는 호스트에 전송)
    ///   - CanvasGroup 패턴: Scene에 활성 상태로 배치. Awake에서 숨김
    ///
    /// 세팅: Canvas 아래 Button 오브젝트에 부착 (CanvasGroup 자동 추가). 손패 위쪽/오른쪽 권장.
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public class WinDeclareButton : MonoBehaviour
    {
        [SerializeField] private CardGameManager gameManager;
        [SerializeField] private Button button;
        [SerializeField] private TMP_Text label;
        [SerializeField] private string labelText = "승리 선언!";

        [Header("Animation")]
        [SerializeField] private float showDuration = 0.25f;
        [SerializeField] private float hideDuration = 0.15f;
        [SerializeField] private bool pulse = true;
        [SerializeField] private float pulseScale = 1.08f;
        [SerializeField] private float pulseDuration = 0.6f;

        private CanvasGroup _cg;
        private RectTransform _rt;
        private Vector3 _baseScale;
        private Tween _pulseTween;
        private bool _visible;

        private void Awake()
        {
            _cg = GetComponent<CanvasGroup>();
            _rt = GetComponent<RectTransform>();
            _baseScale = _rt.localScale;
            if (button == null) button = GetComponent<Button>();
            if (label != null) label.text = labelText;
            if (button != null) button.onClick.AddListener(OnClick);
            HideImmediate();
        }

        private void OnEnable()
        {
            if (gameManager == null) gameManager = FindObjectOfType<CardGameManager>();
            if (gameManager != null) gameManager.OnWinAvailableChanged += HandleWinAvailable;
            CardGameEvents.OnDealStarted += HideFromEvent;
            CardGameEvents.OnTurnStarted += HandleTurnStarted;
        }

        private void OnDisable()
        {
            if (gameManager != null) gameManager.OnWinAvailableChanged -= HandleWinAvailable;
            CardGameEvents.OnDealStarted -= HideFromEvent;
            CardGameEvents.OnTurnStarted -= HandleTurnStarted;
            KillTweens();
        }

        private void HandleWinAvailable(int seat, bool available)
        {
            if (gameManager == null || seat != gameManager.UserPlayerIndex) return;
            if (available) Show(); else Hide();
        }

        private void HandleTurnStarted(int seat)
        {
            if (gameManager != null && seat != gameManager.UserPlayerIndex) Hide();
        }

        private void HideFromEvent() => Hide();

        private void OnClick()
        {
            if (gameManager == null) return;
            if (!gameManager.CanUserDeclareWin) { Hide(); return; }
            button.interactable = false;   // 중복 클릭 방지. 거부되면 다시 켬
            bool ok = gameManager.DeclareWin();
            if (!ok) { button.interactable = true; Hide(); }
        }

        // ---------------------------------------------------------------

        private void Show()
        {
            if (_visible) return;
            _visible = true;
            KillTweens();
            if (button != null) button.interactable = true;
            _cg.blocksRaycasts = true;
            _cg.interactable = true;
            _rt.localScale = _baseScale * 0.6f;
            _cg.DOFade(1f, showDuration).SetLink(gameObject);
            _rt.DOScale(_baseScale, showDuration).SetEase(Ease.OutBack).SetLink(gameObject)
               .OnComplete(() =>
               {
                   if (!pulse || !_visible) return;
                   _pulseTween = _rt.DOScale(_baseScale * pulseScale, pulseDuration)
                                    .SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo).SetLink(gameObject);
               });
        }

        private void Hide()
        {
            if (!_visible) return;
            _visible = false;
            KillTweens();
            _cg.blocksRaycasts = false;
            _cg.interactable = false;
            _cg.DOFade(0f, hideDuration).SetLink(gameObject);
            _rt.DOScale(_baseScale * 0.8f, hideDuration).SetEase(Ease.InQuad).SetLink(gameObject);
        }

        private void HideImmediate()
        {
            _visible = false;
            _cg.alpha = 0f;
            _cg.blocksRaycasts = false;
            _cg.interactable = false;
            _rt.localScale = _baseScale;
        }

        private void KillTweens()
        {
            _pulseTween?.Kill();
            _pulseTween = null;
            _cg.DOKill();
            _rt.DOKill();
        }
    }
}

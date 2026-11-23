using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using CardGame.Core;

namespace CardGame.UI
{
    /// <summary>
    /// 온라인 턴 타이머 표시 — 남은 초 숫자 (게이지 없음).
    ///
    ///   - CardGameManager.OnTurnTimerChanged(seat, duration) 구독. seat &lt; 0 이면 숨김
    ///   - 버리기 제한(30초), Peek/Swap 선택 제한(15초) 모두 같은 타이머로 표시
    ///   - 남은 시간 cautionThreshold(15초) 이하: 주황색 + 한 번 살짝 뜀
    ///   - 남은 시간 warningThreshold(5초) 이하: 빨간색 + 매초 숫자가 뜀
    ///   - 싱글플레이는 제한 시간이 없으므로 나타나지 않음
    ///   - CanvasGroup 패턴: Scene에 활성 상태로 배치. Awake에서 숨김
    ///
    /// 계층 (예):
    ///   TurnTimer (CanvasGroup + 이 컴포넌트)
    ///   ├─ Seconds (TMP)                                                                → secondsText
    ///   └─ Owner (TMP, 선택)  "OO의 차례"                                                → ownerText
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public class TurnTimerUI : MonoBehaviour
    {
        [SerializeField] private CardGameManager gameManager;
        [SerializeField] private TMP_Text secondsText;
        [SerializeField] private TMP_Text ownerText;

        [Header("표시")]
        [Tooltip("true: 내 차례의 타이머만 표시. false: 누구 차례든 표시.")]
        [SerializeField] private bool onlyMyTimer = false;
        [SerializeField] private string myTurnLabel = "내 차례";
        [SerializeField] private string otherTurnFormat = "{0}의 차례";

        [Header("색")]
        [SerializeField] private Color normalColor = Color.white;
        [Tooltip("남은 시간이 cautionThreshold 이하일 때 (주황).")]
        [SerializeField] private Color cautionColor = new Color(1f, 0.62f, 0.15f);
        [SerializeField] private float cautionThreshold = 15f;
        [Tooltip("남은 시간이 warningThreshold 이하일 때 (빨강).")]
        [SerializeField] private Color warningColor = new Color(1f, 0.3f, 0.25f);
        [SerializeField] private float warningThreshold = 5f;

        [Header("Fade")]
        [SerializeField] private float fadeDuration = 0.15f;

        private CanvasGroup _cg;
        private bool _visible;
        private float _duration;
        private int _lastShownSecond = -1;
        private int _lastLevel = -1;   // 0=보통 1=주의 2=경고
        private bool _hooked;

        private void Awake()
        {
            _cg = GetComponent<CanvasGroup>();
            _cg.alpha = 0f;
            _cg.blocksRaycasts = false;
            _cg.interactable = false;
        }

        private void OnEnable()
        {
            if (gameManager == null) gameManager = FindObjectOfType<CardGameManager>();
            if (gameManager != null && !_hooked)
            {
                gameManager.OnTurnTimerChanged += HandleTimerChanged;
                _hooked = true;
                // 이미 돌고 있는 타이머가 있으면 (재접속 직후 등) 바로 반영
                if (gameManager.TurnTimerSeat >= 0)
                    HandleTimerChanged(gameManager.TurnTimerSeat, gameManager.TurnTimerRemaining);
            }
        }

        private void OnDisable()
        {
            if (gameManager != null && _hooked) gameManager.OnTurnTimerChanged -= HandleTimerChanged;
            _hooked = false;
            _cg.DOKill();
            if (secondsText != null) secondsText.transform.DOKill();
        }

        private void HandleTimerChanged(int seat, float duration)
        {
            if (gameManager == null || seat < 0 || duration <= 0f) { Hide(); return; }

            bool mine = seat == gameManager.UserPlayerIndex;
            if (onlyMyTimer && !mine) { Hide(); return; }

            _duration = Mathf.Max(0.01f, duration);
            _lastShownSecond = -1;
            _lastLevel = -1;

            if (ownerText != null)
            {
                if (mine) ownerText.text = myTurnLabel;
                else
                {
                    string name = seat < gameManager.Players.Count ? gameManager.Players[seat].DisplayName : $"P{seat + 1}";
                    ownerText.text = string.Format(otherTurnFormat, name);
                }
            }

            Refresh();
            Show();
        }

        private void Update()
        {
            if (!_visible || gameManager == null) return;
            if (gameManager.TurnTimerSeat < 0) { Hide(); return; }
            Refresh();
        }

        private void Refresh()
        {
            float remaining = gameManager.TurnTimerRemaining;
            int sec = Mathf.CeilToInt(remaining);
            if (sec == _lastShownSecond) return;
            _lastShownSecond = sec;

            int level = sec <= warningThreshold ? 2 : sec <= cautionThreshold ? 1 : 0;
            Color c = level == 2 ? warningColor : level == 1 ? cautionColor : normalColor;

            if (secondsText != null)
            {
                secondsText.text = sec.ToString();
                secondsText.color = c;

                // 경고(빨강): 매초 뜀 / 주의(주황): 처음 바뀔 때 한 번만 살짝
                bool pop = level == 2 || (level == 1 && _lastLevel != 1 && _lastLevel != -1);
                if (pop)
                {
                    var t = secondsText.transform;
                    t.DOKill();
                    t.localScale = Vector3.one * (level == 2 ? 1.25f : 1.15f);
                    t.DOScale(1f, 0.25f).SetEase(Ease.OutQuad).SetLink(secondsText.gameObject);
                }
            }
            _lastLevel = level;
        }

        private void Show()
        {
            if (_visible) return;
            _visible = true;
            _cg.DOKill();
            _cg.DOFade(1f, fadeDuration).SetLink(gameObject);
        }

        private void Hide()
        {
            if (!_visible) return;
            _visible = false;
            _cg.DOKill();
            _cg.DOFade(0f, fadeDuration).SetLink(gameObject);
        }
    }
}

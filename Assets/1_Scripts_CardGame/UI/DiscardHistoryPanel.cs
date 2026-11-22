using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using CardGame.Core;

namespace CardGame.UI
{
    /// <summary>
    /// 버린 카드 열람 패널 — 중앙 더미 클릭 시 열림.
    ///
    ///   - 라운드 동안 버려진 모든 카드를 순서대로 기록 (누가 버렸는지 포함)
    ///   - 플레이어별 필터 탭 (전체 / P1~P4), 최신순/오래된순 토글
    ///   - CanvasGroup 패턴 (ModeSelectDialog와 동일): Scene에 활성 배치, Show/Hide/Toggle
    ///   - 열려 있는 동안 새 카드가 버려지면 실시간 갱신
    ///   - 온라인 클라이언트에서도 동작 (ClientApplyCardDiscarded가 같은 이벤트 발행)
    ///
    /// 패널 계층 (예):
    ///   DiscardHistoryPanel (CanvasGroup + 이 컴포넌트)
    ///   ├─ Dim (Image 반투명, stretch, Button → closeOnDim)
    ///   ├─ Panel
    ///   │  ├─ Header: TitleText "버린 카드"  CountText "(12장)"   CloseButton
    ///   │  ├─ Filters: Btn_All  Btn_P1  Btn_P2  Btn_P3  Btn_P4    NewestFirstToggle
    ///   │  └─ ScrollView → Viewport → Content (Grid Layout Group + Content Size Fitter)  → gridContent
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public class DiscardHistoryPanel : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("비워두면 자동 검색. 플레이어 이름 표시용.")]
        [SerializeField] private CardGameManager gameManager;
        [Tooltip("한 칸 프리팹 (DiscardHistoryEntry). 비우면 cardPrefab을 그대로 사용.")]
        [SerializeField] private DiscardHistoryEntry entryPrefab;
        [Tooltip("entryPrefab이 없을 때 쓰는 카드 프리팹 (CardView 포함).")]
        [SerializeField] private GameObject cardPrefab;
        [SerializeField] private RectTransform gridContent;

        [Header("UI")]
        [SerializeField] private TMP_Text countText;
        [SerializeField] private Button closeButton;
        [SerializeField] private Button dimButton;
        [SerializeField] private Toggle newestFirstToggle;
        [Tooltip("필터 버튼: [0]=전체, [1..4]=플레이어 1~4. 없는 건 비워도 됨.")]
        [SerializeField] private Button[] filterButtons = new Button[5];
        [SerializeField] private Color filterActiveColor = new Color(1f, 0.85f, 0.2f);
        [SerializeField] private Color filterNormalColor = Color.white;

        [Header("Owner Colors (좌석별)")]
        [SerializeField] private Color[] seatColors =
        {
            new Color(0.35f, 0.65f, 1f),   // P1 파랑
            new Color(1f, 0.45f, 0.45f),   // P2 빨강
            new Color(0.45f, 0.85f, 0.45f),// P3 초록
            new Color(1f, 0.8f, 0.35f),    // P4 노랑
        };

        [Header("Fade")]
        [SerializeField] private float fadeDuration = 0.2f;

        private struct Entry
        {
            public int Seat;
            public CardData Card;
            public int Order;
        }

        private readonly List<Entry> _history = new List<Entry>();
        private readonly List<GameObject> _spawned = new List<GameObject>();
        private CanvasGroup _cg;
        private bool _isShowing;
        private int _filterSeat = -1;   // -1 = 전체
        private bool _newestFirst = true;

        public bool IsShowing => _isShowing;
        public int TotalDiscarded => _history.Count;

        // =====================================================================

        private void Awake()
        {
            _cg = GetComponent<CanvasGroup>();
            if (_cg == null) _cg = gameObject.AddComponent<CanvasGroup>();
            HideImmediate();

            if (gameManager == null) gameManager = FindObjectOfType<CardGameManager>();

            if (closeButton != null) closeButton.onClick.AddListener(Hide);
            if (dimButton != null) dimButton.onClick.AddListener(Hide);
            if (newestFirstToggle != null)
            {
                _newestFirst = newestFirstToggle.isOn;
                newestFirstToggle.onValueChanged.AddListener(v => { _newestFirst = v; Rebuild(); });
            }

            for (int i = 0; i < filterButtons.Length; i++)
            {
                var b = filterButtons[i];
                if (b == null) continue;
                int seat = i - 1;   // [0]=전체(-1)
                b.onClick.AddListener(() => SetFilter(seat));
            }
            RefreshFilterVisual();
        }

        private void OnEnable()
        {
            CardGameEvents.OnCardDiscarded += HandleDiscarded;
            CardGameEvents.OnDealStarted += HandleDealStarted;
        }

        private void OnDisable()
        {
            CardGameEvents.OnCardDiscarded -= HandleDiscarded;
            CardGameEvents.OnDealStarted -= HandleDealStarted;
        }

        // =====================================================================
        //  Data
        // =====================================================================

        private void HandleDealStarted()
        {
            _history.Clear();
            if (_isShowing) Rebuild();
            UpdateCount();
        }

        private void HandleDiscarded(int seat, CardData card)
        {
            if (card == null || card.IsPlaceholder) return;
            _history.Add(new Entry { Seat = seat, Card = card, Order = _history.Count + 1 });
            if (_isShowing) Rebuild();
            UpdateCount();
        }

        // =====================================================================
        //  Show / Hide
        // =====================================================================

        public void Show()
        {
            if (_isShowing) return;
            _isShowing = true;
            Rebuild();

            _cg.DOKill();
            _cg.blocksRaycasts = true;
            _cg.interactable = true;
            _cg.DOFade(1f, fadeDuration).SetLink(gameObject);
        }

        public void Hide()
        {
            if (!_isShowing) return;
            _isShowing = false;

            _cg.DOKill();
            _cg.blocksRaycasts = false;
            _cg.interactable = false;
            _cg.DOFade(0f, fadeDuration).SetLink(gameObject);
        }

        public void Toggle()
        {
            if (_isShowing) Hide(); else Show();
        }

        private void HideImmediate()
        {
            _cg.alpha = 0f;
            _cg.blocksRaycasts = false;
            _cg.interactable = false;
            _isShowing = false;
        }

        private void Update()
        {
            if (!_isShowing) return;
#if ENABLE_INPUT_SYSTEM
            if (UnityEngine.InputSystem.Keyboard.current != null &&
                UnityEngine.InputSystem.Keyboard.current.escapeKey.wasPressedThisFrame)
                Hide();
#else
            if (Input.GetKeyDown(KeyCode.Escape)) Hide();
#endif
        }

        // =====================================================================
        //  Filter / Build
        // =====================================================================

        public void SetFilter(int seat)
        {
            _filterSeat = seat;
            RefreshFilterVisual();
            Rebuild();
        }

        private void RefreshFilterVisual()
        {
            for (int i = 0; i < filterButtons.Length; i++)
            {
                var b = filterButtons[i];
                if (b == null) continue;
                int seat = i - 1;
                var img = b.targetGraphic as Image;
                if (img != null) img.color = (seat == _filterSeat) ? filterActiveColor : filterNormalColor;

                // 존재하지 않는 좌석 버튼 숨김
                if (seat >= 0 && gameManager != null && gameManager.Players != null)
                    b.gameObject.SetActive(seat < gameManager.Players.Count);
            }
        }

        private void Rebuild()
        {
            ClearSpawned();
            if (gridContent == null) return;

            var list = new List<Entry>();
            foreach (var e in _history)
                if (_filterSeat < 0 || e.Seat == _filterSeat) list.Add(e);
            if (_newestFirst) list.Reverse();

            foreach (var e in list) SpawnEntry(e);

            LayoutRebuilder.ForceRebuildLayoutImmediate(gridContent);
            UpdateCount();
        }

        private void SpawnEntry(Entry e)
        {
            string owner = OwnerName(e.Seat);
            Color color = SeatColor(e.Seat);

            if (entryPrefab != null)
            {
                var entry = Instantiate(entryPrefab, gridContent);
                entry.Bind(e.Card, owner, color, e.Order);
                _spawned.Add(entry.gameObject);
                return;
            }

            if (cardPrefab == null) return;
            var go = Instantiate(cardPrefab, gridContent);
            go.SetActive(true);
            _spawned.Add(go);

            var view = go.GetComponent<CardView>();
            if (view != null) view.SetCard(e.Card);

            var click = go.GetComponent<CardClickHandler>();
            if (click != null) Destroy(click);
            var hover = go.GetComponent<CardHoverHandler>();
            if (hover != null) Destroy(hover);
            var le = go.GetComponent<LayoutElement>();
            if (le != null) le.ignoreLayout = false;
            var cg = go.GetComponent<CanvasGroup>();
            if (cg != null) { cg.alpha = 1f; cg.blocksRaycasts = false; }
            go.transform.localScale = Vector3.one;
            go.transform.localRotation = Quaternion.identity;
        }

        private void ClearSpawned()
        {
            foreach (var go in _spawned) if (go != null) Destroy(go);
            _spawned.Clear();
        }

        private void UpdateCount()
        {
            if (countText == null) return;
            int shown = 0;
            foreach (var e in _history) if (_filterSeat < 0 || e.Seat == _filterSeat) shown++;
            countText.text = _filterSeat < 0 ? $"{_history.Count}장" : $"{shown}장 / 전체 {_history.Count}장";
        }

        /// <summary>좌석 표시 이름 지정 (온라인 닉네임 등). 미지정 시 "P1"~"P4".</summary>
        public void SetOwnerName(int seat, string name)
        {
            if (seat < 0 || seat >= _ownerNames.Length) return;
            _ownerNames[seat] = name;
            if (_isShowing) Rebuild();
        }

        private readonly string[] _ownerNames = new string[8];

        private string OwnerName(int seat)
        {
            if (seat >= 0 && seat < _ownerNames.Length && !string.IsNullOrEmpty(_ownerNames[seat]))
                return _ownerNames[seat];
            if (gameManager != null && seat == gameManager.UserPlayerIndex) return "나";
            // 온라인: Steam 닉네임 / BOT 이름
            if (gameManager != null && gameManager.IsOnline && seat >= 0 && seat < gameManager.Players.Count
                && !string.IsNullOrEmpty(gameManager.Players[seat].DisplayName))
                return gameManager.Players[seat].DisplayName;
            return $"P{seat + 1}";
        }

        private Color SeatColor(int seat)
        {
            if (seatColors == null || seatColors.Length == 0) return Color.white;
            return seatColors[Mathf.Clamp(seat, 0, seatColors.Length - 1)];
        }
    }
}

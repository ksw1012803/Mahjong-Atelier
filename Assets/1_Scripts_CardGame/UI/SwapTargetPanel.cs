using System;
using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using CardGame.Core;

namespace CardGame.UI
{
    /// <summary>
    /// Swap 카드 발동 시 대상 선택 UI.
    /// 
    /// 흐름:
    ///   1) 다른 플레이어 목록 표시 → 선택
    ///   2) 자기 손패에서 줄 카드 선택
    ///   3) 상대 손패에서 받을 카드 선택 (블라인드 - 뒷면만 보임)
    /// 
    /// 단순화: 자기 카드는 랜덤, 상대 카드도 랜덤 위치 (블라인드)
    /// </summary>
    public class SwapTargetPanel : MonoBehaviour
    {
        [Header("Dialog")]
        [SerializeField] private GameObject dialogContainer;
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text instructionText;

        [Header("Player Buttons (최대 3 - 자신 제외)")]
        [SerializeField] private Button[] targetButtons = new Button[3];
        [SerializeField] private TMP_Text[] targetLabels = new TMP_Text[3];
        [Tooltip("대상 버튼별 캐릭터 초상화 (선택).")]
        [SerializeField] private Image[] targetPortraits = new Image[3];
        [Tooltip("대상 버튼별 손패 장수 텍스트 (선택). 비어 있으면 이름 옆에 함께 표시.")]
        [SerializeField] private TMP_Text[] targetCounts = new TMP_Text[3];

        [Header("Show 연출")]
        [SerializeField] private RectTransform popTarget;
        [SerializeField] private float popDuration = 0.22f;

        [Header("Cancel")]
        [Tooltip("무작위로 선택 — 상대와 카드를 랜덤으로 골라 교환.")]
        [SerializeField] private Button cancelButton;
        [Tooltip("취소 — 교환하지 않고 넘어감 (카드는 이미 사용됨).")]
        [SerializeField] private Button skipButton;

        /// <summary>Target 플레이어, 자기 카드, 상대 카드 전달.</summary>
        public event Action<Player, CardData, CardData> OnTargetSelected;

        /// <summary>'취소'를 눌러 교환하지 않기로 함.</summary>
        public event Action OnSkipped;

        private Player _source;
        private IReadOnlyList<Player> _allPlayers;
        private List<Player> _validTargets = new List<Player>();

        private void Awake()
        {
            if (dialogContainer != null) dialogContainer.SetActive(false);

            for (int i = 0; i < targetButtons.Length; i++)
            {
                int idx = i; // capture
                if (targetButtons[i] != null)
                    targetButtons[i].onClick.AddListener(() => HandleTargetClicked(idx));
            }

            if (cancelButton != null) cancelButton.onClick.AddListener(HandleCancelClicked);
            if (skipButton != null) skipButton.onClick.AddListener(HandleSkipClicked);
        }

        private void OnDestroy()
        {
            for (int i = 0; i < targetButtons.Length; i++)
            {
                if (targetButtons[i] != null)
                    targetButtons[i].onClick.RemoveAllListeners();
            }
            if (cancelButton != null) cancelButton.onClick.RemoveListener(HandleCancelClicked);
            if (skipButton != null) skipButton.onClick.RemoveListener(HandleSkipClicked);
        }

        private void HandleSkipClicked()
        {
            Debug.Log("[SwapTargetPanel] 취소 (교환 안 함)");
            Hide();
            OnSkipped?.Invoke();
        }

        public void Show(Player source, IReadOnlyList<Player> allPlayers)
        {
            _source = source;
            _allPlayers = allPlayers;
            _validTargets.Clear();

            foreach (var p in allPlayers)
            {
                if (p.Index != source.Index && p.Hand.Count > 0)
                    _validTargets.Add(p);
            }

            if (titleText != null) titleText.text = "교환";
            if (instructionText != null) instructionText.text = "카드를 교환할 상대를 선택하세요";

            var db = CharacterDatabase.Instance;

            for (int i = 0; i < targetButtons.Length; i++)
            {
                if (targetButtons[i] == null) continue;
                if (i < _validTargets.Count)
                {
                    targetButtons[i].gameObject.SetActive(true);
                    var t = _validTargets[i];
                    var countLabel = i < targetCounts.Length ? targetCounts[i] : null;
                    if (targetLabels[i] != null)
                        targetLabels[i].text = countLabel != null ? t.DisplayName : $"{t.DisplayName} ({t.Hand.Count}장)";
                    if (countLabel != null) countLabel.text = $"손패 {t.Hand.Count}장";
                    var portrait = i < targetPortraits.Length ? targetPortraits[i] : null;
                    if (portrait != null)
                    {
                        var def = db != null ? db.Get(t.CharacterIndex) : null;
                        if (def != null && def.portrait != null) portrait.sprite = def.portrait;
                    }
                }
                else
                {
                    targetButtons[i].gameObject.SetActive(false);
                }
            }

            if (dialogContainer != null) dialogContainer.SetActive(true);

            if (popTarget != null)
            {
                popTarget.DOKill();
                popTarget.localScale = Vector3.one * 0.9f;
                popTarget.DOScale(1f, popDuration).SetEase(Ease.OutBack).SetLink(popTarget.gameObject);
            }
        }

        public void Hide()
        {
            if (dialogContainer != null) dialogContainer.SetActive(false);
        }

        private void HandleTargetClicked(int index)
        {
            if (_source == null || index >= _validTargets.Count) return;

            var target = _validTargets[index];
            // 단순화: 자기 카드는 손패 첫 카드, 상대 카드는 랜덤 (블라인드)
            var sourceCard = _source.Hand[0];
            var rng = new System.Random();
            var targetCard = target.Hand[rng.Next(target.Hand.Count)];

            Hide();
            OnTargetSelected?.Invoke(target, sourceCard, targetCard);
        }

        private void HandleCancelClicked()
        {
            Debug.Log("[SwapTargetPanel] 무작위로 선택");
            if (_source == null || _validTargets.Count == 0)
            {
                Hide();
                return;
            }

            var rng = new System.Random();
            var target = _validTargets[rng.Next(_validTargets.Count)];
            var sourceCard = _source.Hand[rng.Next(_source.Hand.Count)];
            var targetCard = target.Hand[rng.Next(target.Hand.Count)];

            Hide();
            OnTargetSelected?.Invoke(target, sourceCard, targetCard);
        }
    }
}

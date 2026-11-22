using System;
using System.Collections.Generic;
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

        [Header("Cancel")]
        [SerializeField] private Button cancelButton;

        /// <summary>Target 플레이어, 자기 카드, 상대 카드 전달.</summary>
        public event Action<Player, CardData, CardData> OnTargetSelected;

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
        }

        private void OnDestroy()
        {
            for (int i = 0; i < targetButtons.Length; i++)
            {
                if (targetButtons[i] != null)
                    targetButtons[i].onClick.RemoveAllListeners();
            }
            if (cancelButton != null) cancelButton.onClick.RemoveListener(HandleCancelClicked);
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

            if (titleText != null) titleText.text = "Swap: 대상 선택";
            if (instructionText != null) instructionText.text = "교환할 상대를 선택하세요";

            for (int i = 0; i < targetButtons.Length; i++)
            {
                if (targetButtons[i] == null) continue;
                if (i < _validTargets.Count)
                {
                    targetButtons[i].gameObject.SetActive(true);
                    if (targetLabels[i] != null)
                        targetLabels[i].text = $"P{_validTargets[i].Index} ({_validTargets[i].Hand.Count}장)";
                }
                else
                {
                    targetButtons[i].gameObject.SetActive(false);
                }
            }

            if (dialogContainer != null) dialogContainer.SetActive(true);
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
            Debug.Log("[SwapTargetPanel] 취소 (기본 랜덤 처리)");
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

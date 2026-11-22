using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using MahjongAtelier.Core;

namespace MahjongAtelier.UI
{
    /// <summary>
    /// 치 옵션 선택 UI v2 — 텍스트 대신 타일 이미지 표시.
    /// 
    /// 동작:
    ///   1) 옵션 N개 받으면 ChiOptionView 프리팹을 N번 생성
    ///   2) 각 ChiOptionView는 슌츠 3장을 실제 패 모양으로 표시
    ///   3) 그 묶음 클릭 → 콜백
    /// </summary>
    public class ChiSelectionUI : MonoBehaviour
    {
        [Header("Dialog Container (자식, 처음 비활성)")]
        [SerializeField] private GameObject dialogContainer;

        [Header("Title")]
        [SerializeField] private TMP_Text titleText;

        [Header("Options")]
        [SerializeField] private RectTransform optionContainer;

        [Tooltip("ChiOptionView 컴포넌트가 붙은 프리팹 (타일 3장 표시 가능)")]
        [SerializeField] private GameObject chiOptionViewPrefab;

        [Header("Cancel")]
        [SerializeField] private Button cancelButton;

        private Action<ChiOption> _callback;
        private readonly List<GameObject> _spawnedOptions = new List<GameObject>();
        private bool _isOpen = false;

        public bool IsOpen => _isOpen;

        private void Awake()
        {
            if (dialogContainer != null) dialogContainer.SetActive(false);
            if (cancelButton != null) cancelButton.onClick.AddListener(HandleCancelClicked);
        }

        private void OnDestroy()
        {
            if (cancelButton != null) cancelButton.onClick.RemoveListener(HandleCancelClicked);
        }

        public void Show(List<ChiOption> options, Action<ChiOption> callback)
        {
            Debug.Log($"[ChiSelectionUI] Show 호출. options={options?.Count ?? 0}");

            ClearSpawnedOptions();
            _callback = callback;

            if (titleText != null) titleText.text = "치할 슌츠를 선택하세요";

            if (options != null && optionContainer != null && chiOptionViewPrefab != null)
            {
                foreach (var opt in options)
                    SpawnOptionView(opt);
            }
            else
            {
                Debug.LogWarning($"[ChiSelectionUI] Show 실패: options={options != null}, container={optionContainer != null}, prefab={chiOptionViewPrefab != null}");
            }

            if (dialogContainer != null) dialogContainer.SetActive(true);
            _isOpen = true;
        }

        public void Hide()
        {
            if (dialogContainer != null) dialogContainer.SetActive(false);
            ClearSpawnedOptions();
            _callback = null;
            _isOpen = false;
        }

        private void SpawnOptionView(ChiOption option)
        {
            var go = Instantiate(chiOptionViewPrefab, optionContainer);
            go.SetActive(true);
            _spawnedOptions.Add(go);

            var view = go.GetComponent<ChiOptionView>();
            if (view != null)
            {
                view.Setup(option, OnOptionSelected);
            }
            else
            {
                Debug.LogWarning("[ChiSelectionUI] chiOptionViewPrefab에 ChiOptionView 컴포넌트 없음!");
            }
        }

        private void OnOptionSelected(ChiOption option)
        {
            Debug.Log($"[ChiSelectionUI] 옵션 선택됨");
            var cb = _callback;
            Hide();
            cb?.Invoke(option);
        }

        private void HandleCancelClicked()
        {
            Debug.Log("[ChiSelectionUI] Cancel 클릭");
            var cb = _callback;
            Hide();
            cb?.Invoke(null);
        }

        private void ClearSpawnedOptions()
        {
            foreach (var go in _spawnedOptions)
                if (go != null) Destroy(go);
            _spawnedOptions.Clear();
        }
    }
}
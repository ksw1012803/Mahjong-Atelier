using System;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using CardGame.Core;

namespace CardGame.UI
{
    /// <summary>캐릭터 선택창의 카드 한 장 (초상화 + 이름 + 선택 표시).</summary>
    public class CharacterCardButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private Button button;
        [SerializeField] private Image portrait;
        [SerializeField] private TMP_Text nameText;
        [Tooltip("선택된 카드에만 켜지는 오브젝트 (밝은 테두리, '선택됨' 표시 등).")]
        [SerializeField] private GameObject selectedMark;
        [SerializeField] private float hoverScale = 1.05f;

        private int _index;
        private Action<int> _onClick;
        private Vector3 _baseScale = Vector3.one;

        private void Awake()
        {
            _baseScale = transform.localScale;
            if (button == null) button = GetComponent<Button>();
            if (button != null) button.onClick.AddListener(() => _onClick?.Invoke(_index));
        }

        private void OnDisable()
        {
            transform.DOKill();
            transform.localScale = _baseScale;
        }

        public void Bind(int index, CharacterDefinition def, Action<int> onClick)
        {
            _index = index;
            _onClick = onClick;
            if (def == null) return;
            if (portrait != null) portrait.sprite = def.portrait;
            if (nameText != null) nameText.text = string.IsNullOrEmpty(def.displayName) ? def.name : def.displayName;
        }

        public void SetSelected(bool selected)
        {
            if (selectedMark != null) selectedMark.SetActive(selected);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            transform.DOKill();
            transform.DOScale(_baseScale * hoverScale, 0.12f).SetEase(Ease.OutQuad).SetLink(gameObject);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            transform.DOKill();
            transform.DOScale(_baseScale, 0.12f).SetEase(Ease.OutQuad).SetLink(gameObject);
        }
    }
}

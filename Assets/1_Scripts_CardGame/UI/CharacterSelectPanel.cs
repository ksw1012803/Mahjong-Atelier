using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using CardGame.Core;

namespace CardGame.UI
{
    /// <summary>
    /// 로비 캐릭터 선택창.
    ///   - 왼쪽: 고른 캐릭터 전신 미리보기 + 이름 / 오른쪽: 캐릭터 카드 목록
    ///   - 카드를 누르면 바로 선택·저장 → 로비 캐릭터와 프로필이 함께 바뀜 (CharacterSpriteBinder)
    ///   - CanvasGroup 패턴 (활성 상태로 배치, Awake에서 숨김), ESC로 닫기
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public class CharacterSelectPanel : MonoBehaviour
    {
        [Header("List")]
        [SerializeField] private CharacterCardButton cardPrefab;
        [SerializeField] private Transform cardContainer;

        [Header("Preview")]
        [SerializeField] private Image previewImage;
        [SerializeField] private TMP_Text previewName;
        [SerializeField] private TMP_Text previewDescription;

        [Header("Buttons")]
        [SerializeField] private Button closeButton;
        [SerializeField] private Button confirmButton;
        [SerializeField] private Button dimButton;

        [SerializeField] private float fadeDuration = 0.2f;

        private CanvasGroup _cg;
        private readonly List<CharacterCardButton> _cards = new List<CharacterCardButton>();
        private bool _isShowing;

        public bool IsShowing => _isShowing;

        private void Awake()
        {
            _cg = GetComponent<CanvasGroup>();
            _cg.alpha = 0f;
            _cg.blocksRaycasts = false;
            _cg.interactable = false;

            if (closeButton != null) closeButton.onClick.AddListener(Hide);
            if (confirmButton != null) confirmButton.onClick.AddListener(Hide);
            if (dimButton != null) dimButton.onClick.AddListener(Hide);

            BuildCards();
        }

        private void BuildCards()
        {
            var db = CharacterDatabase.Instance;
            if (db == null || cardPrefab == null || cardContainer == null)
            {
                if (db == null) Debug.LogWarning("[CharacterSelect] Resources/CharacterDatabase 없음");
                return;
            }

            // 미리보기용으로 배치해 둔 카드는 정리
            for (int i = cardContainer.childCount - 1; i >= 0; i--) Destroy(cardContainer.GetChild(i).gameObject);
            _cards.Clear();

            for (int i = 0; i < db.Count; i++)
            {
                var card = Instantiate(cardPrefab, cardContainer);
                card.gameObject.SetActive(true);
                card.Bind(i, db.Get(i), Select);
                _cards.Add(card);
            }
        }

        public void Show()
        {
            if (_isShowing) return;
            _isShowing = true;
            RefreshSelection(PlayerProfile.CharacterIndex);

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

        public void Toggle() { if (_isShowing) Hide(); else Show(); }

        private void Select(int index)
        {
            PlayerProfile.SetCharacter(index);
            RefreshSelection(index);
        }

        private void RefreshSelection(int index)
        {
            for (int i = 0; i < _cards.Count; i++) _cards[i].SetSelected(i == index);

            var db = CharacterDatabase.Instance;
            var def = db != null ? db.Get(index) : null;
            if (def == null) return;

            if (previewName != null) previewName.text = string.IsNullOrEmpty(def.displayName) ? def.name : def.displayName;
            if (previewDescription != null) previewDescription.text = def.description ?? "";
            if (previewImage != null && def.fullBody != null)
            {
                previewImage.sprite = def.fullBody;
                previewImage.preserveAspect = true;
                var rt = previewImage.rectTransform;
                rt.DOKill();
                rt.localScale = Vector3.one * 0.96f;
                rt.DOScale(1f, 0.25f).SetEase(Ease.OutBack).SetLink(previewImage.gameObject);
            }
        }

        private void Update()
        {
            if (!_isShowing) return;
#if ENABLE_INPUT_SYSTEM
            if (UnityEngine.InputSystem.Keyboard.current != null &&
                UnityEngine.InputSystem.Keyboard.current.escapeKey.wasPressedThisFrame) Hide();
#else
            if (Input.GetKeyDown(KeyCode.Escape)) Hide();
#endif
        }
    }
}

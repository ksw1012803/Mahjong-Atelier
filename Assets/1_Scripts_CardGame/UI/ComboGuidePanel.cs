using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using CardGame.Core;

namespace CardGame.UI
{
    /// <summary>
    /// 조합 점수표 — 완성 조합 5가지의 이름·점수·조건·예시 손패.
    /// 내용은 CompletionResult(HandAnalyzer.cs)에서 가져오므로 점수/규칙을 바꾸면 표도 자동으로 바뀜.
    /// 예시 카드는 게임에서 쓰는 실제 카드 스프라이트(CardDatabase)를 작게 표시. 스프라이트가 없으면 색 칩 + 숫자.
    ///
    /// CanvasGroup 패턴: 씬에 활성 상태로 배치, Show()/Hide()/Toggle(). ESC·바깥 클릭·닫기 버튼으로 닫힘.
    /// 행은 rowTemplate(비활성)을 복제해서 만듦.
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public class ComboGuidePanel : MonoBehaviour
    {
        [Header("구성")]
        [SerializeField] private Transform rowContainer;
        [Tooltip("행 템플릿 (비활성). 자식: NameText, PointsText, DescText, Example(카드 칩 부모)")]
        [SerializeField] private GameObject rowTemplate;
        [Tooltip("카드 템플릿 (비활성). Image(카드 스프라이트) + 자식 TMP 숫자(스프라이트 없을 때만 사용).")]
        [SerializeField] private GameObject chipTemplate;
        [SerializeField] private TMP_Text rulesText;

        [Header("버튼")]
        [SerializeField] private Button closeButton;
        [SerializeField] private Button dimButton;

        [Header("카드 색")]
        [SerializeField] private Color red = new Color(0.78f, 0.25f, 0.23f);
        [SerializeField] private Color blue = new Color(0.23f, 0.47f, 0.78f);
        [SerializeField] private Color green = new Color(0.24f, 0.61f, 0.34f);
        [SerializeField] private Color yellow = new Color(0.85f, 0.66f, 0.23f);

        [SerializeField] private float fadeDuration = 0.18f;

        private CanvasGroup _cg;
        private bool _isShowing;
        private bool _built;
        private int _shownFrame = -1;

        public bool IsShowing => _isShowing;

        private void Awake()
        {
            _cg = GetComponent<CanvasGroup>();
            _cg.alpha = 0f;
            _cg.blocksRaycasts = false;
            _cg.interactable = false;

            if (closeButton != null) closeButton.onClick.AddListener(Hide);
            if (dimButton != null) dimButton.onClick.AddListener(Hide);
            if (rowTemplate != null) rowTemplate.SetActive(false);
            if (chipTemplate != null) chipTemplate.SetActive(false);
        }

        private void Build()
        {
            if (_built || rowContainer == null || rowTemplate == null) return;
            _built = true;
            var sprites = CollectCardSprites();

            foreach (var pattern in CompletionResult.DisplayOrder)
            {
                var row = Instantiate(rowTemplate, rowContainer);
                row.name = "Row_" + pattern;
                row.SetActive(true);

                SetText(row.transform, "NameText", CompletionResult.GetPatternName(pattern));
                SetText(row.transform, "PointsText", $"{CompletionResult.GetPoints(pattern)}점");
                SetText(row.transform, "DescText", CompletionResult.GetPatternDescription(pattern));

                var example = row.transform.Find("Example");
                if (example != null && chipTemplate != null)
                {
                    foreach (var kind in CompletionResult.GetExample(pattern))
                    {
                        var chip = Instantiate(chipTemplate, example);
                        chip.SetActive(true);
                        var img = chip.GetComponent<Image>();
                        var label = chip.GetComponentInChildren<TMP_Text>(true);
                        sprites.TryGetValue(Key(kind.Color, kind.Number), out var sprite);

                        if (sprite != null && img != null)
                        {
                            // 실제 카드 이미지
                            img.sprite = sprite;
                            img.type = Image.Type.Simple;
                            img.preserveAspect = true;
                            img.color = Color.white;
                            for (int c = 0; c < chip.transform.childCount; c++) chip.transform.GetChild(c).gameObject.SetActive(false);
                        }
                        else
                        {
                            // 스프라이트가 없으면 색 칩 + 숫자
                            if (img != null) img.color = ColorOf(kind.Color);
                            if (label != null) label.text = kind.Number.ToString();
                        }
                    }
                }
            }

            if (rulesText != null) rulesText.text = CompletionResult.GeneralRules;
        }

        private static int Key(CardColor color, int number) => (int)color * 100 + number;

        /// <summary>게임에서 불러온 카드 DB의 2D 스프라이트 (색·숫자별). DB가 없거나 아직 로드 전이면 빈 목록.</summary>
        private static Dictionary<int, Sprite> CollectCardSprites()
        {
            var map = new Dictionary<int, Sprite>();
            var gm = FindAnyObjectByType<CardGameManager>();
            var db = gm != null ? gm.Database : null;
            if (db == null) return map;

            foreach (var def in db.GetAllDefinitions())
            {
                if (def == null || def.isSpecial) continue;
                var sprite = db.GetSprite(def);
                if (sprite != null) map[Key(def.color, def.number)] = sprite;
            }
            return map;
        }

        private static void SetText(Transform root, string child, string text)
        {
            var t = root.Find(child);
            var tmp = t != null ? t.GetComponent<TMP_Text>() : null;
            if (tmp != null) tmp.text = text;
        }

        private Color ColorOf(CardColor c) => c switch
        {
            CardColor.Red => red,
            CardColor.Blue => blue,
            CardColor.Green => green,
            _ => yellow,
        };

        public void Show()
        {
            if (_isShowing) return;
            Build();
            _isShowing = true;
            _shownFrame = Time.frameCount;
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

        private void Update()
        {
            if (!_isShowing || Time.frameCount == _shownFrame) return;
#if ENABLE_INPUT_SYSTEM
            bool esc = UnityEngine.InputSystem.Keyboard.current != null &&
                       UnityEngine.InputSystem.Keyboard.current.escapeKey.wasPressedThisFrame;
#else
            bool esc = Input.GetKeyDown(KeyCode.Escape);
#endif
            if (!esc) return;
            Hide();
            SettingsPanel.MarkEscapeConsumed();   // 같은 ESC로 설정창이 열리지 않게
        }
    }
}

using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using CardGame.Core;

namespace CardGame.UI
{
    /// <summary>
    /// 설정 패널 — 화면 모드 / 해상도 / 화질 / VSync / FPS.
    ///
    /// CanvasGroup 패턴 (ModeSelectDialog와 동일): Scene에 활성 상태로 배치, Show()/Hide()로 토글.
    /// Title·Lobby의 ⚙ 버튼, 인게임 ESC 등 어디서든 Show() 호출.
    ///
    /// 동작:
    ///   - 열 때 저장값(없으면 현재값)으로 UI 채움
    ///   - [적용]  → 적용 + 저장. 해상도/모드가 바뀌면 "유지하시겠습니까?" 10초 카운트다운 (안 누르면 되돌림)
    ///   - [취소]  → 변경 폐기, 닫기
    ///   - [기본값] → 기본값 UI에 채움 (적용 전)
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public class SettingsPanel : MonoBehaviour
    {
        [Header("화면")]
        [SerializeField] private TMP_Dropdown displayModeDropdown;
        [SerializeField] private TMP_Dropdown resolutionDropdown;

        [Header("화질")]
        [SerializeField] private TMP_Dropdown qualityDropdown;
        [SerializeField] private Toggle vsyncToggle;
        [SerializeField] private TMP_Dropdown fpsDropdown;

        [Header("버튼")]
        [SerializeField] private Button applyButton;
        [SerializeField] private Button cancelButton;
        [SerializeField] private Button defaultsButton;
        [SerializeField] private Button closeButton;

        [Header("확인 팝업 (해상도 변경 후 되돌리기)")]
        [Tooltip("해상도/모드 변경 후 N초 안에 '유지'를 안 누르면 이전 설정으로 복구. 0 = 확인 없이 즉시 적용.")]
        [SerializeField] private float confirmSeconds = 10f;
        [SerializeField] private GameObject confirmGroup;
        [SerializeField] private TMP_Text confirmText;
        [SerializeField] private Button confirmKeepButton;
        [SerializeField] private Button confirmRevertButton;

        [Header("Fade")]
        [SerializeField] private float fadeDuration = 0.2f;

        public event System.Action OnClosed;

        private CanvasGroup _cg;
        private bool _isShowing;
        private List<Vector2Int> _resolutions = new List<Vector2Int>();
        private DisplaySettings.Data _applied;    // 마지막으로 확정된 설정
        private Coroutine _confirmRoutine;

        // =====================================================================

        private void Awake()
        {
            _cg = GetComponent<CanvasGroup>();
            if (_cg == null) _cg = gameObject.AddComponent<CanvasGroup>();
            HideImmediate();

            Bind(applyButton, OnApply);
            Bind(cancelButton, Hide);
            Bind(closeButton, Hide);
            Bind(defaultsButton, OnDefaults);
            Bind(confirmKeepButton, OnConfirmKeep);
            Bind(confirmRevertButton, OnConfirmRevert);

            if (displayModeDropdown != null)
                displayModeDropdown.onValueChanged.AddListener(_ => RefreshResolutionInteractable());
            if (vsyncToggle != null)
                vsyncToggle.onValueChanged.AddListener(_ => RefreshFpsInteractable());

            if (confirmGroup != null) confirmGroup.SetActive(false);
        }

        private static void Bind(Button b, UnityEngine.Events.UnityAction a)
        {
            if (b == null) return;
            b.onClick.RemoveAllListeners();
            b.onClick.AddListener(a);
        }

        // =====================================================================
        //  Show / Hide
        // =====================================================================

        public void Show()
        {
            if (_isShowing) return;
            _isShowing = true;

            _applied = DisplaySettings.HasSaved() ? DisplaySettings.Load() : DisplaySettings.Current();
            PopulateDropdowns();
            FillUI(_applied);

            _cg.DOKill();
            _cg.blocksRaycasts = true;
            _cg.interactable = true;
            _cg.DOFade(1f, fadeDuration).SetLink(gameObject);
        }

        public void Hide()
        {
            if (!_isShowing) return;
            _isShowing = false;

            StopConfirm();
            if (confirmGroup != null) confirmGroup.SetActive(false);

            _cg.DOKill();
            _cg.blocksRaycasts = false;
            _cg.interactable = false;
            _cg.DOFade(0f, fadeDuration).SetLink(gameObject);

            OnClosed?.Invoke();
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

        public bool IsShowing => _isShowing;

        // =====================================================================
        //  Dropdown 채우기
        // =====================================================================

        private void PopulateDropdowns()
        {
            // 화면 모드
            if (displayModeDropdown != null)
            {
                displayModeDropdown.ClearOptions();
                displayModeDropdown.AddOptions(new List<string>
                {
                    DisplaySettings.ModeLabel(DisplayModeOption.Fullscreen),
                    DisplaySettings.ModeLabel(DisplayModeOption.Borderless),
                    DisplaySettings.ModeLabel(DisplayModeOption.Windowed),
                });
            }

            // 해상도
            _resolutions = DisplaySettings.GetResolutionOptions();
            if (resolutionDropdown != null)
            {
                resolutionDropdown.ClearOptions();
                var labels = new List<string>(_resolutions.Count);
                foreach (var r in _resolutions) labels.Add(DisplaySettings.ResolutionLabel(r));
                resolutionDropdown.AddOptions(labels);
            }

            // 화질
            if (qualityDropdown != null)
            {
                qualityDropdown.ClearOptions();
                qualityDropdown.AddOptions(new List<string>(QualitySettings.names));
            }

            // FPS
            if (fpsDropdown != null)
            {
                fpsDropdown.ClearOptions();
                var labels = new List<string>();
                foreach (var f in DisplaySettings.FpsOptions) labels.Add(DisplaySettings.FpsLabel(f));
                fpsDropdown.AddOptions(labels);
            }
        }

        private void FillUI(DisplaySettings.Data d)
        {
            if (displayModeDropdown != null) displayModeDropdown.SetValueWithoutNotify((int)d.mode);

            if (resolutionDropdown != null)
            {
                int idx = _resolutions.FindIndex(r => r.x == d.width && r.y == d.height);
                if (idx < 0) idx = 0;
                resolutionDropdown.SetValueWithoutNotify(idx);
            }

            if (qualityDropdown != null) qualityDropdown.SetValueWithoutNotify(Mathf.Clamp(d.qualityLevel, 0, qualityDropdown.options.Count - 1));
            if (vsyncToggle != null) vsyncToggle.SetIsOnWithoutNotify(d.vsync);

            if (fpsDropdown != null)
            {
                int idx = System.Array.IndexOf(DisplaySettings.FpsOptions, d.targetFps);
                if (idx < 0) idx = 1; // 60
                fpsDropdown.SetValueWithoutNotify(idx);
            }

            RefreshResolutionInteractable();
            RefreshFpsInteractable();
        }

        private DisplaySettings.Data ReadUI()
        {
            var d = _applied;
            if (displayModeDropdown != null) d.mode = (DisplayModeOption)displayModeDropdown.value;
            if (resolutionDropdown != null && _resolutions.Count > 0)
            {
                var r = _resolutions[Mathf.Clamp(resolutionDropdown.value, 0, _resolutions.Count - 1)];
                d.width = r.x; d.height = r.y;
            }
            if (qualityDropdown != null) d.qualityLevel = qualityDropdown.value;
            if (vsyncToggle != null) d.vsync = vsyncToggle.isOn;
            if (fpsDropdown != null) d.targetFps = DisplaySettings.FpsOptions[Mathf.Clamp(fpsDropdown.value, 0, DisplaySettings.FpsOptions.Length - 1)];
            return d;
        }

        private void RefreshResolutionInteractable()
        {
            // 테두리 없는 창은 모니터 해상도 그대로 쓰는 게 일반적 → 비활성 (원하면 지워도 됨)
            if (resolutionDropdown == null || displayModeDropdown == null) return;
            var mode = (DisplayModeOption)displayModeDropdown.value;
            resolutionDropdown.interactable = mode != DisplayModeOption.Borderless;
        }

        private void RefreshFpsInteractable()
        {
            if (fpsDropdown == null || vsyncToggle == null) return;
            fpsDropdown.interactable = !vsyncToggle.isOn;
        }

        // =====================================================================
        //  버튼
        // =====================================================================

        private void OnDefaults()
        {
            FillUI(DisplaySettings.Default());
        }

        private void OnApply()
        {
            var next = ReadUI();
            var prev = _applied;

            // 테두리 없는 창 → 모니터 해상도 강제
            if (next.mode == DisplayModeOption.Borderless)
            {
                var cur = Screen.currentResolution;
                next.width = cur.width; next.height = cur.height;
            }

            bool displayChanged = next.mode != prev.mode || next.width != prev.width || next.height != prev.height;

            DisplaySettings.Apply(next);

            if (displayChanged && confirmSeconds > 0f && confirmGroup != null)
            {
                _confirmRoutine = StartCoroutine(ConfirmRoutine(prev, next));
            }
            else
            {
                _applied = next;
                DisplaySettings.Save(next);
            }
        }

        private IEnumerator ConfirmRoutine(DisplaySettings.Data prev, DisplaySettings.Data next)
        {
            // 해상도 반영은 다음 프레임
            yield return null;

            confirmGroup.SetActive(true);
            _pendingPrev = prev;
            _pendingNext = next;

            float t = confirmSeconds;
            while (t > 0f)
            {
                if (confirmText != null)
                    confirmText.text = $"이 설정을 유지하시겠습니까?\n<size=80%>{Mathf.CeilToInt(t)}초 후 이전 설정으로 되돌아갑니다</size>";
                yield return null;
                t -= Time.unscaledDeltaTime;
            }

            _confirmRoutine = null;
            OnConfirmRevert();
        }

        private DisplaySettings.Data _pendingPrev, _pendingNext;

        private void OnConfirmKeep()
        {
            StopConfirm();
            if (confirmGroup != null) confirmGroup.SetActive(false);
            _applied = _pendingNext;
            DisplaySettings.Save(_pendingNext);
        }

        private void OnConfirmRevert()
        {
            StopConfirm();
            if (confirmGroup != null) confirmGroup.SetActive(false);
            DisplaySettings.Apply(_pendingPrev);
            _applied = _pendingPrev;
            FillUI(_pendingPrev);
        }

        private void StopConfirm()
        {
            if (_confirmRoutine != null)
            {
                StopCoroutine(_confirmRoutine);
                _confirmRoutine = null;
            }
        }

        // ESC로 닫기 (인게임에서 쓸 때 편의)
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
    }
}

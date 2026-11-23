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
    /// 설정 패널 — [화면] 탭: 화면 모드 / 해상도 / 화질 / VSync / FPS,  [소리] 탭: 전체·배경음·효과음 볼륨 / 음소거.
    ///
    /// CanvasGroup 패턴 (ModeSelectDialog와 동일): Scene에 활성 상태로 배치, Show()/Hide()로 토글.
    /// 로비의 ⚙ 버튼, 인게임 ⚙ 버튼 / ESC 등 어디서든 Show() 호출.
    ///
    /// 동작:
    ///   - 화면 탭: 열 때 저장값(없으면 현재값)으로 UI 채움
    ///       [적용]  → 적용 + 저장. 해상도/모드가 바뀌면 "유지하시겠습니까?" 10초 카운트다운 (안 누르면 되돌림)
    ///       [취소]  → 변경 폐기, 닫기
    ///   - 소리 탭: 슬라이더를 움직이면 바로 적용·저장
    ///   - [기본값] → 지금 탭의 설정을 기본값으로
    ///   - 나가기: 로비에서는 "게임 종료", 게임 중에는 "로비로 나가기" (온라인이면 내 자리는 봇이 이어받음)
    ///   - 항복: 게임 중에만 표시. 동작은 '로비로 나가기'와 같고 확인 문구만 다름
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

        [Header("탭")]
        [SerializeField] private Button displayTabButton;
        [SerializeField] private Button soundTabButton;
        [SerializeField] private GameObject displayPage;
        [SerializeField] private GameObject soundPage;
        [SerializeField] private Color tabActiveColor = new Color(0.85f, 0.71f, 0.42f);
        [SerializeField] private Color tabNormalColor = new Color(0.14f, 0.08f, 0.10f);
        [SerializeField] private Color tabActiveTextColor = new Color(0.1f, 0.05f, 0.03f);
        [SerializeField] private Color tabNormalTextColor = new Color(0.95f, 0.9f, 0.83f);

        [Header("소리")]
        [SerializeField] private Slider masterSlider;
        [SerializeField] private Slider bgmSlider;
        [SerializeField] private Slider sfxSlider;
        [SerializeField] private TMP_Text masterValueText;
        [SerializeField] private TMP_Text bgmValueText;
        [SerializeField] private TMP_Text sfxValueText;
        [SerializeField] private Toggle muteToggle;

        [Header("나가기 / 항복")]
        [SerializeField] private Button exitButton;
        [SerializeField] private TMP_Text exitButtonLabel;
        [SerializeField] private Button surrenderButton;
        [SerializeField] private string lobbyExitLabel = "게임 종료";
        [SerializeField] private string inGameExitLabel = "로비로 나가기";
        [SerializeField] private GameObject exitConfirmGroup;
        [SerializeField] private TMP_Text exitConfirmText;
        [SerializeField] private Button exitConfirmYesButton;
        [SerializeField] private TMP_Text exitConfirmYesLabel;
        [SerializeField] private Button exitConfirmNoButton;

        [Header("ESC")]
        [Tooltip("닫혀 있을 때 ESC로 열기 (인게임용). 다른 창이 ESC를 먼저 쓴 프레임에는 열지 않음.")]
        [SerializeField] private bool openWithEscape = false;

        [Header("Fade")]
        [SerializeField] private float fadeDuration = 0.2f;

        /// <summary>다른 창이 이번 프레임에 ESC를 썼다고 알림 → 설정창이 같은 ESC로 열리지 않게.</summary>
        public static void MarkEscapeConsumed() => _escapeConsumedFrame = Time.frameCount;
        private static int _escapeConsumedFrame = -1;

        public event System.Action OnClosed;

        private CanvasGroup _cg;
        private bool _isShowing;
        private List<Vector2Int> _resolutions = new List<Vector2Int>();
        private DisplaySettings.Data _applied;    // 마지막으로 확정된 설정
        private Coroutine _confirmRoutine;
        private int _tab;                // 0 = 화면, 1 = 소리
        private bool _inGame;
        private bool _pendingSurrender;
        private bool _exiting;
        private int _shownFrame = -1;
        private DiscardHistoryPanel _discardPanel;
        private ComboGuidePanel _comboPanel;

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

            Bind(displayTabButton, () => SetTab(0));
            Bind(soundTabButton, () => SetTab(1));
            Bind(exitButton, () => AskExit(false));
            Bind(surrenderButton, () => AskExit(true));
            Bind(exitConfirmYesButton, OnExitConfirmed);
            Bind(exitConfirmNoButton, CloseExitConfirm);

            BindSlider(masterSlider, v => GameAudioSettings.Master = v, masterValueText);
            BindSlider(bgmSlider, v => GameAudioSettings.Bgm = v, bgmValueText);
            BindSlider(sfxSlider, v => GameAudioSettings.Sfx = v, sfxValueText);
            if (muteToggle != null) muteToggle.onValueChanged.AddListener(v => GameAudioSettings.Muted = v);

            if (confirmGroup != null) confirmGroup.SetActive(false);
            if (exitConfirmGroup != null) exitConfirmGroup.SetActive(false);
        }

        private static void BindSlider(Slider s, System.Action<float> set, TMP_Text valueText)
        {
            if (s == null) return;
            s.minValue = 0f; s.maxValue = 1f; s.wholeNumbers = false;
            s.onValueChanged.AddListener(v =>
            {
                set(v);
                if (valueText != null) valueText.text = Percent(v);
            });
        }

        private static string Percent(float v) => $"{Mathf.RoundToInt(v * 100f)}%";

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

            _shownFrame = Time.frameCount;
            _exiting = false;

            _applied = DisplaySettings.HasSaved() ? DisplaySettings.Load() : DisplaySettings.Current();
            PopulateDropdowns();
            FillUI(_applied);
            FillSoundUI();

            // 로비 / 게임 중에 따라 나가기·항복 버튼
            _inGame = GameExit.IsInGame;
            if (exitButtonLabel != null) exitButtonLabel.text = _inGame ? inGameExitLabel : lobbyExitLabel;
            if (exitButton != null) exitButton.interactable = true;
            if (surrenderButton != null)
            {
                surrenderButton.gameObject.SetActive(_inGame);
                surrenderButton.interactable = true;
            }
            if (exitConfirmGroup != null) exitConfirmGroup.SetActive(false);

            SetTab(0);

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
            if (exitConfirmGroup != null) exitConfirmGroup.SetActive(false);

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
        //  탭
        // =====================================================================

        private void SetTab(int tab)
        {
            _tab = tab;
            if (displayPage != null) displayPage.SetActive(tab == 0);
            if (soundPage != null) soundPage.SetActive(tab == 1);

            // 적용/취소는 화면 탭에서만 (소리는 즉시 적용)
            if (applyButton != null) applyButton.gameObject.SetActive(tab == 0);
            if (cancelButton != null) cancelButton.gameObject.SetActive(tab == 0);

            PaintTab(displayTabButton, tab == 0);
            PaintTab(soundTabButton, tab == 1);
        }

        private void PaintTab(Button b, bool active)
        {
            if (b == null) return;
            if (b.targetGraphic != null) b.targetGraphic.color = active ? tabActiveColor : tabNormalColor;
            var label = b.GetComponentInChildren<TMP_Text>(true);
            if (label != null) label.color = active ? tabActiveTextColor : tabNormalTextColor;
        }

        // =====================================================================
        //  소리
        // =====================================================================

        private void FillSoundUI()
        {
            SetSlider(masterSlider, GameAudioSettings.Master, masterValueText);
            SetSlider(bgmSlider, GameAudioSettings.Bgm, bgmValueText);
            SetSlider(sfxSlider, GameAudioSettings.Sfx, sfxValueText);
            if (muteToggle != null) muteToggle.SetIsOnWithoutNotify(GameAudioSettings.Muted);
        }

        private static void SetSlider(Slider s, float v, TMP_Text valueText)
        {
            if (s != null) s.SetValueWithoutNotify(v);
            if (valueText != null) valueText.text = Percent(v);
        }

        // =====================================================================
        //  나가기 / 항복
        // =====================================================================

        private void AskExit(bool surrender)
        {
            _pendingSurrender = surrender;
            if (exitConfirmGroup == null) { OnExitConfirmed(); return; }

            string msg, yes;
            if (!_inGame)
            {
                msg = "게임을 종료하시겠습니까?";
                yes = "종료";
            }
            else
            {
                string note = GameExit.IsOnline
                    ? "<size=75%>진행 중인 게임에서 내 자리는 봇이 이어서 플레이합니다.</size>"
                    : "<size=75%>진행 중인 게임은 저장되지 않습니다.</size>";
                msg = (surrender ? "항복하고 로비로 나가시겠습니까?" : "로비로 나가시겠습니까?") + "\n" + note;
                yes = surrender ? "항복" : "나가기";
            }

            if (exitConfirmText != null) exitConfirmText.text = msg;
            if (exitConfirmYesLabel != null) exitConfirmYesLabel.text = yes;
            exitConfirmGroup.SetActive(true);
        }

        private void CloseExitConfirm()
        {
            if (exitConfirmGroup != null) exitConfirmGroup.SetActive(false);
        }

        private void OnExitConfirmed()
        {
            if (_exiting) return;
            _exiting = true;
            if (exitConfirmYesButton != null) exitConfirmYesButton.interactable = false;
            if (exitButton != null) exitButton.interactable = false;
            if (surrenderButton != null) surrenderButton.interactable = false;

            if (_inGame) GameExit.LeaveToLobby();
            else GameExit.QuitGame();
        }

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
            if (_tab == 1)
            {
                GameAudioSettings.ResetToDefaults();
                FillSoundUI();
                return;
            }
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

        // ESC: 열려 있으면 (확인창 → 닫기 순서로) 닫기, 닫혀 있으면 openWithEscape일 때 열기
        private void Update()
        {
            if (!EscapePressed()) return;

            if (_isShowing)
            {
                if (Time.frameCount == _shownFrame) return;
                if (exitConfirmGroup != null && exitConfirmGroup.activeSelf) { CloseExitConfirm(); return; }
                if (confirmGroup != null && confirmGroup.activeSelf) { OnConfirmRevert(); return; }
                Hide();
                return;
            }

            if (!openWithEscape || _escapeConsumedFrame == Time.frameCount) return;
            if (_discardPanel == null) _discardPanel = FindAnyObjectByType<DiscardHistoryPanel>();
            if (_discardPanel != null && _discardPanel.IsShowing) return;
            if (_comboPanel == null) _comboPanel = FindAnyObjectByType<ComboGuidePanel>();
            if (_comboPanel != null && _comboPanel.IsShowing) return;
            Show();
        }

        private static bool EscapePressed()
        {
#if ENABLE_INPUT_SYSTEM
            return UnityEngine.InputSystem.Keyboard.current != null &&
                   UnityEngine.InputSystem.Keyboard.current.escapeKey.wasPressedThisFrame;
#else
            return Input.GetKeyDown(KeyCode.Escape);
#endif
        }
    }
}

using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CardGame.UI
{
    /// <summary>
    /// 설정 다이얼로그 (볼륨, 그래픽 등). CommonHUD의 settings 버튼에서 호출.
    /// </summary>
    public class SettingsDialog : MonoBehaviour
    {
        [Header("Panel")]
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private RectTransform contentPanel;

        [Header("Controls")]
        [SerializeField] private Slider bgmSlider;
        [SerializeField] private Slider sfxSlider;
        [SerializeField] private Toggle fullscreenToggle;
        [SerializeField] private TMP_Dropdown qualityDropdown;

        [Header("Buttons")]
        [SerializeField] private Button closeButton;
        [SerializeField] private Button backToTitleButton;
        [SerializeField] private Button quitButton;

        private const string KEY_BGM = "settings.bgm";
        private const string KEY_SFX = "settings.sfx";
        private const string KEY_FULLSCREEN = "settings.fullscreen";
        private const string KEY_QUALITY = "settings.quality";

        private void Awake()
        {
            if (closeButton != null) closeButton.onClick.AddListener(Hide);
            if (backToTitleButton != null) backToTitleButton.onClick.AddListener(OnBackToTitle);
            if (quitButton != null) quitButton.onClick.AddListener(OnQuit);

            if (bgmSlider != null) bgmSlider.onValueChanged.AddListener(v => Save(KEY_BGM, v));
            if (sfxSlider != null) sfxSlider.onValueChanged.AddListener(v => Save(KEY_SFX, v));
            if (fullscreenToggle != null) fullscreenToggle.onValueChanged.AddListener(OnFullscreen);
            if (qualityDropdown != null) qualityDropdown.onValueChanged.AddListener(OnQuality);

            Load();
            if (canvasGroup != null) canvasGroup.alpha = 0f;
            gameObject.SetActive(false);
        }

        public void Show()
        {
            gameObject.SetActive(true);
            if (canvasGroup != null)
            {
                canvasGroup.DOKill();
                canvasGroup.DOFade(1f, 0.2f);
            }
            if (contentPanel != null)
            {
                contentPanel.DOKill();
                contentPanel.localScale = Vector3.one * 1.1f;
                contentPanel.DOScale(1f, 0.25f).SetEase(Ease.OutBack);
            }
        }

        public void Hide()
        {
            if (canvasGroup != null)
            {
                canvasGroup.DOKill();
                canvasGroup.DOFade(0f, 0.15f).OnComplete(() => gameObject.SetActive(false));
            }
            else gameObject.SetActive(false);
        }

        private void Load()
        {
            if (bgmSlider != null) bgmSlider.value = PlayerPrefs.GetFloat(KEY_BGM, 0.8f);
            if (sfxSlider != null) sfxSlider.value = PlayerPrefs.GetFloat(KEY_SFX, 1f);
            if (fullscreenToggle != null) fullscreenToggle.isOn = PlayerPrefs.GetInt(KEY_FULLSCREEN, 1) == 1;
            if (qualityDropdown != null) qualityDropdown.value = PlayerPrefs.GetInt(KEY_QUALITY, 2);
        }

        private void Save(string key, float value)
        {
            PlayerPrefs.SetFloat(key, value);
            PlayerPrefs.Save();
        }

        private void OnFullscreen(bool on)
        {
            Screen.fullScreen = on;
            PlayerPrefs.SetInt(KEY_FULLSCREEN, on ? 1 : 0);
            PlayerPrefs.Save();
        }

        private void OnQuality(int level)
        {
            QualitySettings.SetQualityLevel(level);
            PlayerPrefs.SetInt(KEY_QUALITY, level);
            PlayerPrefs.Save();
        }

        private void OnBackToTitle()
        {
            Hide();
            CardGame.Core.SceneLoader.Instance?.Load(CardGame.Core.GameScene.TitleScene);
        }

        private void OnQuit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}

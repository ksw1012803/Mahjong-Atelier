using UnityEngine;

namespace CardGame.Core
{
    /// <summary>
    /// 게임 시작 시 저장된 디스플레이·화질 설정을 적용.
    /// BootstrapScene의 아무 GameObject에 붙이면 됨 (GameBootstrap과 같은 오브젝트 OK).
    /// Awake에서 실행 → 첫 프레임 렌더 전에 해상도/모드 확정.
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public class SettingsBootstrap : MonoBehaviour
    {
        [Tooltip("에디터에서는 해상도/화면모드 변경을 건너뜀 (Game 뷰가 깨지는 것 방지). 화질·VSync는 적용.")]
        [SerializeField] private bool skipResolutionInEditor = true;

        private void Awake()
        {
            var d = DisplaySettings.Load();

#if UNITY_EDITOR
            if (skipResolutionInEditor)
            {
                QualitySettings.SetQualityLevel(d.qualityLevel, true);
                QualitySettings.vSyncCount = d.vsync ? 1 : 0;
                Application.targetFrameRate = d.vsync ? -1 : d.targetFps;
                Debug.Log($"[SettingsBootstrap] (Editor) 화질/VSync만 적용: {d}");
                return;
            }
#endif
            DisplaySettings.ApplySaved();
        }
    }
}

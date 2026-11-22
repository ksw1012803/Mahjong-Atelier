// SteamManager — Steamworks.NET 초기화 싱글톤.
// Steamworks.NET 공식 샘플(SteamManager.cs, MIT)과 동일한 API(SteamManager.Initialized)를 제공.
// .unitypackage 설치 시엔 Assets/Scripts/Steamworks.NET/SteamManager.cs 로 포함되지만,
// UPM(git URL) 설치 시엔 포함되지 않으므로 이 파일을 프로젝트에 추가.
//
// 배치: BootstrapScene(또는 첫 씬)의 빈 GameObject에 부착. DontDestroyOnLoad 자동.
// 필요: 프로젝트 루트(Assets 상위, .exe 옆)에 steam_appid.txt (앱 ID, 테스트는 480), Steam 클라이언트 실행 중.

#if !(UNITY_STANDALONE_WIN || UNITY_STANDALONE_LINUX || UNITY_STANDALONE_OSX || STEAMWORKS_WIN || STEAMWORKS_LIN_OSX)
#define DISABLESTEAMWORKS
#endif

using UnityEngine;
#if !DISABLESTEAMWORKS
using System.Collections;
using Steamworks;
#endif

[DisallowMultipleComponent]
public class SteamManager : MonoBehaviour
{
#if !DISABLESTEAMWORKS
    protected static bool s_EverInitialized = false;

    protected static SteamManager s_instance;
    protected static SteamManager Instance
    {
        get
        {
            if (s_instance == null)
                return new GameObject("SteamManager").AddComponent<SteamManager>();
            return s_instance;
        }
    }

    protected bool m_bInitialized = false;
    public static bool Initialized => Instance.m_bInitialized;

    protected SteamAPIWarningMessageHook_t m_SteamAPIWarningMessageHook;

    [AOT.MonoPInvokeCallback(typeof(SteamAPIWarningMessageHook_t))]
    protected static void SteamAPIDebugTextHook(int nSeverity, System.Text.StringBuilder pchDebugText)
    {
        Debug.LogWarning(pchDebugText);
    }

#if UNITY_2019_3_OR_NEWER
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void InitOnPlayMode()
    {
        s_EverInitialized = false;
        s_instance = null;
    }
#endif

    protected virtual void Awake()
    {
        if (s_instance != null)
        {
            Destroy(gameObject);
            return;
        }
        s_instance = this;

        if (s_EverInitialized)
        {
            // Steamworks.NET은 도메인 리로드 없이 재초기화 불가
            throw new System.Exception("Tried to Initialize the SteamAPI twice in one session!");
        }

        DontDestroyOnLoad(gameObject);

        if (!Packsize.Test())
            Debug.LogError("[Steamworks.NET] Packsize Test returned false, the wrong version of Steamworks.NET is being run in this platform.", this);

        if (!DllCheck.Test())
            Debug.LogError("[Steamworks.NET] DllCheck Test returned false, One or more of the Steamworks binaries seems to be the wrong version.", this);

        try
        {
            // 실행파일이 Steam 밖에서 실행됐으면 Steam을 통해 재실행 (steam_appid.txt가 있으면 false 반환)
            if (SteamAPI.RestartAppIfNecessary(AppId_t.Invalid))
            {
                Application.Quit();
                return;
            }
        }
        catch (System.DllNotFoundException e)
        {
            Debug.LogError("[Steamworks.NET] Could not load [lib]steam_api.dll/so/dylib. It's likely not in the correct location. Refer to the README for more details.\n" + e, this);
            Application.Quit();
            return;
        }

        m_bInitialized = SteamAPI.Init();
        if (!m_bInitialized)
        {
            Debug.LogError("[Steamworks.NET] SteamAPI_Init() failed. Refer to Valve's documentation or the comment above this line for more information.", this);
            return;
        }

        s_EverInitialized = true;
    }

    protected virtual void OnEnable()
    {
        if (s_instance == null) s_instance = this;
        if (!m_bInitialized) return;

        if (m_SteamAPIWarningMessageHook == null)
        {
            m_SteamAPIWarningMessageHook = new SteamAPIWarningMessageHook_t(SteamAPIDebugTextHook);
            SteamClient.SetWarningMessageHook(m_SteamAPIWarningMessageHook);
        }
    }

    protected virtual void OnDestroy()
    {
        if (s_instance != this) return;
        s_instance = null;
        if (!m_bInitialized) return;
        SteamAPI.Shutdown();
    }

    protected virtual void Update()
    {
        if (!m_bInitialized) return;
        SteamAPI.RunCallbacks();   // 콜백(로비 생성/입장 등) 실행 — 필수
    }
#else
    public static bool Initialized => false;
#endif
}

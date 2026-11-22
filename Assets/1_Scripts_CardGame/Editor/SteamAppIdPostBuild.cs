// Assets/Editor/SteamAppIdPostBuild.cs  (반드시 Editor 폴더 안에 둘 것)
// 빌드가 끝나면 프로젝트 루트의 steam_appid.txt를 .exe 옆으로 자동 복사.
#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEngine;

public static class SteamAppIdPostBuild
{
    [PostProcessBuild(1)]
    public static void OnPostprocessBuild(BuildTarget target, string pathToBuiltProject)
    {
        if (target != BuildTarget.StandaloneWindows && target != BuildTarget.StandaloneWindows64 &&
            target != BuildTarget.StandaloneLinux64 && target != BuildTarget.StandaloneOSX)
            return;

        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        string src = Path.Combine(projectRoot, "steam_appid.txt");
        if (!File.Exists(src))
        {
            Debug.LogWarning("[SteamAppIdPostBuild] 프로젝트 루트에 steam_appid.txt가 없습니다.");
            return;
        }

        // Windows/Linux: pathToBuiltProject = .../Game.exe → 그 폴더. macOS: .app 옆.
        string buildDir = Path.GetDirectoryName(pathToBuiltProject);
        string dst = Path.Combine(buildDir, "steam_appid.txt");
        File.Copy(src, dst, true);
        Debug.Log($"[SteamAppIdPostBuild] steam_appid.txt 복사 → {dst}");
    }
}
#endif

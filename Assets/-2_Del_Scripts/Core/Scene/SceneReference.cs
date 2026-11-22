using System;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace MahjongAtelier.Core
{
    /// <summary>
    /// Scene 참조 — Inspector에서 SceneAsset 드래그로 설정 가능.
    /// 빌드 시점에 자동으로 Scene 이름 문자열로 변환.
    /// 
    /// 사용:
    ///   [SerializeField] private SceneReference targetScene;
    ///   ...
    ///   SceneManager.LoadScene(targetScene.SceneName);
    /// 
    /// Inspector에서 .unity 파일을 직접 드래그할 수 있어 Scene 이름 오타 방지.
    /// </summary>
    [Serializable]
    public class SceneReference
    {
#if UNITY_EDITOR
        [SerializeField] private SceneAsset sceneAsset;
#endif

        // 실제 사용되는 Scene 이름 (빌드에 포함되는 값)
        [SerializeField, HideInInspector] private string sceneName;

        /// <summary>로드할 Scene 이름.</summary>
        public string SceneName
        {
            get
            {
#if UNITY_EDITOR
                // 에디터에서는 SceneAsset이 최신, 빌드에서는 직렬화된 문자열 사용
                if (sceneAsset != null) return sceneAsset.name;
#endif
                return sceneName;
            }
        }

        public bool IsValid => !string.IsNullOrEmpty(SceneName);

#if UNITY_EDITOR
        /// <summary>에디터에서 SceneAsset이 바뀔 때 문자열 동기화.</summary>
        public void OnBeforeSerialize()
        {
            if (sceneAsset != null) sceneName = sceneAsset.name;
        }
#endif
    }
}

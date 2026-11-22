using UnityEngine;
using CardGame.Core;

namespace CardGame
{
    /// <summary>
    /// Game 씬 시작 시 SceneContext에서 선택된 모드를 가져와 CardGameManager에 적용.
    /// Game 씬에 1개 배치 (CardGameManager 옆에).
    /// </summary>
    public class GameSceneBootstrap : MonoBehaviour
    {
        [SerializeField] private CardGameManager gameManager;
        [SerializeField] private ModeDefinition fallbackMode;

        private void Awake()
        {
            if (gameManager == null) gameManager = FindObjectOfType<CardGameManager>();

            var mode = SceneContext.Get<ModeDefinition>("selectedMode", fallbackMode);
            if (mode == null)
            {
                Debug.LogWarning("[GameSceneBootstrap] 모드 정의 없음, 기본 설정 사용");
                return;
            }

            Debug.Log($"[GameSceneBootstrap] 모드 적용: {mode.displayName}");
            ApplyMode(mode);
        }

        private void ApplyMode(ModeDefinition mode)
        {
            if (gameManager == null) return;

            // CardGameManager가 ApplyMode(ModeDefinition) 메서드 제공해야 함
            // 또는 reflection/public setter로 설정
            // 예: gameManager.ApplyMode(mode);
        }
    }
}

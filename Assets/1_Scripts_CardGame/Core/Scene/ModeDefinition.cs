using UnityEngine;

namespace CardGame.Core
{
    /// <summary>
    /// 플레이 모드 정의 (SO). 에디터에서 밸런싱 조정.
    /// 각 모드마다 1 에셋 생성.
    /// </summary>
    [CreateAssetMenu(menuName = "SevenMaid/Play Mode", fileName = "Mode_")]
    public class ModeDefinition : ScriptableObject
    {
        [Header("Identity")]
        public PlayMode mode;
        public string displayName;
        [TextArea(2, 4)] public string description;
        public Sprite icon;
        public Color themeColor = Color.white;

        [Header("Rules")]
        public int winningScore = 30;
        public int startingHandSize = 6;
        public int completionHandSize = 7;
        [Range(2, 4)] public int playerCount = 4;
        public bool allowSpecialCards = true;

        [Header("Bot Difficulty")]
        public BotStrategy defaultBotStrategy;

        [Header("Modifiers (Challenge 모드 등)")]
        public bool blindMode = false;        // 손패 뒷면만 보임
        public bool noWildCards = false;      // Wild 카드 금지
    }
}

namespace MahjongAtelier.Core
{
    /// <summary>
    /// 플레이어 타입. 봇과 사람을 구분.
    /// 
    /// Human: 사용자 입력 대기 (UI 클릭, 버튼 등)
    /// Bot: 자동 동작 (현재는 쯔모한 패를 그대로 버리는 단순 동작)
    /// 
    /// 추후 BotEasy/BotNormal/BotHard 같이 세분화 가능.
    /// </summary>
    public enum PlayerType
    {
        Human,
        Bot
    }
}

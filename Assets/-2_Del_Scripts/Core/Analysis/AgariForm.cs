namespace MahjongAtelier.Core
{
    /// <summary>
    /// 화료 형태 분류. 점수 계산 시 어떤 형으로 화료했는지가 중요합니다.
    /// </summary>
    public enum AgariForm
    {
        None,           // 화료 불가
        Standard,       // 표준형: 4면자 + 1머리
        Chiitoitsu,     // 칠대자(七対子): 7쌍
        Kokushi         // 국사무쌍(国士無双): 1·9·자패 13종 + 1장 중복
    }
}

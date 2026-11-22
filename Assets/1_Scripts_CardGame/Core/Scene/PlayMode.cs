namespace CardGame.Core
{
    /// <summary>플레이 모드 종류.</summary>
    public enum PlayMode
    {
        Standard,   // 모드 1: 기본 (30점 선취)
        Speed,      // 모드 2: 빠른 플레이 (15점 선취)
        Challenge,  // 모드 3: 챌린지 (특수 조건)
    }
}

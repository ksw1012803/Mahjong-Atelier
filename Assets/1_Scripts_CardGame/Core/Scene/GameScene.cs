namespace CardGame.Core
{
    /// <summary>
    /// 전체 게임 씬 enum. SceneManager 하드코딩 방지.
    /// Build Settings에 추가된 씬 이름과 **정확히** 일치해야 함.
    /// </summary>
    public enum GameScene
    {
        BootstrapScene,
        TitleScene,
        LobbyScene,
        LoadScene,
        SampleScene,
        CardGameScene,
    }
}

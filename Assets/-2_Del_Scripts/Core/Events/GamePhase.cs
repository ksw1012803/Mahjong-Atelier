namespace MahjongAtelier.Core
{
    /// <summary>
    /// 마작 게임의 현재 단계(국면).
    /// State Machine으로 "지금 누가 뭘 할 수 있는지"를 명시적으로 관리합니다.
    /// 
    /// 흐름:
    ///   NotStarted → Dealing → WaitingDraw → WaitingDiscard
    ///                                            ↓ (버림)
    ///   다른 플레이어 → WaitingCallResponse (치/펑/깡/론 가능 시)
    ///                                            ↓ (응답 또는 시간 초과)
    ///   다음 차례 → WaitingDraw → ... → HandFinished → RoundFinished
    /// </summary>
    public enum GamePhase
    {
        /// <summary>게임 시작 전.</summary>
        NotStarted,

        /// <summary>배패 진행 중.</summary>
        Dealing,

        /// <summary>현재 플레이어의 쯔모 대기 (시스템 처리).</summary>
        WaitingDraw,

        /// <summary>현재 플레이어가 버릴 패를 결정해야 함.</summary>
        WaitingDiscard,

        /// <summary>
        /// 다른 플레이어들이 버려진 패에 대해 치/펑/깡/론을 선언할지 결정.
        /// 다중 응답 가능성 때문에 별도 단계로 관리.
        /// </summary>
        WaitingCallResponse,

        /// <summary>한 판(국) 종료. 점수 계산 및 결과 표시.</summary>
        HandFinished,

        /// <summary>전체 대국 종료 (동풍전/반장전 모두).</summary>
        RoundFinished
    }
}

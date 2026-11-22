using System.Collections.Generic;

namespace CardGame.Core
{
    /// <summary>CardGameManager 실행 모드.</summary>
    public enum GameRunMode
    {
        /// <summary>싱글플레이 — 기존과 100% 동일 동작.</summary>
        Local,
        /// <summary>온라인 호스트 — 게임 로직 권위. 봇 + 원격 플레이어 입력 대기.</summary>
        NetworkHost,
        /// <summary>온라인 클라이언트 — 로직 없음. 호스트가 보낸 상태를 복제해 UI만 구동.</summary>
        NetworkClient,
    }

    /// <summary>한 자리(좌석)의 구성.</summary>
    public sealed class SeatConfig
    {
        public int Seat;
        public PlayerType Type;
        public string DisplayName;
        public ulong SteamId;

        public override string ToString() => $"Seat{Seat}:{Type}:{DisplayName}";
    }

    /// <summary>
    /// 온라인 세션 설정 — NetworkGameSession이 만들어 CardGameManager.ConfigureNetwork()에 전달.
    /// 호스트/클라이언트 모두 같은 좌석 배치를 공유하되, Type만 관점에 따라 다름:
    ///   호스트 관점: 자기 자리 = Human, 원격 = RemoteHuman, 나머지 = Bot
    ///   클라이언트 관점: 자기 자리 = Human, 나머지 사람 = RemoteHuman, 봇 = Bot
    /// </summary>
    public sealed class GameSessionConfig
    {
        public List<SeatConfig> Seats = new List<SeatConfig>();
        /// <summary>이 기기의 좌석 번호 (= CardGameManager.UserPlayerIndex).</summary>
        public int LocalSeat;
        /// <summary>호스트 RNG 시드 (클라이언트는 사용 안 함).</summary>
        public int Seed;
        /// <summary>선택된 게임 모드 인덱스 (ModeDefinition 배열 기준).</summary>
        public int ModeIndex;

        public int SeatCount => Seats.Count;
    }

    /// <summary>
    /// 호스트 이전(마이그레이션) 시 이어받는 진행 상황.
    /// 클라이언트는 남의 손패·덱을 모르므로 "점수·라운드 번호·선·좌석 배치"만 유지하고 현재 라운드를 다시 시작한다.
    /// </summary>
    public sealed class GameResumeData
    {
        /// <summary>다시 시작할 라운드 번호 (1부터).</summary>
        public int Round;
        public int StartingSeat;
        public int[] Scores;
        public int ModeIndex;
        /// <summary>좌석별 구성 (SteamId로 원래 자리 복구).</summary>
        public List<SeatConfig> Seats = new List<SeatConfig>();
    }

    /// <summary>SceneContext 키 모음 (문자열 오타 방지).</summary>
    public static class SceneContextKeys
    {
        /// <summary>bool — true면 CardGameScene의 CardGameManager가 자동 시작하지 않고 NetworkGameSession을 기다림.</summary>
        public const string OnlineSession = "onlineSession";
        /// <summary>ModeDefinition — 선택된 모드 애셋.</summary>
        public const string SelectedMode = "selectedMode";
        /// <summary>int — 선택된 모드 인덱스.</summary>
        public const string SelectedModeIndex = "selectedModeIndex";
    }
}

namespace MahjongAtelier.Core
{
    /// <summary>
    /// 게임 모드 (반장전/동풍전).
    /// </summary>
    public enum MatchMode
    {
        /// <summary>동풍전: 4국 (동 1~4국)</summary>
        Tonpuusen = 4,

        /// <summary>반장전: 8국 (동 1~4국 + 남 1~4국)</summary>
        Hanchan = 8
    }

    /// <summary>
    /// 게임 전체 진행 상태.
    /// 
    /// 한 게임 = N국. N국이 끝나면 게임 종료.
    /// 
    /// 국 번호 (0-indexed):
    ///   동풍전 (4국): 0~3 (東1, 東2, 東3, 東4)
    ///   반장전 (8국): 0~7 (東1~東4, 南1~南4)
    /// </summary>
    public sealed class GameMatchState
    {
        public MatchMode Mode { get; }

        /// <summary>현재 국 인덱스 (0부터). 친 연장 시에도 증가.</summary>
        public int HandIndex { get; set; }

        /// <summary>총 국 수.</summary>
        public int TotalHands => (int)Mode;

        /// <summary>현재 장풍.</summary>
        public SeatWind CurrentRoundWind
        {
            get
            {
                // 동풍전: 항상 동
                // 반장전: 0~3 = 동, 4~7 = 남
                if (Mode == MatchMode.Tonpuusen) return SeatWind.East;
                return HandIndex < 4 ? SeatWind.East : SeatWind.South;
            }
        }

        /// <summary>현재 국 내에서의 순번 (1~4). 동1국이면 1, 동2국이면 2.</summary>
        public int RoundNumber => (HandIndex % 4) + 1;

        /// <summary>게임 종료 여부.</summary>
        public bool IsGameOver => HandIndex >= TotalHands;

        public GameMatchState(MatchMode mode)
        {
            Mode = mode;
            HandIndex = 0;
        }

        /// <summary>다음 국으로 진행 (친 이동 시).</summary>
        public void AdvanceHand()
        {
            HandIndex++;
        }

        /// <summary>장풍과 국 번호로 표시 문자열 생성.</summary>
        public string GetHandLabel()
        {
            string wind = CurrentRoundWind == SeatWind.East ? "東" : "南";
            return $"{wind}{RoundNumber}局";
        }

        public void Reset()
        {
            HandIndex = 0;
        }
    }
}

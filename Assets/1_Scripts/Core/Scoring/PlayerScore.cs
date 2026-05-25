namespace MahjongAtelier.Core
{
    /// <summary>
    /// 플레이어의 점수 + 통계.
    /// MahjongPlayer에 추가하지 않고 별도 클래스로 두는 이유:
    ///   - MahjongPlayer는 "이 판"의 손패 상태에 집중
    ///   - 점수는 판이 끝나도 누적되는 "대국 상태"
    /// </summary>
    public sealed class PlayerScore
    {
        public int PlayerIndex { get; }

        /// <summary>현재 점수.</summary>
        public int Points { get; private set; }

        /// <summary>승리 횟수 (통계).</summary>
        public int WinCount { get; private set; }

        /// <summary>방총 횟수 (통계).</summary>
        public int DealInCount { get; private set; }

        public PlayerScore(int playerIndex, int startingPoints = 25000)
        {
            PlayerIndex = playerIndex;
            Points = startingPoints;
        }

        public void AddPoints(int delta)
        {
            Points += delta;
        }

        public void RecordWin() => WinCount++;
        public void RecordDealIn() => DealInCount++;

        public override string ToString() => $"P{PlayerIndex}: {Points}점 (승리 {WinCount})";
    }
}

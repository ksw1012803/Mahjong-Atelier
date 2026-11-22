using System.Collections.Generic;

namespace MahjongAtelier.Core
{
    /// <summary>
    /// 게임 최종 결과. 순위 + 점수.
    /// </summary>
    public sealed class FinalResult
    {
        /// <summary>플레이어 결과 (순위 1등부터 정렬됨).</summary>
        public List<PlayerEntry> Rankings { get; } = new List<PlayerEntry>();

        /// <summary>몇 국 진행했는지.</summary>
        public int HandsPlayed { get; set; }

        /// <summary>강제 종료된 것인지.</summary>
        public bool WasForceEnded { get; set; }

        public sealed class PlayerEntry
        {
            public int PlayerIndex;
            public int Rank;          // 1, 2, 3, 4
            public int FinalPoints;
            public SeatWind FinalSeat;
            public PlayerType Type;

            public override string ToString()
            {
                return $"{Rank}등: P{PlayerIndex}({FinalSeat}) {FinalPoints:N0}점";
            }
        }

        /// <summary>플레이어들과 점수로부터 결과 생성. 점수 내림차순 정렬.</summary>
        public static FinalResult Build(
            IReadOnlyList<MahjongPlayer> players,
            IReadOnlyList<PlayerScore> scores,
            int handsPlayed,
            bool wasForceEnded)
        {
            var result = new FinalResult
            {
                HandsPlayed = handsPlayed,
                WasForceEnded = wasForceEnded
            };

            // 점수 내림차순 정렬용 임시 리스트
            var entries = new List<PlayerEntry>();
            for (int i = 0; i < players.Count && i < scores.Count; i++)
            {
                entries.Add(new PlayerEntry
                {
                    PlayerIndex = i,
                    FinalPoints = scores[i].Points,
                    FinalSeat = players[i].Seat,
                    Type = players[i].Type
                });
            }

            // 점수 내림차순. 같은 점수면 인덱스 순서.
            entries.Sort((a, b) =>
            {
                int cmp = b.FinalPoints.CompareTo(a.FinalPoints);
                if (cmp != 0) return cmp;
                return a.PlayerIndex.CompareTo(b.PlayerIndex);
            });

            for (int i = 0; i < entries.Count; i++)
            {
                entries[i].Rank = i + 1;
                result.Rankings.Add(entries[i]);
            }

            return result;
        }
    }
}

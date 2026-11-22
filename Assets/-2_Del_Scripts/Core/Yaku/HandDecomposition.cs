using System.Collections.Generic;

namespace MahjongAtelier.Core
{
    /// <summary>
    /// 화료 손패의 면자 분해 결과.
    /// 
    /// 한 손패가 여러 분해 형태를 가질 수 있음. 예:
    ///   11233334m → (123m)(234m)(머리=4m)(머리는 따로 셈)... 등 여러 분해
    /// 
    /// 각 분해마다 핀후 여부, 일배구 여부 등이 다를 수 있으므로
    /// 모든 분해를 검사하고 점수가 가장 높은 것을 선택해야 합니다.
    /// 
    /// AgariForm:
    ///   - Standard: Melds(4개) + Pair(1개)
    ///   - Chiitoitsu: Melds 비어있고 PairsForChiitoitsu에 7개
    ///   - Kokushi: 둘 다 사용 안함 (별도 검사)
    /// </summary>
    public sealed class HandDecomposition
    {
        public AgariForm Form { get; }

        /// <summary>표준형: 4개의 면자 (슌츠/코츠/깡즈).</summary>
        public IReadOnlyList<Meld> Melds { get; }

        /// <summary>표준형: 머리 (대자).</summary>
        public Meld Pair { get; }

        /// <summary>칠대자: 7개의 쌍.</summary>
        public IReadOnlyList<Meld> PairsForChiitoitsu { get; }

        /// <summary>화료 시 마지막 패의 종류. 대기 형태 판정에 필수.</summary>
        public TileKind WinningTile { get; }

        /// <summary>화료 시 마지막 패가 들어간 면자의 인덱스 (Melds 또는 PairsForChiitoitsu). -1이면 머리에 들어감.</summary>
        public int WinningMeldIndex { get; }

        /// <summary>화료 시 머리에 마지막 패가 들어갔는지 (탕키대기).</summary>
        public bool WinningIsPair { get; }

        /// <summary>표준형 생성자.</summary>
        public HandDecomposition(
            IReadOnlyList<Meld> melds,
            Meld pair,
            TileKind winningTile,
            int winningMeldIndex,
            bool winningIsPair)
        {
            Form = AgariForm.Standard;
            Melds = melds;
            Pair = pair;
            PairsForChiitoitsu = null;
            WinningTile = winningTile;
            WinningMeldIndex = winningMeldIndex;
            WinningIsPair = winningIsPair;
        }

        /// <summary>칠대자 생성자.</summary>
        public HandDecomposition(IReadOnlyList<Meld> pairs, TileKind winningTile)
        {
            Form = AgariForm.Chiitoitsu;
            Melds = null;
            PairsForChiitoitsu = pairs;
            WinningTile = winningTile;
            WinningIsPair = true; // 칠대자는 항상 단기와 비슷한 머리 대기
        }

        /// <summary>국사무쌍 생성자 (특수, 분해 자체는 의미 없음).</summary>
        public static HandDecomposition Kokushi(TileKind winningTile)
        {
            return new HandDecomposition(winningTile);
        }

        private HandDecomposition(TileKind winningTile)
        {
            Form = AgariForm.Kokushi;
            WinningTile = winningTile;
        }
    }
}

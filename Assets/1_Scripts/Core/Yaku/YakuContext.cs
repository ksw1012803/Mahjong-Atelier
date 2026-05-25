using System.Collections.Generic;

namespace MahjongAtelier.Core
{
    /// <summary>
    /// 역 판정에 필요한 모든 정보.
    /// 
    /// v3 변경점:
    ///   - IsTenhou (천화), IsChiihou (지화) 추가
    /// </summary>
    public sealed class YakuContext
    {
        // === 손패 ===

        public TileCounter Hand { get; set; }
        public TileKind WinningTile { get; set; }
        public List<Meld> CalledMelds { get; set; } = new List<Meld>();

        public bool IsMenzen
        {
            get
            {
                if (CalledMelds == null || CalledMelds.Count == 0) return true;
                foreach (var m in CalledMelds)
                {
                    if (!(m.IsQuad && m.IsConcealed)) return false;
                }
                return true;
            }
        }

        // === 화료 정보 ===

        public bool IsTsumo { get; set; }
        public bool IsRiichi { get; set; }
        public bool IsDoubleRiichi { get; set; }
        public bool IsIppatsu { get; set; }
        public bool IsHaitei { get; set; }
        public bool IsHoutei { get; set; }
        public bool IsRinshan { get; set; }
        public bool IsChankan { get; set; }

        /// <summary>천화 (天和): 친의 배패 즉시 화료.</summary>
        public bool IsTenhou { get; set; }

        /// <summary>지화 (地和): 자의 첫 쯔모 화료 (그동안 후로 발생 없어야).</summary>
        public bool IsChiihou { get; set; }

        // === 좌석/장 ===

        public TileKind SeatWind { get; set; } = TileKind.East;
        public TileKind RoundWind { get; set; } = TileKind.East;

        // === 도라 ===

        public List<TileKind> DoraIndicators { get; set; } = new List<TileKind>();
        public List<TileKind> UraDoraIndicators { get; set; } = new List<TileKind>();
        public int AkaDoraCount { get; set; }
    }
}

using System.Collections.Generic;

namespace MahjongAtelier.Core
{
    /// <summary>
    /// 후로(鳴き) 면자. Meld의 확장 — 누구에게서 가져왔는지 등 추가 정보.
    /// 
    /// 시각적 표시:
    ///   - 가져온 패는 가로(회전)로 두고, 가져온 방향에 따라 위치 다름
    ///   - 치: 카미챠(왼쪽)에서만 가능 → 왼쪽 패 가로
    ///   - 펑: 누구한테서든 가능 → 그 사람 방향 패 가로
    ///   - 명깡: 펑과 동일
    ///   - 안깡: 양쪽 끝 패만 뒷면(가린) 표시
    ///   - 가깡: 펑한 코츠에 같은 패 1장 위로 올림
    /// </summary>
    public sealed class CalledMeld
    {
        /// <summary>면자 종류.</summary>
        public MeldType Type { get; }

        /// <summary>면자의 시작 패 (슌츠는 가장 작은 수, 코츠/깡즈는 그 패).</summary>
        public TileKind BaseKind { get; }

        /// <summary>면자에 포함된 모든 패의 TileData. 시각적 표시 + 아카도라 추적용.</summary>
        public List<TileData> Tiles { get; }

        /// <summary>가져온 패의 TileData (Tiles 중 하나, 안깡은 null).</summary>
        public TileData CalledTile { get; }

        /// <summary>패를 가져온 상대 인덱스 (안깡은 -1).</summary>
        public int CalledFromPlayerIndex { get; }

        /// <summary>후로 종류.</summary>
        public CallType CallType { get; }

        /// <summary>안깡 여부. true면 멘젠 유지에 영향 없음.</summary>
        public bool IsConcealed => CallType == CallType.Ankan;

        /// <summary>깡즈 여부.</summary>
        public bool IsKan => Type == MeldType.Quad;

        public CalledMeld(
            MeldType type, TileKind baseKind,
            List<TileData> tiles,
            TileData calledTile,
            int calledFromPlayerIndex,
            CallType callType)
        {
            Type = type;
            BaseKind = baseKind;
            Tiles = tiles ?? new List<TileData>();
            CalledTile = calledTile;
            CalledFromPlayerIndex = calledFromPlayerIndex;
            CallType = callType;
        }

        /// <summary>면자에 특정 패가 포함되는지.</summary>
        public bool Contains(TileKind kind)
        {
            switch (Type)
            {
                case MeldType.Sequence:
                    return kind.Suit == BaseKind.Suit &&
                           kind.Number >= BaseKind.Number &&
                           kind.Number <= BaseKind.Number + 2;
                case MeldType.Triplet:
                case MeldType.Quad:
                    return kind == BaseKind;
                default: return false;
            }
        }

        /// <summary>아카도라 개수.</summary>
        public int CountAkaDora()
        {
            int count = 0;
            foreach (var t in Tiles)
                if (t != null && t.IsRedFive) count++;
            return count;
        }

        /// <summary>YakuChecker 호환을 위한 Meld 변환.</summary>
        public Meld ToMeld()
        {
            // 호출 → 명각/명깡 / 안깡 → 안깡
            bool isConcealed = CallType == CallType.Ankan;
            bool isCalled = CallType != CallType.Ankan;
            return new Meld(Type, BaseKind, isConcealed: isConcealed, isCalled: isCalled);
        }

        public override string ToString()
        {
            string typeStr = CallType switch
            {
                CallType.Chi => "치",
                CallType.Pon => "펑",
                CallType.Daiminkan => "명깡",
                CallType.Ankan => "안깡",
                CallType.Shouminkan => "가깡",
                _ => "?"
            };

            string baseStr = Type == MeldType.Sequence
                ? $"{BaseKind}~"
                : $"{BaseKind}";
            return $"{typeStr}({baseStr})";
        }
    }

    /// <summary>후로의 구체 종류.</summary>
    public enum CallType
    {
        Chi,           // 치
        Pon,           // 펑
        Daiminkan,     // 대명깡 (다른 사람 버림패)
        Ankan,         // 안깡 (자기 4장)
        Shouminkan     // 소명깡/가깡 (펑한 코츠에 추가)
    }
}

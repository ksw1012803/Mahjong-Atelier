using System.Collections.Generic;
using System.Linq;

namespace MahjongAtelier.Core
{
    /// <summary>
    /// 손패 정렬 유틸리티.
    /// 일본 마작 표준: 만→통→삭→풍→삼원, 각 그룹 내에서는 숫자 오름차순.
    /// 아카도라는 일반 5와 같은 위치에 두되, 같은 종류 내에서 IsRedFive=true가 먼저 오도록.
    /// (시각적으로 빨간 5를 그룹 좌측에 두는 게 일반적입니다)
    /// </summary>
    public static class HandSorter
    {
        /// <summary>새 리스트를 반환 (불변).</summary>
        public static List<TileData> Sorted(IEnumerable<TileData> tiles)
        {
            return tiles
                .OrderBy(t => t.Kind.SortOrder)
                .ThenByDescending(t => t.IsRedFive) // 빨간 5가 같은 5 중에서 먼저
                .ThenBy(t => t.Id)                  // 안정 정렬 보조
                .ToList();
        }

        /// <summary>주어진 리스트를 in-place 정렬.</summary>
        public static void SortInPlace(List<TileData> tiles)
        {
            tiles.Sort((a, b) =>
            {
                int cmp = a.Kind.SortOrder.CompareTo(b.Kind.SortOrder);
                if (cmp != 0) return cmp;
                cmp = b.IsRedFive.CompareTo(a.IsRedFive); // red 먼저
                if (cmp != 0) return cmp;
                return a.Id.CompareTo(b.Id);
            });
        }
    }
}

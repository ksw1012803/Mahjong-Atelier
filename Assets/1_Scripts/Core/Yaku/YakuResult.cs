using System.Collections.Generic;
using System.Text;

namespace MahjongAtelier.Core
{
    /// <summary>
    /// 역 판정 결과. 한 번의 화료에 대한 모든 성립 역과 총 판수.
    /// </summary>
    public sealed class YakuResult
    {
        /// <summary>성립한 역들. (역, 판수) 쌍.</summary>
        public List<(IYaku yaku, int han)> Yakus { get; } = new List<(IYaku, int)>();

        /// <summary>도라 판수 (성립 역과는 별개, 보너스).</summary>
        public int DoraHan { get; set; }

        /// <summary>역만 판수 (역만 1배 = 1, 더블 역만 = 2 등). 일반 역과 동시 성립 불가.</summary>
        public int YakumanCount { get; set; }

        /// <summary>이 결과로 사용된 분해 (점수 계산 시 부수 산정에 필요).</summary>
        public HandDecomposition Decomposition { get; set; }

        /// <summary>해당 분해의 대기 형태.</summary>
        public WaitType WaitType { get; set; }

        /// <summary>역 또는 역만이 하나라도 성립?</summary>
        public bool HasAnyYaku => Yakus.Count > 0 || YakumanCount > 0;

        /// <summary>일반 역의 총 판수 (도라 제외).</summary>
        public int TotalYakuHan
        {
            get
            {
                int sum = 0;
                foreach (var (_, han) in Yakus) sum += han;
                return sum;
            }
        }

        /// <summary>도라 포함 총 판수 (역만 제외).</summary>
        public int TotalHanIncludingDora => TotalYakuHan + DoraHan;

        public override string ToString()
        {
            if (!HasAnyYaku) return "역 없음";

            var sb = new StringBuilder();
            if (YakumanCount > 0)
            {
                sb.Append($"역만 ×{YakumanCount}: ");
                for (int i = 0; i < Yakus.Count; i++)
                {
                    if (i > 0) sb.Append(", ");
                    sb.Append(Yakus[i].yaku.NameKr);
                }
            }
            else
            {
                for (int i = 0; i < Yakus.Count; i++)
                {
                    if (i > 0) sb.Append(", ");
                    sb.Append($"{Yakus[i].yaku.NameKr}({Yakus[i].han}판)");
                }
                if (DoraHan > 0) sb.Append($", 도라({DoraHan}판)");
                sb.Append($" = 총 {TotalHanIncludingDora}판");
            }
            return sb.ToString();
        }
    }
}

using System.Text;

namespace MahjongAtelier.Core
{
    /// <summary>
    /// 점수 등급. 부수+판수 조합에 따른 상한 분류.
    /// </summary>
    public enum ScoreClass
    {
        /// <summary>일반 (부수×판수로 직접 계산).</summary>
        Normal,

        /// <summary>만관 (満貫): 기본점 2000.</summary>
        Mangan,

        /// <summary>하네만 (跳満): 기본점 3000.</summary>
        Haneman,

        /// <summary>배만 (倍満): 기본점 4000.</summary>
        Baiman,

        /// <summary>삼배만 (三倍満): 기본점 6000.</summary>
        Sanbaiman,

        /// <summary>역만 (役満): 기본점 8000.</summary>
        Yakuman
    }

    /// <summary>
    /// 점수 계산 결과.
    /// 
    /// 마작 점수는 단순한 숫자가 아니라:
    ///   - 화료자가 받는 총액
    ///   - 누가 누구에게 얼마를 주는지의 분배 정보
    /// 두 가지가 모두 필요합니다.
    /// 
    /// 쯔모 화료: 친(親)은 모두에게 동일 지불, 자(子)는 친과 자가 다르게 지불.
    /// 론 화료: 쏜 사람(放銃者)만 지불.
    /// </summary>
    public sealed class ScoreResult
    {
        // === 입력 요약 ===

        public int Fu { get; set; }              // 최종 부수 (올림 후)
        public int Han { get; set; }             // 도라 포함 총 판수
        public int YakumanCount { get; set; }    // 역만 배수 (0이면 일반)
        public ScoreClass Class { get; set; }
        public bool IsDealer { get; set; }       // 화료자가 친?
        public bool IsTsumo { get; set; }

        // === 계산 결과 ===

        /// <summary>기본점 (base point). 한 사람 지불 단위 산정의 기준.</summary>
        public int BasePoint { get; set; }

        /// <summary>화료자가 받는 총 점수 (적립 변동분, 공탁 제외).</summary>
        public int TotalGain { get; set; }

        // === 지불 분배 ===

        /// <summary>론 화료 시 쏜 사람이 지불할 금액. 쯔모이면 0.</summary>
        public int RonPayment { get; set; }

        /// <summary>쯔모 시 친이 지불할 금액 (화료자가 친이면 0).</summary>
        public int TsumoPaymentFromDealer { get; set; }

        /// <summary>쯔모 시 자(子)가 지불할 금액 (화료자가 친이면 모두 동일, 자면 다른 자들이 이만큼 지불).</summary>
        public int TsumoPaymentFromNonDealer { get; set; }

        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.Append($"[{Class}] ");

            if (YakumanCount > 0)
            {
                sb.Append(YakumanCount == 1 ? "역만 " : $"{YakumanCount}배역만 ");
            }
            else
            {
                sb.Append($"{Han}판 {Fu}부 ");
            }

            sb.Append($"= {TotalGain}점");

            if (IsTsumo)
            {
                if (IsDealer)
                    sb.Append($" (자 각 {TsumoPaymentFromNonDealer})");
                else
                    sb.Append($" (친 {TsumoPaymentFromDealer}, 자 각 {TsumoPaymentFromNonDealer})");
            }
            else
            {
                sb.Append($" (론 {RonPayment})");
            }

            return sb.ToString();
        }
    }
}

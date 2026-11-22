using System.Collections.Generic;

namespace MahjongAtelier.Core
{
    /// <summary>
    /// 화료 결과 UI에 전달할 데이터 패키지.
    /// 
    /// MahjongGameManager가 화료 시 이 객체를 만들어 UI 매니저에 넘김.
    /// UI는 이 데이터만 보고 화면을 구성 (게임 로직 직접 참조 안 함 → 결합 낮음).
    /// </summary>
    public sealed class AgariResultData
    {
        public int WinnerIndex { get; set; }
        public bool IsTsumo { get; set; }
        public bool IsDealer { get; set; }

        /// <summary>화료 시점의 손패 (13장 + 화료패).</summary>
        public List<TileData> HandTiles { get; set; } = new List<TileData>();

        /// <summary>화료패 (마지막에 들어온 패). 시각적으로 분리해서 강조.</summary>
        public TileData WinningTile { get; set; }

        /// <summary>도라 표시패 목록.</summary>
        public List<TileData> DoraIndicators { get; set; } = new List<TileData>();

        /// <summary>우라도라 표시패 (리치 화료 시).</summary>
        public List<TileData> UraDoraIndicators { get; set; } = new List<TileData>();

        /// <summary>성립한 역 목록. (이름, 판수)</summary>
        public List<YakuEntry> Yakus { get; set; } = new List<YakuEntry>();

        /// <summary>도라 판수 (별도 표시).</summary>
        public int DoraHan { get; set; }

        // === 점수 정보 ===

        public int Fu { get; set; }
        public int Han { get; set; }
        public int YakumanCount { get; set; }
        public ScoreClass ScoreClass { get; set; }
        public int TotalGain { get; set; }

        /// <summary>지불 분배 (UI 표시용 문자열).</summary>
        public string PaymentDescription { get; set; }

        public sealed class YakuEntry
        {
            public string Name;
            public int Han;
            public bool IsYakuman;

            public YakuEntry(string name, int han, bool isYakuman = false)
            {
                Name = name;
                Han = han;
                IsYakuman = isYakuman;
            }
        }

        /// <summary>YakuResult + ScoreResult로부터 자동 생성.</summary>
        public static AgariResultData From(
            int winnerIndex, bool isDealer, List<TileData> handTiles,
            TileData winningTile, List<TileData> doraIndicators,
            List<TileData> uraDoraIndicators,
            YakuResult yakuResult, ScoreResult scoreResult)
        {
            var data = new AgariResultData
            {
                WinnerIndex = winnerIndex,
                IsDealer = isDealer,
                IsTsumo = scoreResult.IsTsumo,
                HandTiles = handTiles,
                WinningTile = winningTile,
                DoraIndicators = doraIndicators,
                UraDoraIndicators = uraDoraIndicators,
                DoraHan = yakuResult.DoraHan,
                Fu = scoreResult.Fu,
                Han = scoreResult.Han,
                YakumanCount = scoreResult.YakumanCount,
                ScoreClass = scoreResult.Class,
                TotalGain = scoreResult.TotalGain,
                PaymentDescription = BuildPaymentDescription(scoreResult)
            };

            foreach (var (yaku, han) in yakuResult.Yakus)
            {
                data.Yakus.Add(new YakuEntry(yaku.NameKr, han, yaku.IsYakuman));
            }

            // 도라는 역 목록 마지막에 별도 항목으로
            if (yakuResult.DoraHan > 0 && yakuResult.YakumanCount == 0)
            {
                data.Yakus.Add(new YakuEntry("도라", yakuResult.DoraHan, false));
            }

            return data;
        }

        private static string BuildPaymentDescription(ScoreResult s)
        {
            if (s.YakumanCount > 0 || s.IsTsumo == false)
            {
                if (!s.IsTsumo) return $"론  {s.RonPayment}";
            }
            if (s.IsTsumo)
            {
                if (s.IsDealer)
                    return $"자 각  {s.TsumoPaymentFromNonDealer}";
                return $"친  {s.TsumoPaymentFromDealer}   자 각  {s.TsumoPaymentFromNonDealer}";
            }
            return $"론  {s.RonPayment}";
        }
    }
}

using System.Collections.Generic;

namespace CardGame.Core
{
    /// <summary>
    /// 덱 매니저. 80장 카드 관리.
    /// 
    /// 구성:
    ///   일반 카드 72장 = 4색 × 9숫자 × 2장
    ///   특수 카드 8장 = 4종류 × 2장
    /// 
    /// 마작의 WallManager와 유사하지만 훨씬 단순 (도라/왕패 없음).
    /// </summary>
    public sealed class Deck
    {
        public const int TotalCards = 80;
        public const int NormalCards = 72;
        public const int SpecialCards = 8;

        /// <summary>드로우 파일 (덱의 위쪽부터 뽑음).</summary>
        private readonly List<CardData> _drawPile = new List<CardData>();

        /// <summary>버림 카드 (라운드 종료 시 다시 셔플해서 덱에 넣을 수 있음).</summary>
        private readonly List<CardData> _discardPile = new List<CardData>();

        public int RemainingDraws => _drawPile.Count;
        public int DiscardedCount => _discardPile.Count;
        public bool IsEmpty => _drawPile.Count == 0;

        /// <summary>덱을 처음부터 만들어 채움. 카드 ID는 0부터 시작.</summary>
        public void CreateDeck()
        {
            _drawPile.Clear();
            _discardPile.Clear();

            int nextId = 0;

            // 일반 카드 72장
            for (int c = 0; c < 4; c++)
            {
                var color = (CardColor)c;
                for (int number = 1; number <= 9; number++)
                {
                    // 각 카드 2장씩
                    for (int copy = 0; copy < 2; copy++)
                    {
                        var kind = new CardKind(color, number);
                        _drawPile.Add(new CardData(nextId++, kind));
                    }
                }
            }

            // 특수 카드 8장 (4종류 × 2장)
            foreach (SpecialCardType special in new[]
            {
                SpecialCardType.Wild,
                SpecialCardType.Swap,
                SpecialCardType.Peek,
                SpecialCardType.Chaos
            })
            {
                for (int copy = 0; copy < 2; copy++)
                {
                    var kind = new CardKind(special);
                    _drawPile.Add(new CardData(nextId++, kind));
                }
            }
        }

        /// <summary>덱 셔플.</summary>
        public void Shuffle(System.Random rng)
        {
            for (int i = _drawPile.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (_drawPile[i], _drawPile[j]) = (_drawPile[j], _drawPile[i]);
            }
        }

        /// <summary>덱 위에서 1장 뽑기. 없으면 null.</summary>
        public CardData DrawTop()
        {
            if (_drawPile.Count == 0) return null;
            int lastIdx = _drawPile.Count - 1;
            var card = _drawPile[lastIdx];
            _drawPile.RemoveAt(lastIdx);
            return card;
        }

        /// <summary>버림 파일에 추가.</summary>
        public void AddToDiscard(CardData card)
        {
            if (card == null) return;
            _discardPile.Add(card);
        }

        /// <summary>
        /// 덱이 비었을 때 버림 파일을 다시 셔플해서 덱으로 만듦.
        /// 규칙에 따라 다른데 기본은 남은 카드로 계속 진행.
        /// </summary>
        public void ReshuffleDiscardIntoDeck(System.Random rng)
        {
            _drawPile.AddRange(_discardPile);
            _discardPile.Clear();
            Shuffle(rng);
        }

        /// <summary>덱 위 N장 확인 (탐색 카드용). 뽑지는 않음.</summary>
        public List<CardData> PeekTop(int count)
        {
            var result = new List<CardData>();
            int start = System.Math.Max(0, _drawPile.Count - count);
            for (int i = _drawPile.Count - 1; i >= start; i--)
                result.Add(_drawPile[i]);
            return result;
        }

        /// <summary>덱에서 특정 카드를 꺼냄 (탐색 카드로 선택 시).</summary>
        public bool RemoveFromDeck(CardData card)
        {
            return _drawPile.Remove(card);
        }
    }
}

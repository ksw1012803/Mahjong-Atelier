using System.Collections.Generic;
using UnityEngine;

namespace CardGame.Core
{
    /// <summary>
    /// 카드 덱 v5 — 온라인 지원.
    ///
    /// v5 변경:
    ///   - CreateDeck 시 각 카드에 InstanceId(0..N-1) 순차 부여 → 네트워크 식별자
    ///   - FindInDrawPile(instanceId): Peek 선택을 네트워크 명령으로 해석할 때 사용
    ///   - ReplicaDrawCount: 온라인 클라이언트가 "호스트가 알려준 남은 장수"를 표시할 때 사용
    ///     (클라이언트는 실제 덱 내용을 모름 — 장수만 받음)
    ///   - ClearReplica(): 라운드 시작 시 클라이언트 측 버림패/카운트 초기화
    /// </summary>
    public class Deck
    {
        private readonly List<CardData> _drawPile = new List<CardData>();
        private readonly List<CardData> _discardPile = new List<CardData>();
        private CardDatabase _database;

        /// <summary>온라인 클라이언트용 남은 장수 오버라이드. -1이면 실제 _drawPile.Count 사용.</summary>
        public int ReplicaDrawCount { get; set; } = -1;

        public int DrawPileCount => ReplicaDrawCount >= 0 ? ReplicaDrawCount : _drawPile.Count;
        public int DiscardPileCount => _discardPile.Count;
        public IReadOnlyList<CardData> DiscardPile => _discardPile;
        public IReadOnlyList<CardData> DrawPile => _drawPile;

        public void SetDatabase(CardDatabase database) => _database = database;

        public void CreateDeck()
        {
            _drawPile.Clear();
            _discardPile.Clear();
            ReplicaDrawCount = -1;

            if (_database == null)
            {
                Debug.LogError("[Deck] CardDatabase 없음. SetDatabase 호출 필요.");
                return;
            }

            int id = 0;
            foreach (var def in _database.GetDeckDefinitions())
            {
                var card = new CardData(def) { InstanceId = id++ };
                _drawPile.Add(card);
            }

            Debug.Log($"[Deck] 생성: {_drawPile.Count}장");
        }

        public void Shuffle(System.Random rng)
        {
            for (int i = _drawPile.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (_drawPile[i], _drawPile[j]) = (_drawPile[j], _drawPile[i]);
            }
        }

        public CardData DrawTop()
        {
            if (_drawPile.Count == 0) return null;
            var card = _drawPile[_drawPile.Count - 1];
            _drawPile.RemoveAt(_drawPile.Count - 1);
            return card;
        }

        public List<CardData> PeekTop(int count)
        {
            var list = new List<CardData>();
            for (int i = 0; i < count && _drawPile.Count - 1 - i >= 0; i++)
                list.Add(_drawPile[_drawPile.Count - 1 - i]);
            return list;
        }

        /// <summary>InstanceId로 덱(드로우 파일) 안의 카드 검색.</summary>
        public CardData FindInDrawPile(int instanceId)
        {
            for (int i = 0; i < _drawPile.Count; i++)
                if (_drawPile[i].InstanceId == instanceId) return _drawPile[i];
            return null;
        }

        /// <summary>덱에서 특정 카드 제거 (Peek 선택 시 사용).</summary>
        public bool RemoveFromDeck(CardData card)
        {
            if (card == null) return false;
            return _drawPile.Remove(card);
        }

        public void AddToDiscard(CardData card)
        {
            if (card != null) _discardPile.Add(card);
        }

        public void ReshuffleDiscardIntoDeck(System.Random rng)
        {
            if (_discardPile.Count <= 1) return;
            var top = _discardPile[_discardPile.Count - 1];
            _discardPile.RemoveAt(_discardPile.Count - 1);
            _drawPile.AddRange(_discardPile);
            _discardPile.Clear();
            _discardPile.Add(top);
            Shuffle(rng);
        }

        /// <summary>온라인 클라이언트: 라운드 시작 시 복제본 상태 초기화.</summary>
        public void ClearReplica()
        {
            _drawPile.Clear();
            _discardPile.Clear();
            ReplicaDrawCount = 0;
        }
    }
}

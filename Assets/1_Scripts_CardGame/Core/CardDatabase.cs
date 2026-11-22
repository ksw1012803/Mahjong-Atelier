using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace CardGame.Core
{
    /// <summary>
    /// 카드 데이터베이스 v4 — 네트워크용 Definition 인덱스 조회 추가.
    ///
    /// v4 변경:
    ///   - OrderedDefinitions: normalCards + specialCards 순서로 고정된 리스트 (모든 클라이언트에서 동일)
    ///   - GetDefinitionIndex / GetDefinitionByIndex: NetCard.defIndex ↔ CardDefinition 변환
    ///   (v3: _initTask 공유로 중복 호출 안전)
    /// </summary>
    [CreateAssetMenu(menuName = "SevenMaid/Card Database", fileName = "CardDatabase")]
    public class CardDatabase : ScriptableObject
    {
        [Header("일반 카드 (4색 × 9숫자 = 36개)")]
        public List<CardDefinition> normalCards = new List<CardDefinition>();

        [Header("특수 카드 (4종)")]
        public List<CardDefinition> specialCards = new List<CardDefinition>();

        [Header("덱 구성")]
        public int normalCardCopies = 2;
        public int specialCardCopies = 2;

        private Dictionary<CardDefinition, Sprite> _spriteCache;
        private Dictionary<CardDefinition, GameObject> _prefabCache;
        private List<AsyncOperationHandle> _handles;
        private bool _initialized;
        private Task _initTask;

        // ★ v4: 네트워크 인덱스 테이블
        private List<CardDefinition> _ordered;
        private Dictionary<CardDefinition, int> _indexOf;

        public bool IsInitialized => _initialized;

        /// <summary>로딩 진행률 이벤트 (0.0 ~ 1.0).</summary>
        public event Action<float> OnProgress;

        public Task InitializeAsync()
        {
            if (_initialized)
            {
                OnProgress?.Invoke(1f);
                return Task.CompletedTask;
            }

            if (_initTask != null)
            {
                Debug.Log("[CardDatabase] 이미 초기화 진행 중, 기존 Task 재사용");
                return _initTask;
            }

            _initTask = InitializeInternalAsync();
            return _initTask;
        }

        private async Task InitializeInternalAsync()
        {
            _spriteCache = new Dictionary<CardDefinition, Sprite>();
            _prefabCache = new Dictionary<CardDefinition, GameObject>();
            _handles = new List<AsyncOperationHandle>();

            int totalAssets = 0;
            foreach (var def in GetAllDefinitions())
            {
                if (def == null) continue;
                if (def.sprite2D != null && def.sprite2D.RuntimeKeyIsValid()) totalAssets++;
                if (def.prefab3D != null && def.prefab3D.RuntimeKeyIsValid()) totalAssets++;
            }

            if (totalAssets == 0)
            {
                _initialized = true;
                _initTask = null;
                OnProgress?.Invoke(1f);
                return;
            }

            int loadedCount = 0;

            foreach (var def in GetAllDefinitions())
            {
                if (def == null) continue;

                if (def.sprite2D != null && def.sprite2D.RuntimeKeyIsValid())
                {
                    AsyncOperationHandle<Sprite> h;
                    if (def.sprite2D.OperationHandle.IsValid())
                        h = def.sprite2D.OperationHandle.Convert<Sprite>();
                    else
                        h = def.sprite2D.LoadAssetAsync<Sprite>();
                    _handles.Add(h);
                    var sprite = await h.Task;
                    if (sprite != null) _spriteCache[def] = sprite;
                    loadedCount++;
                    OnProgress?.Invoke((float)loadedCount / totalAssets);
                }

                if (def.prefab3D != null && def.prefab3D.RuntimeKeyIsValid())
                {
                    AsyncOperationHandle<GameObject> h;
                    if (def.prefab3D.OperationHandle.IsValid())
                        h = def.prefab3D.OperationHandle.Convert<GameObject>();
                    else
                        h = def.prefab3D.LoadAssetAsync<GameObject>();
                    _handles.Add(h);
                    var prefab = await h.Task;
                    if (prefab != null) _prefabCache[def] = prefab;
                    loadedCount++;
                    OnProgress?.Invoke((float)loadedCount / totalAssets);
                }
            }

            _initialized = true;
            _initTask = null;
            OnProgress?.Invoke(1f);
            Debug.Log($"[CardDatabase] 초기화 완료: 스프라이트 {_spriteCache.Count}, 프리팹 {_prefabCache.Count}");
        }

        public void Release()
        {
            if (_handles != null)
            {
                foreach (var h in _handles)
                    if (h.IsValid()) Addressables.Release(h);
                _handles.Clear();
            }
            _spriteCache?.Clear();
            _prefabCache?.Clear();
            _initialized = false;
            _initTask = null;
        }

        public Sprite GetSprite(CardDefinition def)
        {
            if (def == null || _spriteCache == null) return null;
            _spriteCache.TryGetValue(def, out var s);
            return s;
        }

        public GameObject GetPrefab(CardDefinition def)
        {
            if (def == null || _prefabCache == null) return null;
            _prefabCache.TryGetValue(def, out var p);
            return p;
        }

        public IEnumerable<CardDefinition> GetAllDefinitions()
        {
            foreach (var c in normalCards) if (c != null) yield return c;
            foreach (var c in specialCards) if (c != null) yield return c;
        }

        public List<CardDefinition> GetDeckDefinitions()
        {
            var list = new List<CardDefinition>(normalCards.Count * normalCardCopies + specialCards.Count * specialCardCopies);
            foreach (var def in normalCards)
                for (int i = 0; i < normalCardCopies; i++) list.Add(def);
            foreach (var def in specialCards)
                for (int i = 0; i < specialCardCopies; i++) list.Add(def);
            return list;
        }

        // ========== ★ v4: 네트워크 인덱스 ==========

        private void EnsureIndexTable()
        {
            if (_ordered != null) return;
            _ordered = new List<CardDefinition>();
            _indexOf = new Dictionary<CardDefinition, int>();
            foreach (var def in GetAllDefinitions())
            {
                _indexOf[def] = _ordered.Count;
                _ordered.Add(def);
            }
        }

        /// <summary>고정 순서의 Definition 목록 (모든 클라이언트에서 동일).</summary>
        public IReadOnlyList<CardDefinition> OrderedDefinitions
        {
            get { EnsureIndexTable(); return _ordered; }
        }

        /// <summary>Definition → 네트워크 인덱스. 없으면 -1.</summary>
        public int GetDefinitionIndex(CardDefinition def)
        {
            if (def == null) return -1;
            EnsureIndexTable();
            return _indexOf.TryGetValue(def, out var i) ? i : -1;
        }

        /// <summary>네트워크 인덱스 → Definition. 범위 밖이면 null.</summary>
        public CardDefinition GetDefinitionByIndex(int index)
        {
            EnsureIndexTable();
            if (index < 0 || index >= _ordered.Count) return null;
            return _ordered[index];
        }
    }
}

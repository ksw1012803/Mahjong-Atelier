using System.Collections.Generic;
using UnityEngine;
using CardGame.Core;

namespace CardGame.UI3D
{
    /// <summary>
    /// 3D 버림패 표시 v5 — CardDatabase 기반 프리팹 조회.
    /// </summary>
    public class Card3DDiscardPileView : MonoBehaviour
    {
        [Header("Player")]
        [SerializeField, Range(0, 3)] private int playerIndex = 0;

        [Header("References")]
        [SerializeField] private Transform pileAnchor;
        [SerializeField] private GameObject fallbackPrefab;

        [Header("Layout")]
        [SerializeField, Range(3, 12)] private int cardsPerRow = 7;
        [SerializeField] private float cardSpacingX = 0.12f;
        [SerializeField] private float rowSpacingZ = 0.18f;
        [SerializeField] private bool stackFromLeft = true;

        [Header("Transform")]
        [SerializeField] private bool preservePrefabScale = true;
        [SerializeField] private bool preservePrefabRotation = true;
        [SerializeField] private float scaleMultiplier = 1f;
        [SerializeField] private Vector3 additionalRotation = Vector3.zero;
        [SerializeField] private float cardYOffset = 0.01f;

        [Header("Options")]
        [SerializeField] private bool singleRowOnly = false;
        [SerializeField] private bool stackHeight = false;
        [SerializeField] private float stackHeightPerCard = 0.002f;

        private static CardDatabase _database;
        public static void SetDatabase(CardDatabase db) => _database = db;

        private readonly List<CardData> _discarded = new List<CardData>();
        private readonly List<GameObject> _spawned = new List<GameObject>();

        private void OnEnable()
        {
            CardGameEvents.OnCardDiscarded += HandleDiscarded;
            CardGameEvents.OnDealStarted += HandleDealStarted;
        }

        private void OnDisable()
        {
            CardGameEvents.OnCardDiscarded -= HandleDiscarded;
            CardGameEvents.OnDealStarted -= HandleDealStarted;
        }

        private void HandleDealStarted()
        {
            _discarded.Clear();
            Refresh();
        }

        private void HandleDiscarded(int fromPlayerIndex, CardData card)
        {
            if (fromPlayerIndex != playerIndex) return;
            if (card == null) return;
            _discarded.Add(card);
            Refresh();
        }

        private void Refresh()
        {
            ClearSpawned();
            if (pileAnchor == null) return;

            for (int i = 0; i < _discarded.Count; i++)
            {
                int row = singleRowOnly ? 0 : (i / cardsPerRow);
                int col = singleRowOnly ? i : (i % cardsPerRow);

                float x = col * cardSpacingX;
                float z = -row * rowSpacingZ;
                float y = cardYOffset + (stackHeight ? i * stackHeightPerCard : 0f);

                float xOffset = stackFromLeft ? 0f : -(GetRowCount(row) - 1) * cardSpacingX * 0.5f;
                Spawn(_discarded[i], new Vector3(x + xOffset, y, z));
            }
        }

        private int GetRowCount(int row)
        {
            if (singleRowOnly) return _discarded.Count;
            int start = row * cardsPerRow;
            return Mathf.Min(cardsPerRow, _discarded.Count - start);
        }

        private void Spawn(CardData card, Vector3 localPos)
        {
            GameObject prefab = null;
            if (_database != null && card.Definition != null)
                prefab = _database.GetPrefab(card.Definition);
            if (prefab == null) prefab = fallbackPrefab;
            if (prefab == null) return;

            var prefabScale = prefab.transform.localScale;
            var prefabRot = prefab.transform.localRotation;

            var go = Instantiate(prefab, pileAnchor);
            _spawned.Add(go);
            go.transform.localPosition = localPos;

            go.transform.localScale = preservePrefabScale
                ? prefabScale * scaleMultiplier
                : Vector3.one * scaleMultiplier;

            if (preservePrefabRotation)
                go.transform.localRotation = prefabRot * Quaternion.Euler(additionalRotation);
            else
                go.transform.localEulerAngles = additionalRotation;

            go.name = $"Discard_P{playerIndex}_{card}";

            foreach (var col in go.GetComponentsInChildren<Collider>()) col.enabled = false;
        }

        private void ClearSpawned()
        {
            foreach (var go in _spawned)
                if (go != null) Destroy(go);
            _spawned.Clear();
        }
    }
}
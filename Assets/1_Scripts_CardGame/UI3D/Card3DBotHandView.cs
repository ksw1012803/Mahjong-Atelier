using System.Collections.Generic;
using UnityEngine;
using CardGame.Core;

namespace CardGame.UI3D
{
    /// <summary>
    /// 다른 플레이어(봇) 손패를 3D로 표시.
    /// 
    /// 뒷면만 보이기 때문에 CardData는 필요 없고 개수만 카운트.
    /// 특정 플레이어 앞 3D 위치에 뒷면 카드 배치.
    /// </summary>
    public class Card3DBotHandView : MonoBehaviour
    {
        [Header("Player Assignment")]
        [SerializeField, Range(0, 3)] private int playerIndex = 1;

        [Header("References")]
        [SerializeField] private CardGameManager gameManager;

        [Tooltip("카드 뒷면 프리팹")]
        [SerializeField] private GameObject cardBackPrefab;

        [Tooltip("손패가 놓일 앵커")]
        [SerializeField] private Transform handAnchor;

        [Header("Layout")]
        [SerializeField] private float cardSpacing = 0.1f;
        [SerializeField] private Vector3 cardRotation = new Vector3(90f, 0f, 0f);
        [SerializeField, Range(0f, 30f)] private float fanAngle = 3f;
        [SerializeField] private float cardScale = 0.8f;

        private readonly List<GameObject> _spawnedCards = new List<GameObject>();

        private void OnEnable()
        {
            CardGameEvents.OnHandChanged += HandleHandChanged;
            CardGameEvents.OnDealCompleted += HandleDealCompleted;
        }

        private void OnDisable()
        {
            CardGameEvents.OnHandChanged -= HandleHandChanged;
            CardGameEvents.OnDealCompleted -= HandleDealCompleted;
        }

        private void HandleDealCompleted() => RefreshDisplay();

        private void HandleHandChanged(int idx)
        {
            if (idx != playerIndex) return;
            RefreshDisplay();
        }

        private void RefreshDisplay()
        {
            ClearSpawned();
            if (gameManager == null || cardBackPrefab == null || handAnchor == null) return;
            if (playerIndex >= gameManager.Players.Count) return;

            var player = gameManager.Players[playerIndex];
            int count = player.Hand.Count;

            float totalWidth = (count - 1) * cardSpacing;
            float startX = -totalWidth * 0.5f;

            for (int i = 0; i < count; i++)
            {
                var go = Instantiate(cardBackPrefab, handAnchor);
                _spawnedCards.Add(go);

                float t = count > 1 ? (float)i / (count - 1) : 0.5f;
                float x = startX + i * cardSpacing;
                float fanRot = fanAngle * (t - 0.5f) * 2f;

                go.transform.localPosition = new Vector3(x, 0, 0);
                go.transform.localEulerAngles = cardRotation + new Vector3(0, 0, -fanRot);
                go.transform.localScale = Vector3.one * cardScale;
            }
        }

        private void ClearSpawned()
        {
            foreach (var go in _spawnedCards)
                if (go != null) Destroy(go);
            _spawnedCards.Clear();
        }
    }
}

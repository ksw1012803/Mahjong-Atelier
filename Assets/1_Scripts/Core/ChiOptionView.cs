using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using MahjongAtelier.Core;

namespace MahjongAtelier.UI
{
    /// <summary>
    /// 치 옵션 한 개 표시 + 클릭.
    /// 
    /// v2: TileDatabase 런타임 자동 검색 (Scene 오브젝트도 OK).
    /// </summary>
    public class ChiOptionView : MonoBehaviour, IPointerClickHandler
    {
        [Header("Tiles Display")]
        [SerializeField] private RectTransform tilesContainer;
        [SerializeField] private GameObject tilePrefab;

        [Tooltip("비워두면 런타임에 FindObjectOfType<MahjongTileDatabase>로 자동 검색")]
        [SerializeField] private MahjongTileDatabase tileDatabase;

        [Header("Visual")]
        [SerializeField] private Image highlightImage;
        [SerializeField] private Button button;

        private ChiOption _option;
        private Action<ChiOption> _onSelected;
        private readonly List<GameObject> _spawnedTiles = new List<GameObject>();

        private void Awake()
        {
            if (highlightImage != null) highlightImage.enabled = false;
            if (button != null) button.onClick.AddListener(HandleClick);
        }

        private void OnDestroy()
        {
            if (button != null) button.onClick.RemoveListener(HandleClick);
        }

        public void Setup(ChiOption option, Action<ChiOption> onSelected)
        {
            _option = option;
            _onSelected = onSelected;

            ClearTiles();
            if (option == null) return;

            if (tilesContainer == null)
            {
                Debug.LogError("[ChiOptionView] 'Tiles Container' 필드가 비어있음");
                return;
            }
            if (tilePrefab == null)
            {
                Debug.LogError("[ChiOptionView] 'Tile Prefab' 필드가 비어있음");
                return;
            }

            // ★ TileDatabase 런타임 자동 검색
            if (tileDatabase == null)
            {
                tileDatabase = FindObjectOfType<MahjongTileDatabase>();
                if (tileDatabase == null)
                {
                    Debug.LogError("[ChiOptionView] MahjongTileDatabase를 Scene에서 찾지 못함. " +
                        "Scene에 MahjongTileDatabase 컴포넌트가 있는 GameObject 존재해야 함");
                    return;
                }
                Debug.Log("[ChiOptionView] TileDatabase 자동 검색 성공");
            }

            // 슌츠 3장 표시
            SpawnTile(option.LowTile);
            SpawnTile(option.MiddleTile);
            SpawnTile(option.HighTile);
        }

        /// <summary>
        /// 외부에서 TileDatabase를 직접 주입 (런타임 검색 비용 절약).
        /// ChiSelectionUI에서 Show 직전에 호출해도 됨.
        /// </summary>
        public void SetTileDatabase(MahjongTileDatabase db)
        {
            tileDatabase = db;
        }

        private void SpawnTile(TileData tileData)
        {
            if (tileData == null) return;

            var go = Instantiate(tilePrefab, tilesContainer);
            go.SetActive(true);
            _spawnedTiles.Add(go);

            var dragUI = go.GetComponent<TileDragUI>();
            if (dragUI != null) dragUI.enabled = false;

            var cg = go.GetComponent<CanvasGroup>();
            if (cg != null) cg.blocksRaycasts = false;

            var imgs = go.GetComponentsInChildren<Image>();
            foreach (var img in imgs) img.raycastTarget = false;

            var view = go.GetComponent<TileView>();
            if (view != null)
            {
                var sprite = tileDatabase.GetSprite(tileData);
                view.SetTile(tileData, sprite);
            }
        }

        private void ClearTiles()
        {
            foreach (var go in _spawnedTiles)
                if (go != null) Destroy(go);
            _spawnedTiles.Clear();
        }

        public void OnPointerClick(PointerEventData eventData) => HandleClick();

        private void HandleClick()
        {
            if (_option == null) return;
            Debug.Log($"[ChiOptionView] 옵션 클릭됨: 시작={_option.SequenceStart}");
            _onSelected?.Invoke(_option);
        }
    }
}
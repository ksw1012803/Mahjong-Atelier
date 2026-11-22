using UnityEngine;
using UnityEngine.UI;
using MahjongAtelier.Core;

namespace MahjongAtelier.UI
{
    public class TileView : MonoBehaviour
    {
        public Image tileImage;

        [SerializeField] private GameObject redIndicator; // 아카도라 표시 (선택적)

        private TileData tileData;
        public TileData TileData => tileData;

        public void SetTile(TileData data, Sprite sprite)
        {
            tileData = data;
            tileImage.sprite = sprite;

            // 아카도라 시각 강조
            if (redIndicator != null)
                redIndicator.SetActive(data != null && data.IsRedFive);
        }
    }
}

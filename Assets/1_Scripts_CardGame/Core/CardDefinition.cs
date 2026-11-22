using UnityEngine;
using UnityEngine.AddressableAssets;

namespace CardGame.Core
{
    /// <summary>
    /// 카드 한 종류 정의 (ScriptableObject).
    /// 일반 카드 36개 + 특수 카드 4개 = 40 에셋.
    /// 스프라이트/프리팹은 Addressable로 비동기 로드.
    /// </summary>
    [CreateAssetMenu(menuName = "SevenMaid/Card Definition", fileName = "Card_")]
    public class CardDefinition : ScriptableObject
    {
        [Header("ID")]
        [Tooltip("고유 ID: red_1 ~ red_9, blue_1, wild, swap, peek, chaos 등")]
        public string id;

        [Header("Kind")]
        public bool isSpecial;
        public CardColor color = CardColor.Red;
        [Range(1, 9)] public int number = 1;
        public SpecialCardType specialType = SpecialCardType.Wild;

        [Header("Visual (Addressable)")]
        public AssetReferenceSprite sprite2D;
        public AssetReferenceGameObject prefab3D;

        [Header("Display")]
        public string displayName;
        [TextArea(2, 4)] public string description;

        public CardKind ToKind() => isSpecial
            ? CardKind.MakeSpecial(specialType)
            : CardKind.MakeNormal(color, number);
    }
}

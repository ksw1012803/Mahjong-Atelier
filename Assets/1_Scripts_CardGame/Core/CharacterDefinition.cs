using UnityEngine;

namespace CardGame.Core
{
    /// <summary>
    /// 플레이어가 고를 수 있는 캐릭터 1명.
    ///   - portrait : 프로필(로비 좌상단, 인게임 프로필 칸)에 쓰는 정사각형 이미지
    ///   - fullBody : 로비에 서 있는 전신 이미지
    /// 에셋 위치: Assets/Resources/Characters/ (CharacterDatabase가 목록으로 참조)
    /// </summary>
    [CreateAssetMenu(menuName = "SEVEN MAID/Character", fileName = "Character_")]
    public class CharacterDefinition : ScriptableObject
    {
        [Tooltip("저장·네트워크용 고유 ID (바꾸지 말 것).")]
        public string id;
        [Tooltip("선택창에 보이는 이름.")]
        public string displayName;
        [TextArea] public string description;

        [Header("Images")]
        public Sprite portrait;
        public Sprite fullBody;
    }
}

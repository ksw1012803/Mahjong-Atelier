#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;
using UnityEngine.AddressableAssets;
using CardGame.Core;

namespace CardGame.EditorTools
{
    /// <summary>
    /// CardDefinition 40개 자동 생성 + Addressable 등록 + Database 세팅.
    /// 
    /// 사용법:
    ///   Tools → SEVEN MAID → Build Card Database
    /// 
    /// 전제:
    ///   - Resources/CardSprites/ 에 PNG 36장 (fire_red_card_01 등)
    ///   - Resources/Cards/ 에 3D 프리팹 36개 (Red_Fire_001 등)
    ///   - Addressables 패키지 설치됨
    /// </summary>
    public class CardDatabaseBuilder : EditorWindow
    {
        private const string DEFINITION_FOLDER = "Assets/Resources/CardDefinitions";
        private const string DATABASE_PATH = "Assets/Resources/CardDatabase.asset";
        private const string SPRITE_FOLDER = "Assets/Resources/CardSprites";
        private const string PREFAB_FOLDER = "Assets/Resources/Cards";

        [MenuItem("Tools/SEVEN MAID/Build Card Database")]
        public static void BuildAll()
        {
            if (!EditorUtility.DisplayDialog(
                "Card Database 생성",
                "기존 CardDefinition들이 덮어씌워질 수 있습니다. 계속하시겠습니까?",
                "진행", "취소")) return;

            EnsureFolder(DEFINITION_FOLDER);
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null)
            {
                EditorUtility.DisplayDialog("Addressables 미설정",
                    "Window → Asset Management → Addressables → Groups 에서 초기 설정 먼저.", "확인");
                return;
            }

            var group = settings.FindGroup("Cards") ?? settings.CreateGroup(
                "Cards", false, false, true, null,
                typeof(UnityEditor.AddressableAssets.Settings.GroupSchemas.BundledAssetGroupSchema),
                typeof(UnityEditor.AddressableAssets.Settings.GroupSchemas.ContentUpdateGroupSchema));

            var database = AssetDatabase.LoadAssetAtPath<CardDatabase>(DATABASE_PATH);
            if (database == null)
            {
                database = ScriptableObject.CreateInstance<CardDatabase>();
                AssetDatabase.CreateAsset(database, DATABASE_PATH);
            }
            database.normalCards.Clear();
            database.specialCards.Clear();

            // 일반 카드 36장
            var colors = new[]
            {
                (CardColor.Red,    "fire_red",     "Red_Fire"),
                (CardColor.Blue,   "water_blue",   "Blue_Water"),
                (CardColor.Green,  "air_green",    "Green_Air"),
                (CardColor.Yellow, "earth_yellow", "Yellow_Earth"),
            };

            foreach (var (color, spritePrefix, prefabPrefix) in colors)
            {
                for (int n = 1; n <= 9; n++)
                {
                    var id = $"{color.ToString().ToLower()}_{n}";
                    var def = CreateOrLoadDefinition(id);
                    def.isSpecial = false;
                    def.color = color;
                    def.number = n;
                    def.displayName = $"{color} {n}";

                    var spritePath = $"{SPRITE_FOLDER}/{spritePrefix}_card_{n:D2}.png";
                    var prefabPath = $"{PREFAB_FOLDER}/{prefabPrefix}_{n:D3}.prefab";

                    AssignAddressable(settings, group, spritePath, def, isSprite: true);
                    AssignAddressable(settings, group, prefabPath, def, isSprite: false);

                    EditorUtility.SetDirty(def);
                    database.normalCards.Add(def);
                }
            }

            // 특수 카드 4종
            foreach (SpecialCardType type in System.Enum.GetValues(typeof(SpecialCardType)))
            {
                var id = type.ToString().ToLower();
                var def = CreateOrLoadDefinition(id);
                def.isSpecial = true;
                def.specialType = type;
                def.displayName = type.ToString();
                def.description = GetDescription(type);

                EditorUtility.SetDirty(def);
                database.specialCards.Add(def);
            }

            EditorUtility.SetDirty(database);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            EditorUtility.DisplayDialog("완료",
                $"CardDefinition 40개 생성 + Addressable 등록 + Database 세팅 완료.\n\n위치:\n{DATABASE_PATH}",
                "확인");

            Selection.activeObject = database;
            EditorGUIUtility.PingObject(database);
        }

        private static CardDefinition CreateOrLoadDefinition(string id)
        {
            var path = $"{DEFINITION_FOLDER}/Card_{id}.asset";
            var def = AssetDatabase.LoadAssetAtPath<CardDefinition>(path);
            if (def == null)
            {
                def = ScriptableObject.CreateInstance<CardDefinition>();
                AssetDatabase.CreateAsset(def, path);
            }
            def.id = id;
            return def;
        }

        private static void AssignAddressable(
            AddressableAssetSettings settings, AddressableAssetGroup group,
            string assetPath, CardDefinition def, bool isSprite)
        {
            var guid = AssetDatabase.AssetPathToGUID(assetPath);
            if (string.IsNullOrEmpty(guid))
            {
                Debug.LogWarning($"[CardDatabaseBuilder] 에셋 없음: {assetPath}");
                return;
            }

            var entry = settings.CreateOrMoveEntry(guid, group);
            entry.address = Path.GetFileNameWithoutExtension(assetPath);

            if (isSprite)
                def.sprite2D = new AssetReferenceSprite(guid);
            else
                def.prefab3D = new AssetReferenceGameObject(guid);
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parent = Path.GetDirectoryName(path);
            if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        private static string GetDescription(SpecialCardType type) => type switch
        {
            SpecialCardType.Wild  => "모든 카드로 사용 가능. 완성 패에 넣을 수 있음.",
            SpecialCardType.Swap  => "상대 플레이어와 카드 1장 교환.",
            SpecialCardType.Peek  => "덱 위 3장 중 1장 선택해서 가져옴.",
            SpecialCardType.Chaos => "모든 플레이어의 카드를 섞어 재분배.",
            _ => ""
        };
    }
}
#endif

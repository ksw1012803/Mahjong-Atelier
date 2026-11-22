#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.IO;

namespace CardGame.EditorTools
{
    /// <summary>
    /// Resources/CardSprites/ 폴더로 임포트되는 PNG를 자동으로 Sprite로 설정.
    /// 
    /// 사용법:
    ///   1. 이 파일을 Assets/1_Scripts_CardGame/Editor/ 폴더에 배치
    ///      (반드시 Editor 폴더여야 함)
    ///   2. 폴더 없으면 생성:
    ///      Assets/1_Scripts_CardGame/Editor/CardSpritePostprocessor.cs
    ///   3. 저장하면 자동 컴파일
    /// 
    /// 작동:
    ///   - CardSprites 폴더에 PNG를 끌어다 놓으면 자동으로 Sprite로 설정됨
    ///   - 기존 PNG는 메뉴에서 일괄 변환 가능
    /// </summary>
    public class CardSpritePostprocessor : AssetPostprocessor
    {
        // 자동 변환 대상 폴더 (경로 포함)
        private const string TARGET_FOLDER = "Resources/CardSprites";

        private void OnPreprocessTexture()
        {
            // 대상 폴더 안 PNG만 처리
            if (!assetPath.Contains(TARGET_FOLDER)) return;
            if (!assetPath.ToLower().EndsWith(".png")) return;

            TextureImporter importer = (TextureImporter)assetImporter;

            // 이미 Sprite로 설정됐으면 건너뜀 (사용자 수동 변경 보호)
            if (importer.textureType == TextureImporterType.Sprite) return;

            // Sprite로 자동 설정
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 100f;
            importer.filterMode = FilterMode.Bilinear;
            importer.mipmapEnabled = false;  // UI용은 밉맵 불필요
            importer.alphaIsTransparency = true;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;

            Debug.Log($"[CardSprite] 자동 Sprite 설정: {Path.GetFileName(assetPath)}");
        }

        // ================================================================
        // 메뉴: 기존 PNG 일괄 변환
        // ================================================================
        [MenuItem("Tools/SEVEN MAID/Convert CardSprites to Sprite")]
        public static void ConvertAllCardSpritesInFolder()
        {
            string folder = "Assets/" + TARGET_FOLDER;
            if (!Directory.Exists(folder))
            {
                Debug.LogError($"[CardSprite] 폴더가 없음: {folder}");
                EditorUtility.DisplayDialog(
                    "폴더 없음",
                    $"폴더가 없습니다:\n{folder}\n\n먼저 Resources/CardSprites 폴더를 만들고 PNG를 넣어주세요.",
                    "확인");
                return;
            }

            // 폴더 안 모든 PNG 찾기
            string[] pngFiles = Directory.GetFiles(folder, "*.png", SearchOption.AllDirectories);
            if (pngFiles.Length == 0)
            {
                Debug.LogWarning($"[CardSprite] PNG 파일 없음: {folder}");
                EditorUtility.DisplayDialog(
                    "파일 없음",
                    $"PNG 파일이 없습니다:\n{folder}",
                    "확인");
                return;
            }

            int converted = 0;
            int skipped = 0;

            try
            {
                AssetDatabase.StartAssetEditing();

                for (int i = 0; i < pngFiles.Length; i++)
                {
                    string assetPath = pngFiles[i].Replace("\\", "/");

                    EditorUtility.DisplayProgressBar(
                        "Converting to Sprite",
                        $"{Path.GetFileName(assetPath)} ({i + 1}/{pngFiles.Length})",
                        (float)i / pngFiles.Length);

                    TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
                    if (importer == null) continue;

                    if (importer.textureType == TextureImporterType.Sprite)
                    {
                        skipped++;
                        continue;
                    }

                    importer.textureType = TextureImporterType.Sprite;
                    importer.spriteImportMode = SpriteImportMode.Single;
                    importer.spritePixelsPerUnit = 100f;
                    importer.filterMode = FilterMode.Bilinear;
                    importer.mipmapEnabled = false;
                    importer.alphaIsTransparency = true;
                    importer.alphaSource = TextureImporterAlphaSource.FromInput;

                    importer.SaveAndReimport();
                    converted++;
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
                EditorUtility.ClearProgressBar();
            }

            AssetDatabase.Refresh();

            string msg = $"변환 완료!\n\n- 변환됨: {converted}장\n- 이미 Sprite였음: {skipped}장\n- 총: {pngFiles.Length}장";
            Debug.Log($"[CardSprite] {msg}");
            EditorUtility.DisplayDialog("일괄 변환 완료", msg, "확인");
        }

        // ================================================================
        // 메뉴: 스프라이트 매퍼 캐시 비우기 (리임포트 후 재적용)
        // ================================================================
        [MenuItem("Tools/SEVEN MAID/Clear Sprite Cache")]
        public static void ClearSpriteCache()
        {
            CardGame.Core.CardSpriteMapper.ClearCache();
            Debug.Log("[CardSprite] 캐시 비웠음. 다음 로드 시 재조회됨.");
        }
    }
}
#endif

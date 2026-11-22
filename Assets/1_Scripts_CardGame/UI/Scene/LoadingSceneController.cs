using System;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using CardGame.Core;

namespace CardGame.UI
{
    /// <summary>
    /// LoadScene 컨트롤러 v8 — SceneLoader에 완료 신호 전달.
    ///
    /// v8 변경:
    ///   - public static bool IsLoadComplete — SceneLoader가 이 flag를 보고 대기
    ///   - DB 로드 + 가짜 진행 둘 다 끝나면 IsLoadComplete = true
    ///   - OnDestroy/OnDisable에서 flag reset
    /// </summary>
    public class LoadingSceneController : MonoBehaviour
    {
        /// <summary>SceneLoader가 전환 전 체크하는 완료 플래그.</summary>
        public static bool IsLoadComplete { get; private set; }

        [Header("Data")]
        [SerializeField] private CardDatabase cardDatabase;

        [Header("UI")]
        [SerializeField] private Slider progressBar;
        [SerializeField] private TextMeshProUGUI progressText;
        [SerializeField] private TextMeshProUGUI nextText;

        [Header("Tip Messages")]
        [SerializeField] private string[] tipMessages;

        [Header("DB Load Condition")]
        [Tooltip("이 씬에서 온 경우에만 DB 로드 수행. 기본: LobbyScene.")]
        [SerializeField] private GameScene loadFromScene = GameScene.LobbyScene;

        [Header("Fake Progress (DB 로드 안 할 때)")]
        [Tooltip("DB 로드 안 할 때 가짜 진행 지속 시간 (초).")]
        [SerializeField] private float fakeProgressDuration = 2.5f;

        private void Awake()
        {
            // ★ v8: Scene 진입 즉시 flag reset (Start보다 먼저 실행 보장)
            IsLoadComplete = false;
        }

        private async void Start()
        {
            GameScene prev = SceneContext.PreviousScene;
            bool shouldLoadDB = (prev == loadFromScene);

            Debug.Log($"[LoadScene] 이전 씬: {prev} · DB 로드: {shouldLoadDB}");

            ShowRandomTipIfAny();

            if (shouldLoadDB && cardDatabase != null)
            {
                SetStatus("데이터베이스 로딩 중...");
                SetProgress(0f);

                cardDatabase.OnProgress += OnDBProgress;
                try
                {
                    await cardDatabase.InitializeAsync();
                }
                catch (Exception e)
                {
                    Debug.LogError($"[LoadScene] DB 로드 실패: {e}");
                }
                finally
                {
                    cardDatabase.OnProgress -= OnDBProgress;
                }

                SetProgress(1f);
                SetStatus("완료");
            }
            else
            {
                SetStatus("준비 중...");
                await FakeProgressFill(fakeProgressDuration);
                SetStatus("완료");
            }

            // ★ v8: SceneLoader가 이 flag를 보고 전환
            Debug.Log("[LoadScene] 완료 신호 전송 (IsLoadComplete = true)");
            IsLoadComplete = true;
        }

        private async Task FakeProgressFill(float duration)
        {
            float t = 0f;
            float step = 0.04f;
            while (t < duration)
            {
                if (this == null) return;
                SetProgress(Mathf.Clamp01(t / duration));
                await Task.Delay(TimeSpan.FromSeconds(step));
                t += step;
            }
            SetProgress(1f);
        }

        private void OnDBProgress(float progress)
        {
            SetProgress(progress);
            int pct = Mathf.RoundToInt(Mathf.Clamp01(progress) * 100f);
            SetStatus($"로딩 중... {pct}%");
        }

        private void ShowRandomTipIfAny()
        {
            if (nextText == null) return;
            if (tipMessages == null || tipMessages.Length == 0) return;
            int idx = UnityEngine.Random.Range(0, tipMessages.Length);
            nextText.text = tipMessages[idx];
        }

        private void SetProgress(float value)
        {
            if (progressBar != null) progressBar.value = Mathf.Clamp01(value);
        }

        private void SetStatus(string text)
        {
            if (progressText != null) progressText.text = text;
        }

        private void OnDisable()
        {
            // Scene 전환 시 다음 LoadingScene을 위해 reset
            IsLoadComplete = false;
        }
    }
}
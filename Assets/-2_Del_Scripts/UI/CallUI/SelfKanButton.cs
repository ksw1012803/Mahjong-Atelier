using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using MahjongAtelier.Core;

namespace MahjongAtelier.UI
{
    /// <summary>
    /// 자기 차례 깡 버튼 (안깡/가깡 통합).
    /// 
    /// 동작:
    ///   - 자기 쯔모 직후 안깡 또는 가깡 가능 시 단일 "Kan" 버튼 표시
    ///   - 클릭 시 자동 판정:
    ///     * 안깡만 가능 → 안깡 선언 (첫 종류)
    ///     * 가깡만 가능 → 가깡 선언 (첫 종류)
    ///     * 둘 다 가능 → 안깡 우선 (멘젠 유지가 보통 유리)
    /// 
    /// 한 종류만 가능한 게 일반적이라 단일 버튼으로 충분.
    /// 여러 후보 종류 (예: 안깡 가능한 패가 2종류) 선택 UI는 추후 폴리시.
    /// </summary>
    public class SelfKanButton : MonoBehaviour
    {
        [Header("Kan Button")]
        [SerializeField] private Button kanButton;

        [Header("Optional Label")]
        [SerializeField] private TMP_Text kanLabel;

        [Header("References")]
        [SerializeField] private MahjongGameManager gameManager;

        private List<TileKind> _currentAnkanKinds = new List<TileKind>();
        private List<TileKind> _currentShouminkanKinds = new List<TileKind>();

        private void Awake()
        {
            if (kanButton != null)
            {
                kanButton.onClick.AddListener(HandleKanClicked);
                kanButton.gameObject.SetActive(false);
            }
            if (gameManager == null) gameManager = FindObjectOfType<MahjongGameManager>();
        }

        private void OnDestroy()
        {
            if (kanButton != null) kanButton.onClick.RemoveListener(HandleKanClicked);
        }

        private void OnEnable()
        {
            GameEvents.OnSelfKanAvailability += HandleAvailability;
            GameEvents.OnTurnStarted += HandleTurnStarted;
        }

        private void OnDisable()
        {
            GameEvents.OnSelfKanAvailability -= HandleAvailability;
            GameEvents.OnTurnStarted -= HandleTurnStarted;
        }

        private void HandleAvailability(List<TileKind> ankanKinds, List<TileKind> shouminkanKinds)
        {
            _currentAnkanKinds = ankanKinds ?? new List<TileKind>();
            _currentShouminkanKinds = shouminkanKinds ?? new List<TileKind>();

            bool canKan = _currentAnkanKinds.Count > 0 || _currentShouminkanKinds.Count > 0;

            if (kanButton != null) kanButton.gameObject.SetActive(canKan);

            // 라벨 갱신 (선택)
            if (kanLabel != null && canKan)
            {
                string detail = GetKanTypeDescription();
                kanLabel.text = $"깡 ({detail})";
            }
        }

        private string GetKanTypeDescription()
        {
            var parts = new List<string>();
            if (_currentAnkanKinds.Count > 0)
                parts.Add($"안: {KindsToString(_currentAnkanKinds)}");
            if (_currentShouminkanKinds.Count > 0)
                parts.Add($"가: {KindsToString(_currentShouminkanKinds)}");
            return string.Join(" / ", parts);
        }

        private void HandleTurnStarted(int playerIndex)
        {
            // 다른 사람 차례에는 버튼 숨김
            int userIdx = gameManager != null ? gameManager.UserPlayerIndex : 0;
            if (playerIndex != userIdx)
            {
                if (kanButton != null) kanButton.gameObject.SetActive(false);
            }
        }

        private void HandleKanClicked()
        {
            if (gameManager == null) return;

            // 우선순위: 안깡 > 가깡 (안깡은 멘젠 유지로 보통 유리)
            if (_currentAnkanKinds.Count > 0)
            {
                var kind = _currentAnkanKinds[0];
                Debug.Log($"[SelfKanButton] 안깡 선언: {kind}");
                gameManager.RequestAnkan(kind);
            }
            else if (_currentShouminkanKinds.Count > 0)
            {
                var kind = _currentShouminkanKinds[0];
                Debug.Log($"[SelfKanButton] 가깡 선언: {kind}");
                gameManager.RequestShouminkan(kind);
            }
        }

        private static string KindsToString(List<TileKind> kinds)
        {
            var parts = new List<string>();
            foreach (var k in kinds) parts.Add(k.ToString());
            return string.Join(", ", parts);
        }
    }
}

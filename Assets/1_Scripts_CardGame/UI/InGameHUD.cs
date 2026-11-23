using TMPro;
using UnityEngine;
using UnityEngine.UI;
using CardGame.Core;

namespace CardGame.UI
{
    /// <summary>
    /// 인게임 HUD — 라운드, 남은 패, 각 플레이어 닉네임·프로필 초상화·점수 표시.
    ///
    /// 동작: Update에서 CardGameManager 상태를 polling하고, 변경이 감지된 필드만 UI 갱신.
    /// </summary>
    public class InGameHUD : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("비워두면 Scene에서 자동 검색.")]
        [SerializeField] private CardGameManager gameManager;

        [Header("Round / Deck")]
        [SerializeField] private TMP_Text roundText;
        [Tooltip("{0} = 현재 라운드 번호. 예: \"라운드 {0}\" 또는 \"Round {0}\"")]
        [SerializeField] private string roundFormat = "라운드 {0}";

        [SerializeField] private TMP_Text deckCountText;
        [Tooltip("{0} = 덱에 남은 패 수.")]
        [SerializeField] private string deckCountFormat = "남은 패: {0}";

        [Header("Player Scores")]
        [Tooltip("각 플레이어 점수 텍스트 배열. 배열 크기는 플레이어 수와 일치시킴.")]
        [SerializeField] private TMP_Text[] playerScoreTexts;
        [Tooltip("{0} = 플레이어 번호(1부터), {1} = 현재 점수, {2} = 승리 점수. 예: \"P{0}: {1}/{2}\"")]
        [SerializeField] private string playerScoreFormat = "P{0}: {1}";
        [Tooltip("사용자 플레이어(index 0 보통)는 다른 포맷 사용. 비워두면 playerScoreFormat 공통 사용.")]
        [SerializeField] private string userScoreFormat = "나: {1}";

        [Header("Player Names / Portraits (좌석 순서: 배열 index = 좌석)")]
        [Tooltip("각 좌석의 이름 텍스트. 닉네임 / BOT1, BOT2 ... 표시.")]
        [SerializeField] private TMP_Text[] playerNameTexts;
        [Tooltip("{0} = 이름. 예: \"{0}\"")]
        [SerializeField] private string playerNameFormat = "{0}";
        [Tooltip("내 이름 포맷. 비워두면 playerNameFormat. 예: \"{0} (나)\"")]
        [SerializeField] private string userNameFormat = "";
        [Tooltip("각 좌석의 프로필 초상화 Image. 선택한 캐릭터 이미지로 교체.")]
        [SerializeField] private Image[] playerPortraits;

        [Header("현재 턴 강조 (옵션)")]
        [Tooltip("현재 턴인 플레이어의 점수 텍스트 색상.")]
        [SerializeField] private Color activeTurnColor = new Color(1f, 0.85f, 0.2f);
        [Tooltip("일반 색상.")]
        [SerializeField] private Color normalColor = Color.white;
        [Tooltip("활성화 시 현재 턴인 플레이어의 점수가 색상으로 강조됨.")]
        [SerializeField] private bool highlightCurrentTurn = true;

        // Dirty check 캐시
        private int _lastRound = -1;
        private int _lastDeckCount = -1;
        private int[] _lastScores;
        private int _lastCurrentTurn = -1;
        private string[] _lastNames;
        private int[] _lastChars;

        private void Start()
        {
            if (gameManager == null)
                gameManager = FindObjectOfType<CardGameManager>();

            if (gameManager == null)
            {
                Debug.LogWarning("[InGameHUD] CardGameManager를 찾을 수 없음.");
                return;
            }

            _lastScores = new int[gameManager.PlayerCount];
            for (int i = 0; i < _lastScores.Length; i++) _lastScores[i] = -1;
            _lastNames = new string[Mathf.Max(4, gameManager.PlayerCount)];
            _lastChars = new int[_lastNames.Length];
            for (int i = 0; i < _lastChars.Length; i++) _lastChars[i] = int.MinValue;

            Refresh();
        }

        private void Update()
        {
            if (gameManager == null) return;
            Refresh();
        }

        private void Refresh()
        {
            // --- Round ---
            int currentRound = gameManager.RoundNumber;
            if (currentRound != _lastRound)
            {
                _lastRound = currentRound;
                if (roundText != null)
                    roundText.text = string.Format(roundFormat, currentRound);
            }

            // --- Deck count ---
            int deckCount = (gameManager.Deck != null) ? gameManager.Deck.DrawPileCount : 0;
            if (deckCount != _lastDeckCount)
            {
                _lastDeckCount = deckCount;
                if (deckCountText != null)
                    deckCountText.text = string.Format(deckCountFormat, deckCount);
            }

            RefreshIdentities();

            // --- Player scores + 턴 강조 ---
            var players = gameManager.Players;
            if (playerScoreTexts != null && players != null)
            {
                int winningScore = gameManager.WinningScore;
                int currentTurn = gameManager.CurrentPlayerIndex;
                int userIdx = gameManager.UserPlayerIndex;

                bool turnChanged = (currentTurn != _lastCurrentTurn);
                _lastCurrentTurn = currentTurn;

                int count = Mathf.Min(playerScoreTexts.Length, players.Count);
                for (int i = 0; i < count; i++)
                {
                    var player = players[i];
                    var txt = playerScoreTexts[i];
                    if (player == null || txt == null) continue;

                    int score = player.TotalScore;
                    bool scoreChanged = (_lastScores == null || i >= _lastScores.Length) || (_lastScores[i] != score);

                    if (scoreChanged)
                    {
                        if (_lastScores != null && i < _lastScores.Length)
                            _lastScores[i] = score;

                        // 포맷 결정 (사용자 플레이어는 별도 포맷 사용)
                        string format = (i == userIdx && !string.IsNullOrEmpty(userScoreFormat))
                            ? userScoreFormat
                            : playerScoreFormat;

                        txt.text = string.Format(format, i + 1, score, winningScore);
                    }

                    // 턴 강조
                    if (highlightCurrentTurn && turnChanged)
                    {
                        txt.color = (i == currentTurn) ? activeTurnColor : normalColor;
                        if (playerNameTexts != null && i < playerNameTexts.Length && playerNameTexts[i] != null)
                            playerNameTexts[i].color = (i == currentTurn) ? activeTurnColor : normalColor;
                    }
                }
            }
        }

        /// <summary>좌석별 이름·초상화 — 바뀐 경우만 갱신 (플레이어 생성 전에는 건너뜀).</summary>
        private void RefreshIdentities()
        {
            var players = gameManager.Players;
            if (players == null || players.Count == 0 || _lastNames == null) return;
            int userIdx = gameManager.UserPlayerIndex;
            var db = CharacterDatabase.Instance;

            for (int i = 0; i < players.Count && i < _lastNames.Length; i++)
            {
                var p = players[i];
                if (p == null) continue;

                if (playerNameTexts != null && i < playerNameTexts.Length && playerNameTexts[i] != null && _lastNames[i] != p.DisplayName)
                {
                    _lastNames[i] = p.DisplayName;
                    string fmt = (i == userIdx && !string.IsNullOrEmpty(userNameFormat)) ? userNameFormat : playerNameFormat;
                    playerNameTexts[i].text = string.Format(fmt, p.DisplayName);
                }

                if (playerPortraits != null && i < playerPortraits.Length && playerPortraits[i] != null && db != null && _lastChars[i] != p.CharacterIndex)
                {
                    _lastChars[i] = p.CharacterIndex;
                    var def = db.Get(p.CharacterIndex);
                    if (def != null && def.portrait != null) playerPortraits[i].sprite = def.portrait;
                }
            }
        }
    }
}

using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

namespace CruceChaos.Unity
{
    /// <summary>
    /// Tracks VP scores across rounds and decides when the match is over.
    ///
    /// Scoring (mirrors StandardRoundScorer):
    ///   - Both teams earn VP = their totalPoints / 33 independently.
    ///   - The bidding team: if they earned >= bid → keep earned VP;
    ///     if they missed → subtract the bid instead.
    ///   - Non-bidding team: always keeps earned VP.
    ///
    /// Match ends when either player reaches _targetMatchPoints AND leads by >= 2.
    /// </summary>
    public class MatchManager : MonoBehaviour
    {
        [Header("Match Settings")]
        [SerializeField] private int _targetMatchPoints = 11;

        [Header("HUD — Match Score Labels")]
        [SerializeField] private TextMeshProUGUI _humanMatchScoreLabel;
        [SerializeField] private TextMeshProUGUI _aiMatchScoreLabel;

        [Header("Match Over Panel")]
        [SerializeField] private GameObject      _matchOverPanel;
        [SerializeField] private TextMeshProUGUI _matchOverLabel;

        [Header("Scene Navigation")]
        [SerializeField] private string _mainMenuScene = "Main Menu";

        private int _humanMatchScore;
        private int _aiMatchScore;

        /// <summary>True after the match-over condition is reached.</summary>
        public bool IsMatchOver { get; private set; }

        /// <summary>Current human VP score for display.</summary>
        public int HumanMatchScore => _humanMatchScore;

        /// <summary>Current AI VP score for display.</summary>
        public int AIMatchScore => _aiMatchScore;

        private void Start()
        {
            _humanMatchScore = 0;
            _aiMatchScore    = 0;
            IsMatchOver      = false;
            RefreshLabels();
            if (_matchOverPanel != null) _matchOverPanel.SetActive(false);
        }

        // ── Public API ─────────────────────────────────────────────────────────

        /// <summary>
        /// Call at the end of each round.
        ///
        /// humanTotalPts / aiTotalPts  = captured card pts + marriage pts
        /// winningBid                  = the bid the winner placed (null if no bid info)
        /// humanWonBid                 = true if the human was the bidding team
        /// </summary>
        public void ReportRoundResult(
            int  humanTotalPts,
            int  aiTotalPts,
            int? winningBid    = null,
            bool humanWonBid   = false)
        {
            int humanEarned = humanTotalPts / 33;
            int aiEarned    = aiTotalPts    / 33;

            int humanDelta;
            int aiDelta;

            if (winningBid.HasValue)
            {
                int bid = winningBid.Value;
                if (humanWonBid)
                {
                    // Human is the bidding team.
                    humanDelta = humanEarned >= bid ? humanEarned : -bid;
                    aiDelta    = aiEarned;
                }
                else
                {
                    // AI is the bidding team.
                    aiDelta    = aiEarned >= bid ? aiEarned : -bid;
                    humanDelta = humanEarned;
                }
            }
            else
            {
                // No bid info — fall back to simple earned VP.
                humanDelta = humanEarned;
                aiDelta    = aiEarned;
            }

            _humanMatchScore += humanDelta;
            _aiMatchScore    += aiDelta;

            Debug.Log($"[Match] Human +{humanDelta} ({humanTotalPts} pts) / " +
                      $"AI +{aiDelta} ({aiTotalPts} pts). " +
                      $"Standings: {_humanMatchScore} – {_aiMatchScore}");

            RefreshLabels();
            CheckMatchOver();
        }

        public void ResetMatch()
        {
            _humanMatchScore = 0;
            _aiMatchScore    = 0;
            IsMatchOver      = false;
            RefreshLabels();
            if (_matchOverPanel != null) _matchOverPanel.SetActive(false);
        }

        public void OnMainMenuClicked()
        {
            SceneManager.LoadScene(_mainMenuScene);
        }

        // ── Private ────────────────────────────────────────────────────────────

        private void CheckMatchOver()
        {
            int hi  = Mathf.Max(_humanMatchScore, _aiMatchScore);
            int lo  = Mathf.Min(_humanMatchScore, _aiMatchScore);
            bool targetReached = hi >= _targetMatchPoints;
            bool clearLead     = (hi - lo) >= 2;

            if (targetReached && clearLead)
            {
                IsMatchOver = true;
                ShowMatchOver();
            }
        }

        private void RefreshLabels()
        {
            if (_humanMatchScoreLabel != null)
                _humanMatchScoreLabel.text = $"You: {_humanMatchScore}";
            if (_aiMatchScoreLabel != null)
                _aiMatchScoreLabel.text = $"AI: {_aiMatchScore}";
        }

        private void ShowMatchOver()
        {
            if (_matchOverPanel == null) return;
            _matchOverPanel.SetActive(true);

            if (_matchOverLabel != null)
            {
                bool humanWins = _humanMatchScore > _aiMatchScore;
                _matchOverLabel.text = humanWins
                    ? $"You win the match!\n{_humanMatchScore} – {_aiMatchScore}"
                    : $"AI wins the match!\n{_humanMatchScore} – {_aiMatchScore}";
            }
        }
    }
}

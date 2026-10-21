using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

namespace CruceChaos.Unity
{
    /// <summary>
    /// Tracks the match-level score across multiple rounds (Cruce is played to
    /// a target number of rounds won, not just card points).
    ///
    /// In Cruce scoring, each round awards 1–6 points depending on the margin:
    ///   - Loser gets 0 pts  → winner +4 (clean sweep, "rupt")
    ///   - Loser gets 1–30   → winner +3
    ///   - Loser gets 31–60  → winner +2
    ///   - Loser gets 61+    → winner +1
    ///
    /// The first player to reach <see cref="_targetMatchPoints"/> wins the match.
    ///
    /// SCENE SETUP:
    ///   Add this component to the same GameObject as GameManager (or a
    ///   persistent singleton object).
    ///   Wire the labels in the Inspector.
    ///   The GameManager calls <see cref="ReportRoundResult"/> after each round.
    /// </summary>
    public class MatchManager : MonoBehaviour
    {
        [Header("Match Settings")]
        [Tooltip("First player to reach this many match points wins the whole match.")]
        [SerializeField] private int _targetMatchPoints = 7;

        [Header("HUD References")]
        [SerializeField] private TextMeshProUGUI _humanMatchScoreLabel;
        [SerializeField] private TextMeshProUGUI _aiMatchScoreLabel;
        [SerializeField] private GameObject _matchOverPanel;
        [SerializeField] private TextMeshProUGUI _matchOverLabel;

        [Header("Scene Navigation")]
        [SerializeField] private string _mainMenuScene = "Main Menu";

        // Match-level scores (these persist for the whole match).
        private int _humanMatchScore;
        private int _aiMatchScore;

        private void Start()
        {
            _humanMatchScore = 0;
            _aiMatchScore    = 0;
            RefreshLabels();
            if (_matchOverPanel != null) _matchOverPanel.SetActive(false);
        }

        // ── Public API ─────────────────────────────────────────────────────────

        /// <summary>
        /// Called by GameManager at the end of each round with the two players'
        /// final card-point totals (out of 120).
        /// </summary>
        public void ReportRoundResult(int humanCardPts, int aiCardPts)
        {
            int humanDelta = 0;
            int aiDelta    = 0;

            if (humanCardPts > aiCardPts)
                humanDelta = PointDelta(loserPts: aiCardPts);
            else if (aiCardPts > humanCardPts)
                aiDelta = PointDelta(loserPts: humanCardPts);
            // Draw: no match points awarded (extremely rare — only if both = 60).

            _humanMatchScore += humanDelta;
            _aiMatchScore    += aiDelta;

            Debug.Log($"[Match] Round result: Human +{humanDelta} / AI +{aiDelta}. " +
                      $"Match score: Human {_humanMatchScore} – AI {_aiMatchScore}");

            RefreshLabels();

            if (_humanMatchScore >= _targetMatchPoints || _aiMatchScore >= _targetMatchPoints)
                ShowMatchOver();
        }

        /// <summary>Resets the match score and hides the match-over panel.</summary>
        public void ResetMatch()
        {
            _humanMatchScore = 0;
            _aiMatchScore    = 0;
            RefreshLabels();
            if (_matchOverPanel != null) _matchOverPanel.SetActive(false);
        }

        // Called by "Back to Menu" button on the match-over panel.
        public void OnMainMenuClicked()
        {
            SceneManager.LoadScene(_mainMenuScene);
        }

        // ── Private helpers ────────────────────────────────────────────────────

        /// <summary>
        /// Returns how many match points the winner earns, based on the loser's
        /// card-point total.
        /// </summary>
        private static int PointDelta(int loserPts)
        {
            if (loserPts == 0)  return 4; // rupt — clean sweep
            if (loserPts <= 30) return 3;
            if (loserPts <= 60) return 2;
            return 1;
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
                bool humanWins = _humanMatchScore >= _targetMatchPoints;
                _matchOverLabel.text = humanWins
                    ? $"You win the match!\n{_humanMatchScore} – {_aiMatchScore}"
                    : $"AI wins the match!\n{_humanMatchScore} – {_aiMatchScore}";
            }
        }
    }
}

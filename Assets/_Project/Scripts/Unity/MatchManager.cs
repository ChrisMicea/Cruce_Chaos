using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

namespace CruceChaos.Unity
{
    /// <summary>
    /// Tracks per-team match VP across rounds and decides when the match is over.
    ///
    /// This class does NOT compute scoring — it only accumulates the per-team
    /// deltas handed to it (produced by Core's StandardRoundScorer) and applies
    /// the match-end rule: the leading team is at or past the target AND at
    /// least 2 clear of the runner-up (mirrors Core's MatchEngine.IsMatchOver).
    ///
    /// Scores are keyed by team index, so this works for any player/team count.
    /// The two serialized labels show the human's team and the opposing side
    /// (for 1v1 / 2v2); the N-player scoreboard is layered on in a later step.
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

        /// <summary>Per-team cumulative match score, keyed by team index.</summary>
        private readonly Dictionary<int, int> _teamScores = new Dictionary<int, int>();

        /// <summary>The team index the human belongs to (for win/lose wording).</summary>
        private int _myTeam;

        /// <summary>
        /// Whether this match is team play (2v2) rather than individual (1v1).
        /// Only affects HUD wording: individual play keeps "You" / "AI", team
        /// play shows "Your Team" / "Opponents".
        /// </summary>
        private bool _isTeamPlay;

        private string MyLabel  => _isTeamPlay ? "Your Team" : "You";
        private string OppLabel => _isTeamPlay ? "Opponents" : "AI";

        /// <summary>True after the match-over condition is reached.</summary>
        public bool IsMatchOver { get; private set; }

        // ── Setup (called by GameManager at startup) ─────────────────────────

        /// <summary>
        /// Registers the teams in this match and which one is the human's, so
        /// scores can be tracked per team and the match-over wording knows who
        /// "you" are. Call once before the first round.
        /// </summary>
        public void SetTeams(IEnumerable<int> teamIndices, int myTeam, bool isTeamPlay)
        {
            _teamScores.Clear();
            foreach (int t in teamIndices)
                _teamScores[t] = 0;

            _myTeam     = myTeam;
            _isTeamPlay = isTeamPlay;
            IsMatchOver = false;
            RefreshLabels();
            if (_matchOverPanel != null) _matchOverPanel.SetActive(false);
        }

        // ── Queries ──────────────────────────────────────────────────────────

        /// <summary>Current match score of a specific team (0 if unknown).</summary>
        public int TeamScore(int team)
            => _teamScores.TryGetValue(team, out int s) ? s : 0;

        /// <summary>Sum of every team's score except <paramref name="team"/>.</summary>
        public int OpponentsScore(int team)
            => _teamScores.Where(kv => kv.Key != team).Sum(kv => kv.Value);

        // ── Public API ───────────────────────────────────────────────────────

        /// <summary>
        /// Call at the end of each round with Core's per-team VP deltas
        /// (StandardRoundScorer output). This class simply accumulates them —
        /// it does not recompute the formula.
        /// </summary>
        public void ReportRoundResult(IReadOnlyDictionary<int, int> teamDeltas)
        {
            foreach (KeyValuePair<int, int> kv in teamDeltas)
            {
                if (!_teamScores.ContainsKey(kv.Key))
                    _teamScores[kv.Key] = 0;
                _teamScores[kv.Key] += kv.Value;
            }

            string standings = string.Join("  |  ",
                _teamScores.OrderByDescending(kv => kv.Value)
                           .Select(kv => $"T{kv.Key}: {kv.Value}"));
            Debug.Log($"[Match] Round applied. Standings: {standings}");

            RefreshLabels();
            CheckMatchOver();
        }

        public void ResetMatch()
        {
            foreach (int t in _teamScores.Keys.ToList())
                _teamScores[t] = 0;
            IsMatchOver = false;
            RefreshLabels();
            if (_matchOverPanel != null) _matchOverPanel.SetActive(false);
        }

        public void OnMainMenuClicked()
        {
            SceneManager.LoadScene(_mainMenuScene);
        }

        // ── Private ──────────────────────────────────────────────────────────

        /// <summary>
        /// Match ends when the leading team is at/past the target AND at least 2
        /// clear of the runner-up (a 1-point gap keeps play going). Same rule as
        /// Core's MatchEngine.IsMatchOver, generalized to any number of teams.
        /// </summary>
        private void CheckMatchOver()
        {
            if (_teamScores.Count == 0) return;

            var ranked = _teamScores.OrderByDescending(kv => kv.Value).ToList();
            int leaderScore   = ranked[0].Value;
            int runnerUpScore = ranked.Count > 1 ? ranked[1].Value : int.MinValue;

            if (leaderScore >= _targetMatchPoints && (leaderScore - runnerUpScore) >= 2)
            {
                IsMatchOver = true;
                ShowMatchOver(ranked[0].Key);
            }
        }

        private void RefreshLabels()
        {
            if (_humanMatchScoreLabel != null)
                _humanMatchScoreLabel.text = $"{MyLabel}: {TeamScore(_myTeam)}";
            if (_aiMatchScoreLabel != null)
                _aiMatchScoreLabel.text = $"{OppLabel}: {OpponentsScore(_myTeam)}";
        }

        private void ShowMatchOver(int winningTeam)
        {
            if (_matchOverPanel == null) return;
            _matchOverPanel.SetActive(true);

            if (_matchOverLabel != null)
            {
                bool humanWins = winningTeam == _myTeam;
                string winner  = humanWins
                    ? (_isTeamPlay ? "Your team wins" : "You win")
                    : (_isTeamPlay ? "Opponents win"  : "AI wins");
                _matchOverLabel.text =
                    $"{winner} the match!\n{TeamScore(_myTeam)} – {OpponentsScore(_myTeam)}";
            }
        }
    }
}

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
    /// Scores and labels are per-team (one slot per team), so this works for any
    /// player/team count — 2 for 1v1 / 2v2, 3 for 1v1v1, 4 for free-for-all.
    /// GameManager registers the slots (team index + display name) at startup,
    /// the SAME names the ScoreHUD uses, so the two HUDs stay consistent.
    /// </summary>
    public class MatchManager : MonoBehaviour
    {
        [Header("Match Settings")]
        [SerializeField] private int _targetMatchPoints = 11;

        [Header("HUD — Match Score Labels")]
        [Tooltip("One match-score label per team/player, in the SAME order as the " +
                 "ScoreHUD's Score Labels: element 0 = you, then opponents in team " +
                 "order. Wire 2 for 1v1/2v2, 3 for 1v1v1, 4 for free-for-all. " +
                 "Extra labels are hidden automatically.")]
        [SerializeField] private TextMeshProUGUI[] _matchScoreLabels;

        [Header("Match Over Panel")]
        [SerializeField] private GameObject      _matchOverPanel;
        [SerializeField] private TextMeshProUGUI _matchOverLabel;

        [Header("Scene Navigation")]
        [SerializeField] private string _mainMenuScene = "Main Menu";

        /// <summary>Per-team cumulative match score, keyed by team index.</summary>
        private readonly Dictionary<int, int> _teamScores = new Dictionary<int, int>();

        /// <summary>Scoreboard slots (team index + display name), in label order.</summary>
        private ScoreHUD.Slot[] _slots = System.Array.Empty<ScoreHUD.Slot>();
        private readonly Dictionary<int, int> _slotOfTeam = new Dictionary<int, int>();

        /// <summary>The team index the human belongs to (for win/lose wording).</summary>
        private int _myTeam;

        /// <summary>True after the match-over condition is reached.</summary>
        public bool IsMatchOver { get; private set; }

        // ── Setup (called by GameManager at startup) ─────────────────────────

        /// <summary>
        /// Registers the scoreboard slots (one per team, SAME names/order as the
        /// ScoreHUD) and which team is the human's. Binds slots to the serialized
        /// labels by position; extra labels are hidden. Call once before the
        /// first round.
        /// </summary>
        public void SetScoreboard(ScoreHUD.Slot[] slots, int myTeam)
        {
            _slots = slots ?? System.Array.Empty<ScoreHUD.Slot>();
            _myTeam = myTeam;

            _teamScores.Clear();
            _slotOfTeam.Clear();
            for (int i = 0; i < _slots.Length; i++)
            {
                _teamScores[_slots[i].TeamIndex] = 0;
                _slotOfTeam[_slots[i].TeamIndex] = i;
            }

            IsMatchOver = false;
            RefreshLabels();
            if (_matchOverPanel != null) _matchOverPanel.SetActive(false);
        }

        // ── Queries ──────────────────────────────────────────────────────────

        /// <summary>Current match score of a specific team (0 if unknown).</summary>
        public int TeamScore(int team)
            => _teamScores.TryGetValue(team, out int s) ? s : 0;

        private string NameOfTeam(int team)
            => _slotOfTeam.TryGetValue(team, out int i) ? _slots[i].Name : $"T{team}";

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
                           .Select(kv => $"{NameOfTeam(kv.Key)}: {kv.Value}"));
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

        /// <summary>
        /// Updates each team's match-score label by slot position. Labels beyond
        /// the slot count are hidden; slots beyond the label count have no
        /// on-screen readout.
        /// </summary>
        private void RefreshLabels()
        {
            if (_matchScoreLabels == null) return;
            for (int i = 0; i < _matchScoreLabels.Length; i++)
            {
                if (_matchScoreLabels[i] == null) continue;
                bool used = i < _slots.Length;
                _matchScoreLabels[i].gameObject.SetActive(used);
                if (used)
                    _matchScoreLabels[i].text =
                        $"{_slots[i].Name}: {TeamScore(_slots[i].TeamIndex)}";
            }
        }

        private void ShowMatchOver(int winningTeam)
        {
            if (_matchOverPanel == null) return;
            _matchOverPanel.SetActive(true);

            if (_matchOverLabel == null) return;

            // Standings line: every team's final score, highest first.
            string standings = string.Join("   ",
                _teamScores.OrderByDescending(kv => kv.Value)
                           .Select(kv => $"{NameOfTeam(kv.Key)} {kv.Value}"));

            string headline = winningTeam == _myTeam
                ? "You win the match!"
                : $"{NameOfTeam(winningTeam)} wins the match!";

            _matchOverLabel.text = $"{headline}\n{standings}";
        }
    }
}

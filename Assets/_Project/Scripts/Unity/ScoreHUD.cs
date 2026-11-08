using UnityEngine;
using TMPro;
using CruceChaos.Core.Cards;
using CruceChaos.Core.Game.Scoring;

namespace CruceChaos.Unity
{
    /// <summary>
    /// Updates the on-screen HUD: card points, draw pile count, and trump suit.
    ///
    /// SCENE SETUP:
    ///   Create a Canvas child "HUD" and add this component.
    ///   Wire the TextMeshPro labels and the trump icon image in the Inspector.
    ///
    ///   Suggested layout:
    ///     Top-left:   "Your Points: 0 / 120"
    ///     Top-right:  "AI Points: 0 / 120"
    ///     Bottom-right: "Deck: 8 cards"
    ///     Centre-top:  trump icon (same sprites as TrickAreaView)
    /// </summary>
    public class ScoreHUD : MonoBehaviour
    {
        [Header("Score Labels")]
        [Tooltip("One running-score label per team/player, in team-index order. " +
                 "Wire as many as the mode needs: 2 for 1v1 and 2v2, 3 for " +
                 "1v1v1, 4 for free-for-all. Extra labels are hidden automatically.")]
        [SerializeField] private TextMeshProUGUI[] _scoreLabels;

        [Header("Draw Pile")]
        [SerializeField] private TextMeshProUGUI _deckLabel;

        [Header("Trump Display")]
        [SerializeField] private UnityEngine.UI.Image _trumpIcon;
        [SerializeField] private TextMeshProUGUI _trumpLabel;

        [Tooltip("Sprites for each suit in order: Green, Red, Acorn, Bell.")]
        [SerializeField] private Sprite[] _suitIcons; // indexed by (int)Suit

        [Header("Round Result Banner")]
        [SerializeField] private GameObject _resultBanner;
        [SerializeField] private TextMeshProUGUI _resultLabel;

        [Header("Persistent Info Label")]
        [Tooltip("A label shown centre-screen for bid winner / trump announcements. Stays until ClearPersistentInfo() is called.")]
        [SerializeField] private TextMeshProUGUI _persistentInfoLabel;

        // ---- Scoreboard model -------------------------------------------------

        /// <summary>
        /// Describes one scoreboard slot: a team and the name to show for it.
        /// GameManager builds one per team at startup ("You", "Opponent A",
        /// "Your Team", ...), in team-index order.
        /// </summary>
        public struct Slot
        {
            public int TeamIndex;
            public string Name;

            public Slot(int teamIndex, string name)
            {
                TeamIndex = teamIndex;
                Name      = name;
            }
        }

        private Slot[] _slots = System.Array.Empty<Slot>();
        // team index -> position in _slots / _scoreLabels
        private readonly System.Collections.Generic.Dictionary<int, int> _slotOfTeam
            = new System.Collections.Generic.Dictionary<int, int>();

        /// <summary>
        /// Registers the scoreboard slots (one per team) and binds them to the
        /// serialized labels by position. Call once at startup. Labels beyond
        /// the slot count are hidden; slots beyond the label count simply have
        /// no on-screen readout.
        /// </summary>
        public void SetScoreboard(Slot[] slots)
        {
            _slots = slots ?? System.Array.Empty<Slot>();
            _slotOfTeam.Clear();
            for (int i = 0; i < _slots.Length; i++)
                _slotOfTeam[_slots[i].TeamIndex] = i;

            if (_scoreLabels != null)
            {
                for (int i = 0; i < _scoreLabels.Length; i++)
                {
                    if (_scoreLabels[i] == null) continue;
                    bool used = i < _slots.Length;
                    _scoreLabels[i].gameObject.SetActive(used);
                    if (used) _scoreLabels[i].text = $"{_slots[i].Name}: 0";
                }
            }
        }

        /// <summary>The display name registered for a team (empty if unknown).</summary>
        private string NameOfTeam(int teamIndex)
            => _slotOfTeam.TryGetValue(teamIndex, out int i) ? _slots[i].Name : $"T{teamIndex}";

        // ---- Public API -------------------------------------------------------

        /// <summary>Updates one team's running captured-point total on the HUD.</summary>
        public void SetTeamPoints(int teamIndex, int pts)
        {
            if (_scoreLabels == null) return;
            if (!_slotOfTeam.TryGetValue(teamIndex, out int i)) return;
            if (i < _scoreLabels.Length && _scoreLabels[i] != null)
                _scoreLabels[i].text = $"{_slots[i].Name}: {pts}";
        }

        /// <summary>Updates the draw pile card count label.</summary>
        public void SetDeckCount(int count)
        {
            if (_deckLabel != null)
                _deckLabel.text = count > 0 ? $"Deck: {count}" : "Deck: empty";
        }

        /// <summary>
        /// Updates the trump suit display.
        /// Pass null before trump has been established.
        /// </summary>
        public void SetTrump(Suit? trump)
        {
            string suitName = trump.HasValue ? SuitDisplayName(trump.Value) : "—";

            if (_trumpLabel != null)
                _trumpLabel.text = $"Trump: {suitName}";

            if (_trumpIcon != null)
            {
                if (trump.HasValue && _suitIcons != null && _suitIcons.Length > (int)trump.Value)
                {
                    _trumpIcon.sprite  = _suitIcons[(int)trump.Value];
                    _trumpIcon.enabled = true;
                }
                else
                {
                    _trumpIcon.enabled = false;
                }
            }
        }

        /// <summary>
        /// One team's end-of-round figures for the result banner.
        /// </summary>
        public struct ResultRow
        {
            public int TeamIndex;
            public int TotalPoints;   // captured + marriage this round
            public int VPDelta;       // match-score change this round
            public int MatchScore;    // running match score after this round
            public bool WonBid;       // true if this team was the bidding side

            public ResultRow(int teamIndex, int totalPoints, int vpDelta, int matchScore, bool wonBid)
            {
                TeamIndex   = teamIndex;
                TotalPoints = totalPoints;
                VPDelta     = vpDelta;
                MatchScore  = matchScore;
                WonBid      = wonBid;
            }
        }

        /// <summary>
        /// Shows the end-of-round result banner for any number of teams: one
        /// block per team (points, VP change, running match score) plus the bid
        /// outcome. Rows are shown in the order given.
        /// </summary>
        public void ShowResult(ResultRow[] rows, int? winningBid)
        {
            if (_resultBanner == null) return;
            _resultBanner.SetActive(true);
            if (_resultLabel == null || rows == null) return;

            var lines = new System.Collections.Generic.List<string>();

            // One line per team: "Name: 95 pts  (+2 VP)  match 7"
            foreach (ResultRow r in rows)
            {
                string delta = r.VPDelta >= 0 ? $"+{r.VPDelta}" : $"{r.VPDelta}";
                lines.Add($"{NameOfTeam(r.TeamIndex)}: {r.TotalPoints} pts   ({delta} VP)   match {r.MatchScore}");
            }

            // Bid outcome line.
            if (winningBid.HasValue)
            {
                int bid = winningBid.Value;
                foreach (ResultRow r in rows)
                {
                    if (!r.WonBid) continue;
                    int vp = StandardRoundScorer.VictoryPointsFor(r.TotalPoints);
                    string outcome = vp >= bid ? "made" : "missed";
                    lines.Add($"Bid: {NameOfTeam(r.TeamIndex)} bid {bid} — {outcome} ({vp} VP earned)");
                    break;
                }
            }

            _resultLabel.text = string.Join("\n", lines);
        }

        /// <summary>
        /// Shows a message that stays visible until ClearPersistentInfo() is called.
        /// Use for bid winner and trump declaration announcements.
        /// </summary>
        public void ShowPersistentInfo(string message)
        {
            if (_persistentInfoLabel != null)
            {
                _persistentInfoLabel.text    = message;
                _persistentInfoLabel.enabled = true;
            }
        }

        /// <summary>Clears the persistent info label.</summary>
        public void ClearPersistentInfo()
        {
            if (_persistentInfoLabel != null)
                _persistentInfoLabel.enabled = false;
        }

        /// <summary>
        /// Shows a temporary bid status message (e.g. "You bid 3", "AI passed").
        /// If no label is wired, falls back to a Debug.Log.
        /// </summary>
        public void ShowBidInfo(string message)
        {
            // Reuse the trump label as a temporary message area if no dedicated label exists.
            if (_trumpLabel != null)
                StartCoroutine(TempMessage(_trumpLabel, message, 1.2f));
            else
                Debug.Log("[BidInfo] " + message);
        }

        /// <summary>
        /// Flashes a marriage bonus notification ("+40" or "+20").
        /// </summary>
        public void ShowMarriageBonus(int bonus)
        {
            if (_trumpLabel != null)
                StartCoroutine(TempMessage(_trumpLabel, $"Marriage +{bonus}!", 1.5f));
        }

        private System.Collections.IEnumerator TempMessage(TMPro.TextMeshProUGUI label, string msg, float duration)
        {
            string prev = label.text;
            label.text = msg;
            yield return new UnityEngine.WaitForSeconds(duration);
            label.text = prev;
        }

        /// <summary>Hides the result banner (call at the start of a new round).</summary>
        public void HideResult()
        {
            if (_resultBanner != null)
                _resultBanner.SetActive(false);
        }

        // ---- Helpers ----------------------------------------------------------

        private static string SuitDisplayName(Suit suit)
        {
            switch (suit)
            {
                case Suit.Green: return "Verde";
                case Suit.Red:   return "Rosu";
                case Suit.Acorn: return "Ghinda";
                case Suit.Bell:  return "Duba";
                default:         return suit.ToString();
            }
        }
    }
}

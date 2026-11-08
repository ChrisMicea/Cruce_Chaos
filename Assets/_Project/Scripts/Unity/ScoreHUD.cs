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
        [SerializeField] private TextMeshProUGUI _humanPointsLabel;
        [SerializeField] private TextMeshProUGUI _aiPointsLabel;

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

        // ---- Team-play wording ------------------------------------------------

        /// <summary>
        /// Whether to use team wording. Set by <c>GameManager</c> at startup:
        /// 1v1 keeps "You" / "AI", 2v2 shows "Your Team" / "Opponents". The two
        /// score slots always mean "the human's side" and "the opposing side",
        /// so in 2v2 they carry the human's TEAM total vs the opponents' total.
        /// </summary>
        private bool _isTeamPlay;

        /// <summary>Tell the HUD whether to use team wording. Call once at startup.</summary>
        public void SetTeamPlay(bool isTeamPlay) => _isTeamPlay = isTeamPlay;

        private string MyLabel  => _isTeamPlay ? "Your Team" : "You";
        private string OppLabel => _isTeamPlay ? "Opponents" : "AI";

        // ---- Public API -------------------------------------------------------

        /// <summary>Updates the human side's captured card-point total.</summary>
        public void SetHumanPoints(int pts)
        {
            if (_humanPointsLabel != null)
                _humanPointsLabel.text = _isTeamPlay ? $"Your Team: {pts}" : $"Your Points: {pts}";
        }

        /// <summary>Updates the opposing side's captured card-point total.</summary>
        public void SetAIPoints(int pts)
        {
            if (_aiPointsLabel != null)
                _aiPointsLabel.text = $"{OppLabel} Points: {pts}";
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
        /// Shows the end-of-round result banner with full scoring breakdown:
        /// card points, bid outcome, VP delta for each side, and running match scores.
        /// </summary>
        public void ShowResult(
            int  humanCardPts,   int aiCardPts,
            int  humanTotal,     int aiTotal,
            int? winningBid,     bool humanWonBid,
            int  humanVPDelta,   int aiVPDelta,
            int  humanMatchScore, int aiMatchScore)
        {
            if (_resultBanner == null) return;
            _resultBanner.SetActive(true);

            if (_resultLabel == null) return;

            // ── Line 1: card points ──────────────────────────────────────────
            string pointsLine = $"{MyLabel}: {humanTotal} pts     {OppLabel}: {aiTotal} pts";

            // ── Line 2: bid outcome ──────────────────────────────────────────
            string bidLine = "";
            if (winningBid.HasValue)
            {
                int bid         = winningBid.Value;
                string bidder   = humanWonBid ? MyLabel : OppLabel;
                int bidderTotal = humanWonBid ? humanTotal : aiTotal;
                int bidderVP    = StandardRoundScorer.VictoryPointsFor(bidderTotal);
                bool made       = bidderVP >= bid;
                string outcome  = made ? "made" : "missed";
                bidLine = $"Bid: {bidder} bid {bid} — {outcome} ({bidderVP} VP earned)";
            }

            // ── Line 3: VP gained ────────────────────────────────────────────
            string humanDeltaStr = humanVPDelta >= 0 ? $"+{humanVPDelta}" : $"{humanVPDelta}";
            string aiDeltaStr    = aiVPDelta    >= 0 ? $"+{aiVPDelta}"    : $"{aiVPDelta}";
            string vpLine        = $"VP:  {MyLabel} {humanDeltaStr}     {OppLabel} {aiDeltaStr}";

            // ── Line 4: running match score ──────────────────────────────────
            string standingsLine = $"Match:  {MyLabel} {humanMatchScore}  –  {OppLabel} {aiMatchScore}";

            _resultLabel.text = string.Join("\n", pointsLine, bidLine, vpLine, standingsLine);
        }

        /// <summary>Simple overload — used when match manager is not assigned.</summary>
        public void ShowResult(int humanPts, int aiPts)
        {
            if (_resultBanner == null) return;
            _resultBanner.SetActive(true);
            if (_resultLabel != null)
                _resultLabel.text = humanPts > aiPts
                    ? $"{MyLabel} win!\n{humanPts} – {aiPts}"
                    : aiPts > humanPts
                        ? $"{OppLabel} win!\n{humanPts} – {aiPts}"
                        : $"Draw!\n{humanPts} – {aiPts}";
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

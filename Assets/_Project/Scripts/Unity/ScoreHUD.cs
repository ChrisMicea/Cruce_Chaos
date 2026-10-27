using UnityEngine;
using TMPro;
using CruceChaos.Core.Cards;

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

        // ---- Public API -------------------------------------------------------

        /// <summary>Updates the human player's captured card-point total.</summary>
        public void SetHumanPoints(int pts)
        {
            if (_humanPointsLabel != null)
                _humanPointsLabel.text = $"Your Points: {pts}";
        }

        /// <summary>Updates the AI player's captured card-point total.</summary>
        public void SetAIPoints(int pts)
        {
            if (_aiPointsLabel != null)
                _aiPointsLabel.text = $"AI Points: {pts}";
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
        /// Shows the end-of-round result banner.
        /// </summary>
        public void ShowResult(int humanPts, int aiPts)
        {
            if (_resultBanner == null) return;

            _resultBanner.SetActive(true);

            if (_resultLabel != null)
            {
                string outcome;
                if (humanPts > aiPts)
                    outcome = $"You win!\n{humanPts} – {aiPts}";
                else if (aiPts > humanPts)
                    outcome = $"AI wins!\n{humanPts} – {aiPts}";
                else
                    outcome = $"Draw!\n{humanPts} – {aiPts}";

                _resultLabel.text = outcome;
            }
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

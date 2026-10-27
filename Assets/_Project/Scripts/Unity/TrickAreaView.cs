using UnityEngine;
using UnityEngine.UI;
using CruceChaos.Core.Cards;

namespace CruceChaos.Unity
{
    /// <summary>
    /// Manages the two card slots in the centre of the table that display the
    /// cards played in the current trick.
    ///
    /// SCENE SETUP:
    ///   Create a GameObject "TrickArea" with this component.
    ///   Add two child GameObjects "HumanSlot" and "AISlot", each with an
    ///   Image component — these are the card display slots.
    ///   Optionally add a "WinnerFlash" Image child that plays a brief colour
    ///   flash when the trick is resolved.
    /// </summary>
    public class TrickAreaView : MonoBehaviour
    {
        [Header("Trick Slots")]
        [Tooltip("Image that shows the card played by the human (bottom player).")]
        [SerializeField] private Image _humanSlot;

        [Tooltip("Image that shows the card played by the AI (top player).")]
        [SerializeField] private Image _aiSlot;

        [Header("Trump Indicator")]
        [Tooltip("Image that shows the trump suit icon (set via SetTrump).")]
        [SerializeField] private Image _trumpIcon;

        [Tooltip("Sprites for each suit in order: Green, Red, Acorn, Bell.")]
        [SerializeField] private Sprite[] _suitIcons; // 4 elements, indexed by (int)Suit

        [Header("Trick Winner Flash")]
        [SerializeField] private Image _winnerFlash;
        [SerializeField] private Color _winFlashColour  = new Color(1f, 0.85f, 0f, 0.6f);  // gold
        [SerializeField] private Color _loseFlashColour = new Color(0.7f, 0.2f, 0.2f, 0.6f); // red
        [SerializeField] private float _flashDuration   = 0.6f;

        // ---- Public API -------------------------------------------------------

        /// <summary>
        /// Shows a card sprite in the human player's trick slot.
        /// </summary>
        public void ShowHumanCard(Sprite sprite)
        {
            if (_humanSlot == null) return;
            _humanSlot.sprite  = sprite;
            _humanSlot.enabled = sprite != null;
        }

        /// <summary>
        /// Shows a card sprite in the AI player's trick slot.
        /// </summary>
        public void ShowAICard(Sprite sprite)
        {
            if (_aiSlot == null) return;
            _aiSlot.sprite  = sprite;
            _aiSlot.enabled = sprite != null;
        }

        /// <summary>
        /// Clears both trick slots (call at the start of each new trick).
        /// </summary>
        public void ClearTrick()
        {
            ShowHumanCard(null);
            ShowAICard(null);
        }

        /// <summary>
        /// Updates the trump indicator icon.
        /// Pass null to hide the indicator (no trump established yet).
        /// </summary>
        public void SetTrump(Suit? trump)
        {
            if (_trumpIcon == null) return;

            if (!trump.HasValue || _suitIcons == null || _suitIcons.Length < 4)
            {
                _trumpIcon.enabled = false;
                return;
            }

            int idx = (int)trump.Value;
            _trumpIcon.sprite  = _suitIcons[idx];
            _trumpIcon.enabled = true;
        }

        /// <summary>
        /// Plays a brief colour flash on the trick area.
        /// <paramref name="humanWon"/> true = gold (human wins), false = red (AI wins).
        /// </summary>
        public void FlashWinner(bool humanWon)
        {
            if (_winnerFlash == null) return;
            StopAllCoroutines();
            StartCoroutine(FlashRoutine(humanWon ? _winFlashColour : _loseFlashColour));
        }

        // ---- Private ----------------------------------------------------------

        private System.Collections.IEnumerator FlashRoutine(Color flashColour)
        {
            _winnerFlash.color   = flashColour;
            _winnerFlash.enabled = true;

            float elapsed = 0f;
            while (elapsed < _flashDuration)
            {
                elapsed += Time.deltaTime;
                float alpha = Mathf.Lerp(flashColour.a, 0f, elapsed / _flashDuration);
                _winnerFlash.color = new Color(flashColour.r, flashColour.g, flashColour.b, alpha);
                yield return null;
            }

            _winnerFlash.enabled = false;
        }
    }
}

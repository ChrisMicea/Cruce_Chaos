using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using CruceChaos.Core.Cards;

namespace CruceChaos.Unity
{
    /// <summary>
    /// Displays the cards played in the current trick as a single central pile.
    /// Each card is placed slightly offset from the previous so both are visible.
    ///
    /// SCENE SETUP (simplified — no child slots needed):
    ///   Create a GameObject "TrickArea" anywhere inside the Canvas.
    ///   Add this component to it.
    ///   Set its RectTransform to centre-anchor, size ~300x400.
    ///   Optionally add a child Image "WinnerFlash" (stretch to fill) for the
    ///   flash effect — assign it in the Inspector.
    ///
    ///   That's it. No HumanSlot / AISlot children required.
    /// </summary>
    public class TrickAreaView : MonoBehaviour
    {
        [Header("Card Display")]
        [Tooltip("Size of each card image spawned in the pile.")]
        [SerializeField] private Vector2 _cardSize = new Vector2(150f, 225f);

        [Tooltip("How much each successive card is offset from the previous (gives a fanned look).")]
        [SerializeField] private Vector2 _stackOffset = new Vector2(12f, -12f);

        [Header("Trump Indicator")]
        [Tooltip("Optional Image that shows the trump suit icon.")]
        [SerializeField] private Image _trumpIcon;

        [Tooltip("Sprites for each suit in order: Green, Red, Acorn, Bell.")]
        [SerializeField] private Sprite[] _suitIcons;

        [Header("Winner Flash")]
        [SerializeField] private Image _winnerFlash;
        [SerializeField] private Color _winFlashColour  = new Color(1f, 0.85f, 0f, 0.6f);
        [SerializeField] private Color _loseFlashColour = new Color(0.7f, 0.2f, 0.2f, 0.6f);
        [SerializeField] private float _flashDuration   = 0.8f;

        // Cards currently visible in the pile this trick.
        private readonly List<Image> _pileImages = new List<Image>();

        // ── Public API ─────────────────────────────────────────────────────────

        /// <summary>
        /// Adds a card face-up to the central pile.
        /// Call once per card played (human or AI — same method for both).
        /// </summary>
        public void AddCardToPile(Sprite sprite)
        {
            // Create a new Image as a child of this RectTransform.
            GameObject go = new GameObject("TrickCard", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(transform, false);

            RectTransform rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = _cardSize;

            // Stack offset: each successive card shifts slightly.
            int index = _pileImages.Count;
            rt.anchoredPosition = _stackOffset * index;

            // Assign the sprite.
            Image img = go.GetComponent<Image>();
            img.sprite = sprite;
            img.preserveAspect = true;

            // Ensure this card renders on top of previous ones.
            go.transform.SetAsLastSibling();

            _pileImages.Add(img);
        }

        /// <summary>
        /// Clears the pile (call at the start of each new trick).
        /// </summary>
        public void ClearTrick()
        {
            foreach (Image img in _pileImages)
                if (img != null) Destroy(img.gameObject);
            _pileImages.Clear();
        }

        /// <summary>
        /// Updates the trump suit icon. Pass null to hide it.
        /// </summary>
        public void SetTrump(Suit? trump)
        {
            if (_trumpIcon == null) return;

            if (!trump.HasValue || _suitIcons == null || _suitIcons.Length < 4)
            {
                _trumpIcon.enabled = false;
                return;
            }

            _trumpIcon.sprite  = _suitIcons[(int)trump.Value];
            _trumpIcon.enabled = true;
        }

        /// <summary>
        /// Flashes the trick area gold (human won) or red (AI won).
        /// </summary>
        public void FlashWinner(bool humanWon)
        {
            if (_winnerFlash == null) return;
            StopAllCoroutines();
            StartCoroutine(FlashRoutine(humanWon ? _winFlashColour : _loseFlashColour));
        }

        // ── Private ────────────────────────────────────────────────────────────

        private IEnumerator FlashRoutine(Color colour)
        {
            _winnerFlash.color   = colour;
            _winnerFlash.enabled = true;

            float elapsed = 0f;
            while (elapsed < _flashDuration)
            {
                elapsed += Time.deltaTime;
                float a = Mathf.Lerp(colour.a, 0f, elapsed / _flashDuration);
                _winnerFlash.color = new Color(colour.r, colour.g, colour.b, a);
                yield return null;
            }

            _winnerFlash.enabled = false;
        }
    }
}

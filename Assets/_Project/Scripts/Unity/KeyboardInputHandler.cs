using System.Collections.Generic;
using UnityEngine;
using CruceChaos.Core.Cards;

namespace CruceChaos.Unity
{
    /// <summary>
    /// Handles all keyboard input for the game scene. Attach to the GameManager
    /// object and wire the references in the Inspector.
    ///
    /// Bindings:
    ///   1–8        Select card by position in the fan (highlights it like hover)
    ///   Enter      Confirm selected card / confirm bid
    ///   Escape     Deselect current card
    ///   Tab        Increase bid value by 1 (wraps at MaxBid → Pass)
    ///   S          Toggle auto-sort
    ///   Space      Shuffle hand
    /// </summary>
    public class KeyboardInputHandler : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private GameManager            _gameManager;
        [SerializeField] private HumanPlayerController  _humanController;
        [SerializeField] private BidPanel               _bidPanel;

        // The index into the human hand that is currently keyboard-selected (-1 = none).
        private int _selectedCardIndex = -1;

        // All current CardViews — set each frame by GameManager via SetCardViews().
        private List<CardView> _cardViews = new List<CardView>();

        // Whether we are currently in the card-play phase and waiting for input.
        private bool _waitingForCard = false;

        // Whether we are currently in the bid phase.
        private bool _waitingForBid = false;

        // ── Public API called by GameManager ──────────────────────────────────

        /// <summary>
        /// Called by GameManager whenever the hand is rebuilt so we have an
        /// up-to-date reference to the CardViews.
        /// </summary>
        public void SetCardViews(List<CardView> views)
        {
            _cardViews = views;
            // If previously selected index is now out of range, clear it.
            if (_selectedCardIndex >= _cardViews.Count)
                ClearSelection();
        }

        /// <summary>Called by GameManager when it is the human's turn to play.</summary>
        public void BeginCardInput()
        {
            _waitingForCard = true;
            _selectedCardIndex = -1;
        }

        /// <summary>Called by GameManager when the card turn ends (played or cancelled).</summary>
        public void EndCardInput()
        {
            _waitingForCard = false;
            ClearSelection();
        }

        /// <summary>Called by GameManager when the bid panel is shown.</summary>
        public void BeginBidInput()  => _waitingForBid = true;

        /// <summary>Called by GameManager when bidding is done.</summary>
        public void EndBidInput()    => _waitingForBid = false;

        // ── Unity Update ──────────────────────────────────────────────────────

        private void Update()
        {
            HandleCardKeys();
            HandleBidKeys();
            HandleGlobalKeys();
        }

        // ── Card selection ────────────────────────────────────────────────────

        private void HandleCardKeys()
        {
            if (!_waitingForCard) return;

            // 1–8: select card at that position.
            for (int i = 0; i < 8; i++)
            {
                if (Input.GetKeyDown(KeyCode.Alpha1 + i) ||
                    Input.GetKeyDown(KeyCode.Keypad1 + i))
                {
                    TrySelectCard(i);
                    return;
                }
            }

            // Enter: confirm currently selected card.
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            {
                ConfirmCard();
                return;
            }

            // Escape: deselect.
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                ClearSelection();
            }
        }

        private void TrySelectCard(int index)
        {
            if (index < 0 || index >= _cardViews.Count) return;

            CardView cv = _cardViews[index];
            if (cv == null) return;

            // Check if the card is legal — CardView exposes this via its Button.
            UnityEngine.UI.Button btn = cv.GetComponent<UnityEngine.UI.Button>();
            if (btn != null && !btn.interactable) return;

            // Deselect previous.
            ClearSelection();

            _selectedCardIndex = index;

            // Use SetSelected so it lifts and gets the green tint.
            cv.SetSelected(true);
        }

        private void ConfirmCard()
        {
            if (_selectedCardIndex < 0 || _selectedCardIndex >= _cardViews.Count) return;

            CardView cv = _cardViews[_selectedCardIndex];
            if (cv == null) return;

            UnityEngine.UI.Button btn = cv.GetComponent<UnityEngine.UI.Button>();
            if (btn != null && !btn.interactable) return;

            // Fire the same path as a mouse click.
            _humanController.HandleCardClicked(cv.Card);

            _waitingForCard    = false;
            _selectedCardIndex = -1;
        }

        private void ClearSelection()
        {
            if (_selectedCardIndex >= 0 && _selectedCardIndex < _cardViews.Count)
            {
                CardView cv = _cardViews[_selectedCardIndex];
                if (cv != null) cv.SetSelected(false);
            }
            _selectedCardIndex = -1;
        }

        // ── Bid keys ──────────────────────────────────────────────────────────

        private void HandleBidKeys()
        {
            if (!_waitingForBid || _bidPanel == null) return;

            // Tab: increment the slider value by 1.
            if (Input.GetKeyDown(KeyCode.Tab))
            {
                _bidPanel.IncrementBid();
                return;
            }

            // Enter: confirm the current slider value as the bid.
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            {
                _bidPanel.ConfirmBid();
                return;
            }

            // Escape: pass.
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                _bidPanel.ConfirmPass();
            }
        }

        // ── Global keys ───────────────────────────────────────────────────────

        private void HandleGlobalKeys()
        {
            // S: toggle auto-sort.
            if (Input.GetKeyDown(KeyCode.S))
            {
                _gameManager?.OnAutoSortToggleClicked();
                return;
            }

            // Space: shuffle.
            if (Input.GetKeyDown(KeyCode.Space))
            {
                _gameManager?.OnShuffleHandClicked();
            }
        }
    }
}

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
    ///   ← / →      Move the selection cursor between cards (skips illegal ones)
    ///   1–8        Jump the cursor to the card at that position in the fan
    ///   Enter      Confirm the highlighted card / confirm bid
    ///   Escape     Deselect current card
    ///   Tab        Increase bid value by 1 (wraps at MaxBid → Pass)
    ///   S          Toggle auto-sort
    ///   Space      Shuffle hand
    ///
    /// Each card also shows its 1-based fan position as a number on top of it.
    /// </summary>
    public class KeyboardInputHandler : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private GameManager            _gameManager;
        [SerializeField] private HumanPlayerController  _humanController;
        [SerializeField] private BidPanel               _bidPanel;

        // The index into the human hand that is currently keyboard-selected (-1 = none).
        // This is the "committed" selection (green, lifts high) confirmed with Enter.
        private int _selectedCardIndex = -1;

        // The index the arrow-key cursor is pointing at (-1 = none). Visually a blue
        // highlight; pressing Enter confirms whatever the cursor is on.
        private int _cursorIndex = -1;

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

            // Keep the arrow-navigation cursor valid against the new layout.
            if (_waitingForCard)
                RefreshCursor();
            else
                _cursorIndex = -1;
        }

        /// <summary>Called by GameManager when it is the human's turn to play.</summary>
        public void BeginCardInput()
        {
            _waitingForCard = true;
            _selectedCardIndex = -1;

            // Place the cursor on the first legal card so arrows have a start point.
            _cursorIndex = -1;
            MoveCursor(+1);
        }

        /// <summary>Called by GameManager when the card turn ends (played or cancelled).</summary>
        public void EndCardInput()
        {
            _waitingForCard = false;
            ClearSelection();
            ClearCursor();
        }

        /// <summary>Called by GameManager when the bid panel is shown.</summary>
        public void BeginBidInput()  => _waitingForBid = true;

        /// <summary>Called by GameManager when bidding is done.</summary>
        public void EndBidInput()    => _waitingForBid = false;

        // ── Unity Update ──────────────────────────────────────────────────────

        private void Update()
        {
            SyncCursorWithMouse();
            HandleCardKeys();
            HandleBidKeys();
            HandleGlobalKeys();
        }

        // Tracks whether the keyboard cursor highlight is currently suppressed
        // because the mouse is hovering a card (mouse takes priority).
        private bool _cursorHiddenByMouse = false;

        /// <summary>
        /// Mouse takes priority over the keyboard: while the pointer is hovering
        /// any card, the blue keyboard-cursor highlight is hidden so only the
        /// mouse-hovered card lights up. The cursor index is remembered and the
        /// highlight is restored as soon as the mouse leaves.
        /// </summary>
        private void SyncCursorWithMouse()
        {
            if (!_waitingForCard) return;

            bool mouseOverCard = CardView.HoveredCard != null;

            if (mouseOverCard && !_cursorHiddenByMouse)
            {
                _cursorHiddenByMouse = true;
                if (_cursorIndex >= 0 && _cursorIndex < _cardViews.Count &&
                    _cardViews[_cursorIndex] != null)
                    _cardViews[_cursorIndex].SetKeyboardCursor(false);
            }
            else if (!mouseOverCard && _cursorHiddenByMouse)
            {
                _cursorHiddenByMouse = false;
                if (_cursorIndex >= 0 && _cursorIndex < _cardViews.Count &&
                    _cardViews[_cursorIndex] != null)
                    _cardViews[_cursorIndex].SetKeyboardCursor(true);
            }
        }

        // ── Card selection ────────────────────────────────────────────────────

        private void HandleCardKeys()
        {
            if (!_waitingForCard) return;

            // ← / →: move the cursor to the previous / next legal card.
            if (Input.GetKeyDown(KeyCode.LeftArrow))
            {
                MoveCursor(-1);
                return;
            }
            if (Input.GetKeyDown(KeyCode.RightArrow))
            {
                MoveCursor(+1);
                return;
            }

            // 1–8: jump the cursor straight to that position.
            for (int i = 0; i < 8; i++)
            {
                if (Input.GetKeyDown(KeyCode.Alpha1 + i) ||
                    Input.GetKeyDown(KeyCode.Keypad1 + i))
                {
                    SetCursor(i);
                    return;
                }
            }

            // Enter: confirm the card the cursor is on.
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            {
                ConfirmCard();
                return;
            }

            // Escape: deselect / clear the cursor.
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                ClearSelection();
                ClearCursor();
            }
        }

        // ── Cursor navigation ────────────────────────────────────────────────

        private static bool IsLegal(CardView cv)
        {
            if (cv == null) return false;
            UnityEngine.UI.Button btn = cv.GetComponent<UnityEngine.UI.Button>();
            return btn == null || btn.interactable;
        }

        /// <summary>Moves the cursor in <paramref name="dir"/> direction, skipping illegal cards.</summary>
        private void MoveCursor(int dir)
        {
            int count = _cardViews.Count;
            if (count == 0) return;

            int start = _cursorIndex;
            int idx   = start;

            for (int step = 0; step < count; step++)
            {
                idx += dir;
                if (idx < 0)       idx = count - 1; // wrap to the end
                if (idx >= count)  idx = 0;         // wrap to the start

                if (IsLegal(_cardViews[idx]))
                {
                    SetCursor(idx);
                    return;
                }

                if (idx == start) break; // came full circle, no legal card found
            }
        }

        /// <summary>Points the cursor at a specific index if that card is legal.</summary>
        private void SetCursor(int index)
        {
            if (index < 0 || index >= _cardViews.Count) return;
            if (!IsLegal(_cardViews[index])) return;

            // Remove the highlight from the old cursor card.
            ClearCursor();

            _cursorIndex = index;
            // Only show the highlight if the mouse isn't currently taking priority.
            if (!_cursorHiddenByMouse)
                _cardViews[index].SetKeyboardCursor(true);
        }

        /// <summary>Re-applies the cursor highlight after a hand rebuild, keeping it valid.</summary>
        private void RefreshCursor()
        {
            if (_cursorIndex >= 0 && _cursorIndex < _cardViews.Count &&
                IsLegal(_cardViews[_cursorIndex]))
            {
                if (!_cursorHiddenByMouse)
                    _cardViews[_cursorIndex].SetKeyboardCursor(true);
                return;
            }

            // Old index no longer valid — snap to the first legal card.
            _cursorIndex = -1;
            MoveCursor(+1);
        }

        private void ClearCursor()
        {
            if (_cursorIndex >= 0 && _cursorIndex < _cardViews.Count)
            {
                CardView cv = _cardViews[_cursorIndex];
                if (cv != null) cv.SetKeyboardCursor(false);
            }
            _cursorIndex = -1;
        }

        private void ConfirmCard()
        {
            // Prefer the cursor; fall back to an explicit selection if one exists.
            int index = _cursorIndex >= 0 ? _cursorIndex : _selectedCardIndex;
            if (index < 0 || index >= _cardViews.Count) return;

            CardView cv = _cardViews[index];
            if (!IsLegal(cv)) return;

            // Fire the same path as a mouse click.
            _humanController.HandleCardClicked(cv.Card);

            _waitingForCard    = false;
            _selectedCardIndex = -1;
            ClearCursor();
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

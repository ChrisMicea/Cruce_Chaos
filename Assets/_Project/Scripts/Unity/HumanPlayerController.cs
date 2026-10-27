using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using CruceChaos.Core.Cards;
using CruceChaos.Core.Game;
using CruceChaos.Core.Players;

namespace CruceChaos.Unity
{
    /// <summary>
    /// Implements <see cref="IPlayerController"/> for a human sitting at the
    /// keyboard/mouse. Because the core engine's <c>ChooseCard</c> is
    /// synchronous but Unity input is asynchronous, this class acts as a
    /// bridge:
    ///
    ///   1. The GameManager calls <see cref="BeginTurn"/> with the legal moves
    ///      and a callback. The human controller highlights those cards and
    ///      waits.
    ///   2. The player clicks a card. <see cref="HandleCardClicked"/> fires,
    ///      records the choice, and invokes the callback so the GameManager
    ///      can advance the round.
    ///   3. <see cref="ChooseCard"/> is never called directly on this class
    ///      from the engine — the GameManager drives turns manually (see
    ///      <see cref="GameManager"/>).
    ///
    /// NOTE: <see cref="IPlayerController.ChooseCard"/> IS still implemented
    /// (throws) so the type satisfies the interface if the engine is ever run
    /// directly. All real gameplay goes through <see cref="BeginTurn"/>.
    /// </summary>
    public class HumanPlayerController : MonoBehaviour, IPlayerController
    {
        // Set by GameManager before each turn.
        private IReadOnlyList<Card> _legalMoves;
        private Action<Card> _onCardChosen;
        private bool _waitingForInput;

        /// <summary>
        /// Called by GameManager at the start of the human's turn.
        /// Stores the legal set and the callback, then signals CardViews
        /// to show which cards are playable.
        /// </summary>
        public void BeginTurn(IReadOnlyList<Card> legalMoves, Action<Card> onCardChosen)
        {
            _legalMoves    = legalMoves;
            _onCardChosen  = onCardChosen;
            _waitingForInput = true;
        }

        /// <summary>
        /// Cancels an in-progress turn (e.g. the round ended unexpectedly).
        /// </summary>
        public void CancelTurn()
        {
            _waitingForInput = false;
            _onCardChosen    = null;
        }

        /// <summary>
        /// Called by each <see cref="CardView.OnCardClicked"/> event.
        /// Validates the card is in the legal set, then fires the callback.
        /// </summary>
        public void HandleCardClicked(Card card)
        {
            if (!_waitingForInput) return;

            // Safety: verify it is actually in the legal set.
            bool isLegal = false;
            foreach (Card c in _legalMoves)
            {
                if (c == card) { isLegal = true; break; }
            }
            if (!isLegal) return;

            _waitingForInput = false;
            Action<Card> cb  = _onCardChosen;
            _onCardChosen    = null;
            cb?.Invoke(card);
        }

        /// <summary>
        /// Satisfies <see cref="IPlayerController"/> but should never be
        /// called — all human turns go through <see cref="BeginTurn"/>.
        /// </summary>
        Card IPlayerController.ChooseCard(
            RoundParticipant self,
            IReadOnlyList<Card> legalMoves,
            ITrickContext context)
        {
            throw new InvalidOperationException(
                "HumanPlayerController.ChooseCard must not be called directly. " +
                "Use BeginTurn() and wait for the player to click a card.");
        }
    }
}

using System;
using System.Collections.Generic;
using UnityEngine;
using CruceChaos.Core.Cards;
using CruceChaos.Core.Game;
using CruceChaos.Core.Players;

namespace CruceChaos.Unity
{
    /// <summary>
    /// Implements <see cref="IPlayerController"/> for a human player.
    ///
    /// The interface is synchronous but Unity input is asynchronous, so this
    /// class bridges them with callbacks:
    ///
    ///   - Card play:   GameManager calls BeginTurn() → player clicks →
    ///                  HandleCardClicked() fires the callback.
    ///   - Bidding:     GameManager calls BeginBid() → BidUI resolves →
    ///                  SubmitBid() fires the callback.
    ///   - Trump decl:  GameManager calls BeginDeclareTrump() → player picks
    ///                  suit → SubmitTrump() fires the callback.
    ///   - Marriage:    GameManager calls BeginAnnounceMarriage() → player
    ///                  chooses → SubmitMarriage() fires the callback.
    ///   - Draft:       Not used in 1v1 DrawPilePerTrick mode (no draft phase).
    ///
    /// NOTE: IPlayerController.ChooseCard / PlaceBid / DeclareTrump /
    /// AnnounceMarriage / ChooseDraftCard are implemented as throws — all real
    /// decisions go through the Begin/Submit pattern so the GameManager drives
    /// the coroutine loop.
    /// </summary>
    public class HumanPlayerController : MonoBehaviour, IPlayerController
    {
        // ── Card play ─────────────────────────────────────────────────────────

        private IReadOnlyList<Card> _legalMoves;
        private Action<Card>        _onCardChosen;
        private bool                _waitingForCard;

        public void BeginTurn(IReadOnlyList<Card> legalMoves, Action<Card> onCardChosen)
        {
            _legalMoves    = legalMoves;
            _onCardChosen  = onCardChosen;
            _waitingForCard = true;
        }

        public void CancelTurn()
        {
            _waitingForCard = false;
            _onCardChosen   = null;
        }

        /// <summary>Called by each CardView.OnCardClicked event.</summary>
        public void HandleCardClicked(Card card)
        {
            if (!_waitingForCard) return;

            bool isLegal = false;
            foreach (Card c in _legalMoves)
                if (c == card) { isLegal = true; break; }
            if (!isLegal) return;

            _waitingForCard = false;
            Action<Card> cb = _onCardChosen;
            _onCardChosen   = null;
            cb?.Invoke(card);
        }

        // ── Bidding ───────────────────────────────────────────────────────────

        private Action<Bid> _onBidChosen;
        private bool        _waitingForBid;
        private BiddingContext _biddingContext;

        public void BeginBid(BiddingContext context, Action<Bid> onBidChosen)
        {
            _biddingContext = context;
            _onBidChosen    = onBidChosen;
            _waitingForBid  = true;
        }

        /// <summary>Called by the bid UI when the player confirms a bid value.</summary>
        public void SubmitBid(int value)
        {
            if (!_waitingForBid) return;
            _waitingForBid = false;
            Action<Bid> cb = _onBidChosen;
            _onBidChosen   = null;
            cb?.Invoke(value >= _biddingContext.MinLegalBid ? Bid.Of(value) : Bid.Pass);
        }

        /// <summary>Called by the bid UI when the player clicks Pass.</summary>
        public void SubmitPass()
        {
            if (!_waitingForBid) return;
            _waitingForBid = false;
            Action<Bid> cb = _onBidChosen;
            _onBidChosen   = null;
            cb?.Invoke(Bid.Pass);
        }

        // ── Trump declaration ─────────────────────────────────────────────────

        private Action<Suit> _onTrumpChosen;
        private bool         _waitingForTrump;

        public void BeginDeclareTrump(Action<Suit> onTrumpChosen)
        {
            _onTrumpChosen  = onTrumpChosen;
            _waitingForTrump = true;
        }

        /// <summary>Called by the trump-picker UI when the player selects a suit.</summary>
        public void SubmitTrump(Suit suit)
        {
            if (!_waitingForTrump) return;
            _waitingForTrump = false;
            Action<Suit> cb = _onTrumpChosen;
            _onTrumpChosen  = null;
            cb?.Invoke(suit);
        }

        // ── Marriage announcement ─────────────────────────────────────────────

        private Action<Suit?> _onMarriageChosen;
        private bool          _waitingForMarriage;

        public void BeginAnnounceMarriage(IReadOnlyList<Suit> available, Action<Suit?> onMarriageChosen)
        {
            _onMarriageChosen  = onMarriageChosen;
            _waitingForMarriage = true;
        }

        /// <summary>Called by the marriage UI when the player picks a suit to announce.</summary>
        public void SubmitMarriage(Suit suit)
        {
            if (!_waitingForMarriage) return;
            _waitingForMarriage = false;
            Action<Suit?> cb = _onMarriageChosen;
            _onMarriageChosen  = null;
            cb?.Invoke(suit);
        }

        /// <summary>Called when the player declines to announce a marriage.</summary>
        public void SubmitNoMarriage()
        {
            if (!_waitingForMarriage) return;
            _waitingForMarriage = false;
            Action<Suit?> cb = _onMarriageChosen;
            _onMarriageChosen  = null;
            cb?.Invoke(null);
        }

        // ── IPlayerController explicit implementations ────────────────────────
        // These should never be called directly — all decisions go through the
        // Begin/Submit coroutine pattern above. They throw so any accidental
        // direct call surfaces immediately as a clear error.

        Card IPlayerController.ChooseCard(RoundParticipant self, IReadOnlyList<Card> legalMoves, ITrickContext context)
            => throw new InvalidOperationException("Use HumanPlayerController.BeginTurn() instead.");

        Bid IPlayerController.PlaceBid(RoundParticipant self, BiddingContext context)
            => throw new InvalidOperationException("Use HumanPlayerController.BeginBid() instead.");

        Suit IPlayerController.DeclareTrump(RoundParticipant self)
            => throw new InvalidOperationException("Use HumanPlayerController.BeginDeclareTrump() instead.");

        Suit? IPlayerController.AnnounceMarriage(RoundParticipant self, IReadOnlyList<Suit> available, Suit? trump)
            => throw new InvalidOperationException("Use HumanPlayerController.BeginAnnounceMarriage() instead.");

        // Draft is not used in 1v1 DrawPilePerTrick — falls back to greedy pick.
        Card IPlayerController.ChooseDraftCard(RoundParticipant self, IReadOnlyList<Card> available, Suit trump)
        {
            Debug.LogWarning("[HumanPlayerController] ChooseDraftCard called — not expected in 1v1 mode. Picking first available.");
            return available[0];
        }
    }
}

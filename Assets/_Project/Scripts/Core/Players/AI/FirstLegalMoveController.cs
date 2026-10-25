using System.Collections.Generic;
using CruceChaos.Core.Cards;
using CruceChaos.Core.Game;

namespace CruceChaos.Core.Players.AI
{
    /// <summary>
    /// The simplest possible AI: always plays the first legal card.
    ///
    /// This is the baseline "stupidest AI that still plays legally" — useful as
    /// a difficulty floor, as an opponent while developing, and as a control to
    /// measure smarter AIs against. It is production code (the shipped game can
    /// plug it in), not a test-only driver, which is why it lives in Core with
    /// the rest of the engine rather than in the sandbox.
    ///
    /// It makes no strategic decision: it relies entirely on the legal-move
    /// pipeline having already filtered the hand, then takes the first option.
    /// </summary>
    public sealed class FirstLegalMoveController : IPlayerController
    {
        /// <summary>Always passes — the baseline makes no bidding decision.</summary>
        public Bid PlaceBid(RoundParticipant self, BiddingContext context) => Bid.Pass;

        /// <summary>Declares the suit of its first card — no strategy.</summary>
        public Suit DeclareTrump(RoundParticipant self) => self.Hand.Cards[0].Suit;

        /// <summary>Takes the first available card — no strategy.</summary>
        public Card ChooseDraftCard(RoundParticipant self, IReadOnlyList<Card> available, Suit trump) => available[0];

        public Card ChooseCard(RoundParticipant self, IReadOnlyList<Card> legalMoves, ITrickContext context)
        {
            // legalMoves is guaranteed non-empty by the engine's contract.
            return legalMoves[0];
        }
    }
}

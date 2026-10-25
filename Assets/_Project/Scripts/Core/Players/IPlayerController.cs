using System.Collections.Generic;
using CruceChaos.Core.Cards;
using CruceChaos.Core.Game;

namespace CruceChaos.Core.Players
{
    /// <summary>
    /// The decision-making seam for a player. Given the current round state and
    /// the set of cards it is legally allowed to play, a controller chooses one.
    ///
    /// This is the ONLY thing that differs between a human (choice comes from
    /// UI input) and an AI (choice comes from a strategy).
    /// </summary>
    public interface IPlayerController
    {
        /// <summary>
        /// Decide this player's bid during the bidding phase. Return
        /// <see cref="Bid.Pass"/> to pass, or <see cref="Bid.Of"/> for a numeric
        /// contract. A numeric bid MUST be in
        /// [<see cref="BiddingContext.MinLegalBid"/>, <see cref="BiddingContext.MaxBid"/>];
        /// the engine re-validates and treats an out-of-range bid as a pass.
        /// </summary>
        Bid PlaceBid(RoundParticipant self, BiddingContext context);

        /// <summary>
        /// Choose which card to play from <paramref name="legalMoves"/>.
        ///
        /// <paramref name="legalMoves"/> is guaranteed non-empty, and the
        /// returned card MUST be one of its members. <paramref name="self"/> is
        /// the choosing player's own round state (hand, captured points);
        /// <paramref name="context"/> exposes read-only information about the
        /// trick in progress (what has been led/played so far).
        /// </summary>
        Card ChooseCard(RoundParticipant self, IReadOnlyList<Card> legalMoves, ITrickContext context);
    }
}

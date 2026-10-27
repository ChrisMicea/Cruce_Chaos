namespace CruceChaos.Core.Game
{
    /// <summary>
    /// How cards are dealt and how any leftover (undealt) cards are resolved.
    /// This is an independent axis from the game mode: e.g. 2v2 can use either
    /// <see cref="DealAll"/> or <see cref="RevealAndDraft"/>, chosen by the
    /// player. A <see cref="GameMode"/> preset only picks a default.
    /// </summary>
    public enum DealStrategy
    {
        /// <summary>
        /// Deal every card; no leftover pile. Requires
        /// cardsPerPlayer × playerCount == 24. (2v2 "all 6"; 4-player
        /// individual.)
        /// </summary>
        DealAll,

        /// <summary>
        /// Deal a partial hand; the leftover forms a draw pile, and after each
        /// trick every player draws one (winner first) until it is empty. While
        /// the pile has cards, follow-suit is suspended ("cu carti de jos").
        /// (Current 1v1.)
        /// </summary>
        DrawPilePerTrick,

        /// <summary>
        /// Deal a partial hand; the leftover is revealed to everyone, then the
        /// bid winner picks one card, then each other player picks one in
        /// clockwise order. Requires leftover == playerCount so each drafts
        /// exactly one. DEFERRED — depends on bidding (needs the bid winner).
        /// (1v1v1 "7 + draft 1"; 2v2 "5 + 1".)
        /// </summary>
        RevealAndDraft,

        /// <summary>
        /// Deal a partial hand; each player receives their leftover card(s)
        /// blind (face-down, no choice, no reveal). DEFERRED — not yet
        /// implemented. (The full-random variants, e.g. 2v2 "3 + 3 random".)
        /// </summary>
        BlindSurprise
    }
}

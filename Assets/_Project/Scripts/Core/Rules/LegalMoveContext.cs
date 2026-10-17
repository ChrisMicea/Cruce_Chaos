using CruceChaos.Core.Cards;
using CruceChaos.Core.Game;

namespace CruceChaos.Core.Rules
{
    /// <summary>
    /// A read-only snapshot of everything a legality rule might need to decide
    /// which cards are playable right now. Rules READ from this; they never
    /// mutate game state.
    ///
    /// Bundling the inputs into one context object (rather than passing a
    /// growing list of parameters to every rule) means new rules can need new
    /// facts without changing the signature of <see cref="ILegalMoveRule"/> or
    /// every existing rule.
    /// </summary>
    public sealed class LegalMoveContext
    {
        /// <summary>The hand of the player about to play.</summary>
        public Hand Hand { get; }

        /// <summary>The trick in progress (what has been led / played so far).</summary>
        public ITrickContext Trick { get; }

        /// <summary>
        /// The trump suit for this round, or null if there is no trump (either
        /// the mode has none, or it has not been established yet). Null for the
        /// current milestone since trump is not implemented.
        /// </summary>
        public Suit? Trump { get; }

        /// <summary>
        /// Number of cards still in the draw pile. In "cu carti de jos" modes,
        /// follow-suit obligations are suspended while this is &gt; 0.
        /// </summary>
        public int DrawPileCount { get; }

        /// <summary>True when the player is leading (no card led yet this trick).</summary>
        public bool IsLeading => Trick.IsEmpty;

        public LegalMoveContext(Hand hand, ITrickContext trick, Suit? trump, int drawPileCount)
        {
            Hand = hand;
            Trick = trick;
            Trump = trump;
            DrawPileCount = drawPileCount;
        }
    }
}

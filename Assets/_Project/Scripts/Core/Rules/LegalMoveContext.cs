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
        /// The round's trump suit, declared by the bid winner before play, or
        /// null if the mode has no trump.
        /// </summary>
        public Suit? Trump { get; }

        /// <summary>
        /// Number of cards still in the draw pile. In "cu carti de jos" modes,
        /// follow-suit obligations are suspended while this is &gt; 0.
        /// </summary>
        public int DrawPileCount { get; }

        /// <summary>
        /// True only for the bid winner's very first lead of the round. Used to
        /// enforce the "opening lead must be trump" obligation (rule a).
        /// </summary>
        public bool IsOpeningLead { get; }

        /// <summary>
        /// When the leading player has announced a marriage this lead, the suit
        /// of it; otherwise null. The AnnouncedMarriageLeadRule uses this to
        /// require the lead to be the Three or Four of the announced suit.
        /// </summary>
        public Suit? AnnouncedSuit { get; }

        /// <summary>True when the player is leading (no card led yet this trick).</summary>
        public bool IsLeading => Trick.IsEmpty;

        public LegalMoveContext(
            Hand hand, ITrickContext trick, Suit? trump, int drawPileCount,
            bool isOpeningLead = false, Suit? announcedSuit = null)
        {
            Hand = hand;
            Trick = trick;
            Trump = trump;
            DrawPileCount = drawPileCount;
            IsOpeningLead = isOpeningLead;
            AnnouncedSuit = announcedSuit;
        }
    }
}

using System.Collections.Generic;
using System.Linq;
using CruceChaos.Core.Cards;

namespace CruceChaos.Core.Rules.Implementations
{
    /// <summary>
    /// "Daca nu ai culoarea ceruta si ai tromf, trebuie sa pui tromf" — when a
    /// player cannot follow the led suit but holds trump, they must play a
    /// trump.
    ///
    /// This rule is only about the OBLIGATION to play trump, not about which
    /// trump. The "must play a HIGHER trump than one already on the table"
    /// requirement belongs to <see cref="MustRaiseRule"/> (it is a raising
    /// requirement, applied to whichever suit is contested), and only applies
    /// when that optional rule is active.
    ///
    /// Runs AFTER <see cref="FollowSuitRule"/>, so if the player could follow
    /// the led suit the candidates are already restricted to it; in that case
    /// this rule leaves them untouched. It bites only when the player cannot
    /// follow suit.
    /// </summary>
    public sealed class TrumpObligationRule : ILegalMoveRule
    {
        public const string RuleId = "trump-obligation";

        public string Id => RuleId;
        public string DisplayName => "Must play trump when unable to follow suit";

        public RuleResult Apply(IReadOnlyList<Card> candidates, LegalMoveContext context)
        {
            // No trump suit in play, or leading -> no obligation to enforce.
            if (context.Trump == null || context.IsLeading)
            {
                return RuleResult.Continue(candidates);
            }

            Suit trump = context.Trump.Value;
            Suit led = context.Trick.LedSuit;

            // If the player can still follow the led suit, following suit takes
            // precedence over trumping, so trump does not apply. We detect this
            // by the candidates still containing a led-suit card. (When
            // led == trump, following suit already means playing trump, so this
            // correctly defers to follow-suit either way.)
            if (candidates.Any(c => c.Suit == led))
            {
                return RuleResult.Continue(candidates);
            }

            // Player cannot follow suit. If they hold trump, they must play a
            // trump — restrict to trumps. If they hold none, no obligation; the
            // never-empty floor leaves the candidates unchanged anyway, but we
            // return them explicitly for clarity.
            List<Card> trumps = candidates.Where(c => c.Suit == trump).ToList();
            return RuleResult.Continue(trumps.Count > 0 ? trumps : candidates);
        }
    }
}

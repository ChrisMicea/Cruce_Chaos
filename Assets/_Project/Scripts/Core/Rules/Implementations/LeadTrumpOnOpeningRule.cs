using System.Collections.Generic;
using System.Linq;
using CruceChaos.Core.Cards;

namespace CruceChaos.Core.Rules.Implementations
{
    /// <summary>
    /// "The bid winner must lead TRUMP on the very first card of the round."
    ///
    /// A hard rule, enforced for every mode: on the opening lead, and
    /// only then, the player's legal moves are restricted to trump cards. Every
    /// later lead is unconstrained by this rule.
    ///
    /// If the bid winner somehow holds no trump at all, the never-empty floor in
    /// the evaluator relaxes this to "lead anything" — an impossible hand in
    /// practice (they declared trump from their own cards), but handled safely.
    /// </summary>
    public sealed class LeadTrumpOnOpeningRule : ILegalMoveRule
    {
        public const string RuleId = "lead-trump-on-opening";

        public string Id => RuleId;
        public string DisplayName => "Bid winner must lead trump on the opening";

        public RuleResult Apply(IReadOnlyList<Card> candidates, LegalMoveContext context)
        {
            // Only bites on the opening lead, and only if trump exists.
            if (!context.IsOpeningLead || context.Trump == null)
            {
                return RuleResult.Continue(candidates);
            }

            Suit trump = context.Trump.Value;
            List<Card> trumps = candidates.Where(c => c.Suit == trump).ToList();

            // Restrict to trump; if none (floor will relax), leave unchanged.
            return RuleResult.Continue(trumps.Count > 0 ? trumps : candidates);
        }
    }
}

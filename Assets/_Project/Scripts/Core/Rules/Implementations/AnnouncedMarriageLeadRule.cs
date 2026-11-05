using System.Collections.Generic;
using System.Linq;
using CruceChaos.Core.Cards;

namespace CruceChaos.Core.Rules.Implementations
{
    /// <summary>
    /// "If you announced a marriage this lead, you must lead one of its cards."
    ///
    /// A marriage (anunțare) is announced by leading the Three or Four of the
    /// announced suit. When the engine has recorded an announcement
    /// (<see cref="LegalMoveContext.AnnouncedSuit"/> is set), this rule restricts
    /// the lead to exactly those two cards, so the chosen lead actually makes the
    /// announcement. When no marriage is announced, the rule does nothing.
    ///
    /// Keeping this in the pipeline (rather than as ad-hoc filtering in the
    /// engine) matches how every other lead/play obligation is enforced, and the
    /// never-empty floor safely handles the impossible case where neither card
    /// is present.
    /// </summary>
    public sealed class AnnouncedMarriageLeadRule : ILegalMoveRule
    {
        public const string RuleId = "announced-marriage-lead";

        public string Id => RuleId;
        public string DisplayName => "Must lead the announced marriage's Three or Four";

        public RuleResult Apply(IReadOnlyList<Card> candidates, LegalMoveContext context)
        {
            if (context.AnnouncedSuit == null)
            {
                return RuleResult.Continue(candidates);
            }

            Suit suit = context.AnnouncedSuit.Value;
            List<Card> marriageCards = candidates
                .Where(c => c.Suit == suit && (c.Rank == Rank.Three || c.Rank == Rank.Four))
                .ToList();

            return RuleResult.Continue(marriageCards.Count > 0 ? marriageCards : candidates);
        }
    }
}

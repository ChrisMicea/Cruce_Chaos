using System.Collections.Generic;
using System.Linq;
using CruceChaos.Core.Cards;

namespace CruceChaos.Core.Rules.Implementations
{
    /// <summary>
    /// "Trebuie sa raspunzi la culoare": if the player is NOT leading and holds
    /// at least one card of the led suit, they must play the led suit — all
    /// other suits are removed from the candidate set.
    ///
    /// Does nothing when the player is leading (there is no led suit to follow)
    /// or when they hold none of the led suit (handled here by leaving the set
    /// unchanged; the trump obligation rule, later in the pipeline, decides what
    /// happens when you cannot follow suit).
    /// </summary>
    public sealed class FollowSuitRule : ILegalMoveRule
    {
        public const string RuleId = "follow-suit";

        public string Id => RuleId;
        public string DisplayName => "Must follow the led suit";

        public RuleResult Apply(IReadOnlyList<Card> candidates, LegalMoveContext context)
        {
            if (context.IsLeading)
            {
                return RuleResult.Continue(candidates);
            }

            Suit led = context.Trick.LedSuit;
            List<Card> ofLed = candidates.Where(c => c.Suit == led).ToList();

            // If the player can follow suit, restrict to the led suit.
            // If not, leave the set as-is (the never-empty floor would undo an
            // empty result anyway, but returning the original is clearer).
            if (ofLed.Count > 0)
            {
                return RuleResult.Continue(ofLed);
            }

            return RuleResult.Continue(candidates);
        }
    }
}

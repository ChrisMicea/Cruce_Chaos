using System.Collections.Generic;
using CruceChaos.Core.Cards;

namespace CruceChaos.Core.Rules.Implementations
{
    /// <summary>
    /// "Cu carti de jos" rule: while the draw pile still has cards, a player
    /// may play ANY card — follow-suit and trump obligations are suspended.
    ///
    /// This is a short-circuiting rule: when the draw pile is non-empty it
    /// declares the full candidate set final, so the follow-suit / trump rules
    /// after it never run. When the pile is empty it does nothing and lets the
    /// rest of the pipeline apply the normal obligations.
    ///
    /// Belongs only in rule sets for modes that deal from a draw pile (1v1
    /// "cu carti de jos"); modes without a draw pile (e.g. 2v2) simply omit it.
    /// </summary>
    public sealed class DrawPhaseAnythingGoesRule : ILegalMoveRule
    {
        public const string RuleId = "draw-phase-anything-goes";

        public string Id => RuleId;
        public string DisplayName => "Free play while cards remain to draw";

        public RuleResult Apply(IReadOnlyList<Card> candidates, LegalMoveContext context)
        {
            if (context.DrawPileCount > 0)
            {
                // Anything in hand is legal; no later rule should narrow it.
                return RuleResult.Final(candidates);
            }

            // Draw pile exhausted: this rule has no opinion, defer to the rest.
            return RuleResult.Continue(candidates);
        }
    }
}

using System.Collections.Generic;
using System.Linq;
using CruceChaos.Core.Cards;

namespace CruceChaos.Core.Rules.Implementations
{
    /// <summary>
    /// OPTIONAL rule (player-toggled): you must not merely play the required
    /// suit but play a card STRONGER than the highest card of that suit already
    /// on the table, if you hold one.
    ///
    /// Applies to whichever suit is CONTESTED, not just the led suit:
    ///   - Normally the earlier rules narrow the candidates to the led suit, so
    ///     this enforces "beat the highest led-suit card" (raspunzi mai mare).
    ///   - When the player could not follow suit and
    ///     <see cref="TrumpObligationRule"/> narrowed the candidates to trump,
    ///     this enforces "over-trump the highest trump on the table" — which is
    ///     exactly the tabletop rule that you must beat an existing trump, and
    ///     correctly only when this optional rule is enabled.
    ///
    /// Because it reads the contested suit from the (already narrowed)
    /// candidates rather than hardcoding the led suit, it needs no knowledge of
    /// WHY the candidates are what they are, and it must sit LAST — after
    /// follow-suit and trump-obligation have established the contested suit.
    ///
    /// The never-empty floor in <see cref="LegalMoveEvaluator"/> handles the
    /// "cannot beat it" case: if no candidate out-ranks the table, this rule's
    /// narrowed set is empty and the evaluator falls back to the previous set
    /// (any card of the contested suit).
    /// </summary>
    public sealed class MustRaiseRule : ILegalMoveRule
    {
        public const string RuleId = "must-raise";

        public string Id => RuleId;
        public string DisplayName => "Must beat the highest card of the contested suit";

        public RuleResult Apply(IReadOnlyList<Card> candidates, LegalMoveContext context)
        {
            if (context.IsLeading || candidates.Count == 0)
            {
                return RuleResult.Continue(candidates);
            }

            // The contested suit is whatever the earlier rules narrowed the
            // candidates to. If the candidates are mixed (e.g. the player had no
            // obligation and may discard anything), there is nothing to raise
            // against, so leave them alone.
            Suit contested = candidates[0].Suit;
            if (candidates.Any(c => c.Suit != contested))
            {
                return RuleResult.Continue(candidates);
            }

            // Highest strength of the contested suit currently on the table.
            int highestOnTable = context.Trick.Plays
                .Where(p => p.Card.Suit == contested)
                .Select(p => p.Card.Strength)
                .DefaultIfEmpty(-1)
                .Max();

            // Keep only candidates that beat it. If none do, the evaluator's
            // never-empty floor restores the previous set.
            List<Card> raising = candidates
                .Where(c => c.Strength > highestOnTable)
                .ToList();

            return RuleResult.Continue(raising);
        }
    }
}

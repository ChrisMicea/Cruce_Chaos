using System.Collections.Generic;
using CruceChaos.Core.Cards;

namespace CruceChaos.Core.Rules
{
    /// <summary>
    /// One narrow, self-contained constraint on which cards may be played.
    ///
    /// Rules are composed into an ordered pipeline (see
    /// <see cref="LegalMoveEvaluator"/>): each rule receives the candidates
    /// left by the previous rule and returns a (possibly smaller) set. A rule
    /// answers exactly ONE question — "must I follow suit?", "must I trump?",
    /// "must I raise?" — which keeps each rule testable in isolation and lets
    /// game modes and power-ups add, remove, or reorder rules without touching
    /// the others.
    ///
    /// CONTRACT:
    /// - A rule must never widen the set; it only keeps or removes candidates.
    /// - A rule should NOT worry about accidentally emptying the set. The
    ///   evaluator enforces a never-empty floor: if a rule's output is empty,
    ///   the evaluator discards it and keeps the previous non-empty set. This
    ///   keeps the deadlock-avoidance invariant in ONE place instead of in
    ///   every rule.
    /// </summary>
    public interface ILegalMoveRule
    {
        /// <summary>
        /// Stable identifier for this rule, so power-ups and configuration can
        /// reference it (e.g. "remove the follow-suit rule") without holding a
        /// direct object reference. Expected to be unique within a rule set.
        /// </summary>
        string Id { get; }

        /// <summary>Human-readable name, e.g. for a rule-customization menu.</summary>
        string DisplayName { get; }

        /// <summary>
        /// Apply this rule to the current <paramref name="candidates"/> given
        /// <paramref name="context"/>, returning the narrowed set (and whether
        /// to stop the pipeline).
        /// </summary>
        RuleResult Apply(IReadOnlyList<Card> candidates, LegalMoveContext context);
    }
}

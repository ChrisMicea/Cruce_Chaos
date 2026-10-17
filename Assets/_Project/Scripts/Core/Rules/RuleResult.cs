using System.Collections.Generic;

namespace CruceChaos.Core.Rules
{
    /// <summary>
    /// What a single rule returns: the (possibly narrowed) set of candidate
    /// cards, plus whether the pipeline should STOP after this rule.
    ///
    /// Most rules return <see cref="Continue"/> so later rules can narrow
    /// further. A short-circuiting rule (e.g. "while the draw pile has cards,
    /// anything goes") returns <see cref="Final"/> to declare the set complete
    /// and skip the remaining rules.
    /// </summary>
    public readonly struct RuleResult
    {
        /// <summary>The candidate cards after this rule has had its say.</summary>
        public IReadOnlyList<Cards.Card> Candidates { get; }

        /// <summary>
        /// When true, the evaluator stops and returns <see cref="Candidates"/>
        /// without running any further rules.
        /// </summary>
        public bool Stop { get; }

        private RuleResult(IReadOnlyList<Cards.Card> candidates, bool stop)
        {
            Candidates = candidates;
            Stop = stop;
        }

        /// <summary>This rule narrowed (or kept) the set; keep running later rules.</summary>
        public static RuleResult Continue(IReadOnlyList<Cards.Card> candidates) =>
            new RuleResult(candidates, false);

        /// <summary>This set is final; stop and ignore any remaining rules.</summary>
        public static RuleResult Final(IReadOnlyList<Cards.Card> candidates) =>
            new RuleResult(candidates, true);
    }
}

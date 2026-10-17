using System;
using System.Collections.Generic;
using CruceChaos.Core.Cards;

namespace CruceChaos.Core.Rules
{
    /// <summary>
    /// Runs a <see cref="RuleSet"/> as an ordered pipeline to compute the legal
    /// cards a player may play, starting from their whole hand.
    ///
    /// Two invariants it enforces so individual rules don't have to:
    ///
    ///   1. NEVER-EMPTY FLOOR. If applying a rule would leave zero candidates,
    ///      that result is discarded and the previous non-empty set is kept.
    ///      This is Cruce's natural fallback behaviour ("if you can't satisfy
    ///      the obligation, you fall back to the looser one") and it guarantees
    ///      the player always has at least one legal move, so the game can
    ///      never deadlock on an over-constrained rule combination.
    ///
    ///      This encodes a contract: a rule returning an empty set means "my
    ///      constraint is unsatisfiable, ignore me" (relaxation), NOT "stop the
    ///      pipeline" (termination). Those are different operations; see
    ///      <see cref="RuleResult.Stop"/> for the latter.
    ///
    ///   2. SHORT-CIRCUIT. A rule may declare its result final (e.g. the
    ///      draw-phase "anything goes" rule), ending the pipeline early.
    ///
    /// LIMITATION: the floor only remembers ONE step back. It is correct as
    /// long as every narrowing rule's intended fallback is "the set that
    /// existed immediately before me", which holds for standard Cruce. A future
    /// (power-up) rule whose correct fallback is some earlier set would need a
    /// fuller history; that is deliberately not built until something needs it.
    /// </summary>
    public sealed class LegalMoveEvaluator
    {
        private readonly RuleSet _ruleSet;
        private readonly Action<string> _warn;

        /// <summary>
        /// Creates an evaluator for the given rule set.
        /// </summary>
        /// <param name="ruleSet">The ordered rules to run.</param>
        /// <param name="warn">
        /// Optional callback invoked when the evaluator's safety guard trips —
        /// i.e. a misbehaving rule left a non-empty hand with zero legal moves.
        /// Pass null to ignore (the guard still recovers; it just stays quiet).
        /// </param>
        public LegalMoveEvaluator(RuleSet ruleSet, Action<string> warn = null)
        {
            _ruleSet = ruleSet;
            _warn = warn ?? (_ => { });
        }

        /// <summary>
        /// Compute the legal cards for the player described by
        /// <paramref name="context"/>. The result is always non-empty as long
        /// as the hand is non-empty.
        /// </summary>
        public IReadOnlyList<Card> LegalMoves(LegalMoveContext context)
        {
            // Start from the full hand as the baseline candidate set.
            IReadOnlyList<Card> current = new List<Card>(context.Hand.Cards);

            foreach (ILegalMoveRule rule in _ruleSet.Rules)
            {
                RuleResult result = rule.Apply(current, context);

                // Never-empty floor: a rule that empties the set is ignored,
                // preserving the previous non-empty candidates.
                if (result.Candidates != null && result.Candidates.Count > 0)
                {
                    current = result.Candidates;
                }

                if (result.Stop)
                {
                    break;
                }
            }

            // Safety guard: the per-rule floor should already guarantee this,
            // but if a rule misbehaved (e.g. returned a non-null empty set that
            // slipped through, or the starting set was empty for a reason other
            // than an empty hand) we must never hand the player zero moves when
            // they are actually holding cards. Fall back to the full hand and
            // flag it loudly, turning a silent deadlock into a debuggable one.
            if (current.Count == 0 && context.Hand.Count > 0)
            {
                _warn("LegalMoveEvaluator: a rule left zero legal moves for a " +
                      "non-empty hand; falling back to the full hand. This is a " +
                      "rule bug — a narrowing rule must return the unchanged set " +
                      "(or an empty set, treated as a no-op) when it cannot apply.");
                return new List<Card>(context.Hand.Cards);
            }

            return current;
        }
    }
}

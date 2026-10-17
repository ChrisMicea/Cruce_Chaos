using System.Collections.Generic;
using System.Linq;

namespace CruceChaos.Core.Rules
{
    /// <summary>
    /// An ordered, mutable collection of legality rules — the "which rules are
    /// in play right now" object.
    ///
    /// It is deliberately mutable and addressable by rule <see cref="ILegalMoveRule.Id"/>
    /// so that:
    ///   - a game mode assembles its baseline list up front, then
    ///   - start-of-game customization toggles optional rules on/off, and
    ///   - (later) power-ups add, remove, or replace rules WHILE a match runs.
    ///
    /// Order is significant: it is the precedence of the pipeline. Follow-suit
    /// must come before must-raise, etc.
    /// </summary>
    public sealed class RuleSet
    {
        private readonly List<ILegalMoveRule> _rules = new List<ILegalMoveRule>();

        public RuleSet() { }

        public RuleSet(IEnumerable<ILegalMoveRule> rules)
        {
            _rules.AddRange(rules);
        }

        /// <summary>The rules in pipeline order.</summary>
        public IReadOnlyList<ILegalMoveRule> Rules => _rules;

        /// <summary>Appends a rule to the end of the pipeline.</summary>
        public RuleSet Append(ILegalMoveRule rule)
        {
            _rules.Add(rule);
            return this;
        }

        /// <summary>Inserts a rule at a specific position in the pipeline.</summary>
        public RuleSet InsertAt(int index, ILegalMoveRule rule)
        {
            _rules.Insert(index, rule);
            return this;
        }

        /// <summary>
        /// Removes the rule with the given id. Returns true if one was removed.
        /// Used by rule-customization menus and by power-ups that disable a rule.
        /// </summary>
        public bool RemoveById(string id)
        {
            int index = _rules.FindIndex(r => r.Id == id);
            if (index < 0)
            {
                return false;
            }

            _rules.RemoveAt(index);
            return true;
        }

        /// <summary>True if a rule with the given id is present.</summary>
        public bool Contains(string id) => _rules.Any(r => r.Id == id);
    }
}

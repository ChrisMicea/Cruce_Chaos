using CruceChaos.Core.Rules.Implementations;

namespace CruceChaos.Core.Rules
{
    /// <summary>
    /// The single place where a game mode plus the player's chosen options turn
    /// into a concrete, ordered <see cref="RuleSet"/>.
    ///
    /// Two layers, kept deliberately separate (per the vanilla-mode design):
    ///   - a MODE BASELINE: the rules that define what the mode fundamentally is
    ///     (these are not offered to the player to switch off), plus
    ///   - OPTIONAL rules layered on top from the start-of-game menu.
    ///
    /// Keeping them apart means the customization menu only ever exposes the
    /// optional rules, and the baseline that makes a mode "correct Cruce" stays
    /// protected.
    /// </summary>
    public static class RuleSetFactory
    {
        /// <summary>
        /// Player-selectable options chosen before the match. Expand this as
        /// more optional rules are added to the menu.
        /// </summary>
        public struct Options
        {
            /// <summary>Enable the optional "must beat the highest card" rule.</summary>
            public bool MustRaiseWhenFollowing;
        }

        /// <summary>
        /// Rule set for draw-pile ("cu carti de jos") modes: free play while the
        /// draw pile has cards, then follow-suit / trump obligations once it is
        /// empty. The draw-phase rule short-circuits the pipeline while cards
        /// remain to draw.
        /// </summary>
        public static RuleSet DrawPileRuleSet(Options options)
        {
            var set = new RuleSet()
                // Opening-lead trump obligation comes FIRST — it must override
                // the draw-phase "anything goes" short-circuit on that one lead.
                .Append(new LeadTrumpOnOpeningRule())
                .Append(new AnnouncedMarriageLeadRule())   // lead must be the announced 3/4
                .Append(new DrawPhaseAnythingGoesRule())  // short-circuits during the draw phase
                .Append(new FollowSuitRule())             // then normal obligations:
                .Append(new TrumpObligationRule());

            AppendOptional(set, options);
            return set;
        }

        /// <summary>
        /// Rule set for modes WITHOUT a draw pile (e.g. 2v2 / 1v1v1): follow-suit
        /// and trump obligations apply from the first trick. Simply omits the
        /// draw-phase short-circuit rule.
        /// </summary>
        public static RuleSet NoDrawPile(Options options)
        {
            var set = new RuleSet()
                .Append(new LeadTrumpOnOpeningRule())
                .Append(new AnnouncedMarriageLeadRule())
                .Append(new FollowSuitRule())
                .Append(new TrumpObligationRule());

            AppendOptional(set, options);
            return set;
        }

        private static void AppendOptional(RuleSet set, Options options)
        {
            // Optional rules go AFTER the baseline so they refine, not replace,
            // the fundamental obligations. Must-raise in particular only makes
            // sense after follow-suit has restricted to the led suit.
            if (options.MustRaiseWhenFollowing)
            {
                set.Append(new MustRaiseRule());
            }
        }
    }
}

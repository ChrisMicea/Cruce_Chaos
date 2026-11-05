using CruceChaos.Core.Rules;

namespace CruceChaos.Core.Game
{
    /// <summary>
    /// The named game modes a player picks from. Each maps to a default
    /// <see cref="MatchConfig"/> via <see cref="GameModes.DefaultConfig"/>. The
    /// enum is the player-facing choice; the config is the data the engine runs
    /// on. An advanced menu may start from a default config and override fields.
    /// </summary>
    public enum GameMode
    {
        /// <summary>2 players, individual, "cu carti de jos" draw pile.</summary>
        OneVsOne,

        /// <summary>4 players in two teams of opposite seats (2v2), all 6 dealt.</summary>
        TwoVsTwo,

        /// <summary>2v2 "5 + 1 draft": 5 dealt each, 4 revealed and drafted one apiece.</summary>
        TwoVsTwoDraft,

        /// <summary>3 players, individual.</summary>
        OneVsOneVsOne,

        /// <summary>4 players, individual (free-for-all).</summary>
        FourPlayerIndividual
    }

    /// <summary>
    /// Builds the default <see cref="MatchConfig"/> for each <see cref="GameMode"/>.
    ///
    /// Deal sizes follow the project's chosen variants:
    ///   - 1v1: 8 dealt each, 8 as a draw pile (DrawPilePerTrick).
    ///   - 2v2: 6 dealt each, no leftover (DealAll). (The "5 + 1" draft variant
    ///     is an advanced override to RevealAndDraft once bidding exists.)
    ///   - 1v1v1: 7 dealt each, 3 leftover drafted one apiece (RevealAndDraft).
    ///   - 4-player individual: 6 dealt each, no leftover (DealAll).
    /// </summary>
    public static class GameModes
    {
        public static MatchConfig DefaultConfig(GameMode mode, RuleSetFactory.Options ruleOptions)
        {
            switch (mode)
            {
                case GameMode.OneVsOne:
                    return new MatchConfig(
                        playerCount: 2,
                        cardsPerPlayer: 8,
                        dealStrategy: DealStrategy.DrawPilePerTrick,
                        teamOfSeat: new[] { 0, 1 },
                        ruleOptions: ruleOptions);

                case GameMode.TwoVsTwo:
                    return new MatchConfig(
                        playerCount: 4,
                        cardsPerPlayer: 6,
                        dealStrategy: DealStrategy.DealAll,
                        // Opposite seats share a team: seats 0,2 vs 1,3.
                        teamOfSeat: new[] { 0, 1, 0, 1 },
                        ruleOptions: ruleOptions);

                case GameMode.TwoVsTwoDraft:
                    return new MatchConfig(
                        playerCount: 4,
                        cardsPerPlayer: 5,     // 4×5 = 20 dealt, 4 revealed & drafted
                        dealStrategy: DealStrategy.RevealAndDraft,
                        teamOfSeat: new[] { 0, 1, 0, 1 },
                        ruleOptions: ruleOptions);

                case GameMode.OneVsOneVsOne:
                    return new MatchConfig(
                        playerCount: 3,
                        cardsPerPlayer: 7,
                        dealStrategy: DealStrategy.RevealAndDraft,
                        teamOfSeat: new[] { 0, 1, 2 },
                        ruleOptions: ruleOptions);

                case GameMode.FourPlayerIndividual:
                    return new MatchConfig(
                        playerCount: 4,
                        cardsPerPlayer: 6,
                        dealStrategy: DealStrategy.DealAll,
                        teamOfSeat: new[] { 0, 1, 2, 3 },
                        ruleOptions: ruleOptions);

                default:
                    return null;
            }
        }
    }
}

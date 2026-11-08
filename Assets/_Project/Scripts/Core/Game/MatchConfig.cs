using System;
using System.Linq;
using CruceChaos.Core.Rules;

namespace CruceChaos.Core.Game
{
    /// <summary>
    /// Match-level configuration: the settings a player chooses ONCE before a
    /// match and that hold for every round of it — player count, team layout,
    /// how many cards are dealt, the deal/leftover strategy, and the legality
    /// rule options.
    ///
    /// This is data, not behaviour: a <see cref="GameMode"/> preset produces a
    /// sensible default config, and an advanced menu may override fields within
    /// what is legal. <see cref="Validate"/> enforces the invariants so an
    /// impossible combination is caught at setup, never mid-deal.
    /// </summary>
    public sealed class MatchConfig
    {
        /// <summary>Total deck size — always 24 for Cruce.</summary>
        public const int DeckSize = 24;

        public int PlayerCount { get; }

        /// <summary>Cards dealt to each player at the start of a round.</summary>
        public int CardsPerPlayer { get; }

        public DealStrategy DealStrategy { get; }

        /// <summary>
        /// Team index per seat, length == <see cref="PlayerCount"/>. Seat i
        /// belongs to team <c>TeamOfSeat[i]</c>. Individual modes give each seat
        /// its own team (0,1,2,...); 2v2 pairs opposite seats (0,1,0,1), so
        /// teammates sit across the table and the turn rotation alternates teams.
        /// </summary>
        public int[] TeamOfSeat { get; }

        /// <summary>Number of distinct teams (equals PlayerCount for individual play).</summary>
        public int TeamCount => TeamOfSeat.Distinct().Count();

        /// <summary>True when at least one team contains more than one seat.</summary>
        public bool IsTeamPlay => TeamCount < PlayerCount;

        /// <summary>Legality rule options (must-raise etc.), fed to the rule pipeline.</summary>
        public RuleSetFactory.Options RuleOptions { get; }

        /// <summary>
        /// The highest numeric bid allowed. Vanilla Cruce uses 6; it is
        /// configurable (not a hardcoded 6) so power-ups can raise the ceiling.
        /// </summary>
        public int MaxBid { get; }

        /// <summary>
        /// Match-score target. A team wins once it reaches this AND leads the
        /// runner-up by at least 2 (see the MatchEngine's end condition).
        /// </summary>
        public int TargetScore { get; }

        /// <summary>Cards left undealt after the initial deal.</summary>
        public int LeftoverCount => DeckSize - (CardsPerPlayer * PlayerCount);

        /// <summary>
        /// The seat that opens the bidding for a given 1-based round number.
        /// The opener rotates each round for fairness (whoever opens has an
        /// advantage under these rules): round 1 opens at seat 0, round 2 at
        /// seat 1, and so on, wrapping by <see cref="PlayerCount"/>.
        ///
        /// This is the single source of truth for the rotation. Both the
        /// headless <see cref="MatchEngine"/> and the Unity round driver call it
        /// so the two can never diverge.
        /// </summary>
        /// <param name="roundNumber">1-based round index (first round == 1).</param>
        public int StartingBidderSeat(int roundNumber)
        {
            // Normalise so any int (including a 0 or negative) maps into range.
            int zeroBased = roundNumber - 1;
            return ((zeroBased % PlayerCount) + PlayerCount) % PlayerCount;
        }

        public MatchConfig(
            int playerCount,
            int cardsPerPlayer,
            DealStrategy dealStrategy,
            int[] teamOfSeat,
            RuleSetFactory.Options ruleOptions,
            int maxBid = 6,
            int targetScore = 11)
        {
            PlayerCount = playerCount;
            CardsPerPlayer = cardsPerPlayer;
            DealStrategy = dealStrategy;
            TeamOfSeat = teamOfSeat;
            RuleOptions = ruleOptions;
            MaxBid = maxBid;
            TargetScore = targetScore;

            Validate();
        }

        /// <summary>
        /// Throws <see cref="ArgumentException"/> if the configuration is
        /// internally inconsistent. Keeping every invariant here means callers
        /// (and GameMode presets) cannot construct a config that would fail
        /// partway through a deal.
        /// </summary>
        private void Validate()
        {
            if (PlayerCount < 2 || PlayerCount > 4)
            {
                throw new ArgumentException($"PlayerCount must be 2–4, was {PlayerCount}.");
            }

            if (CardsPerPlayer < 1)
            {
                throw new ArgumentException($"CardsPerPlayer must be positive, was {CardsPerPlayer}.");
            }

            if (MaxBid < 1)
            {
                throw new ArgumentException($"MaxBid must be at least 1, was {MaxBid}.");
            }

            if (TargetScore < 1)
            {
                throw new ArgumentException($"TargetScore must be at least 1, was {TargetScore}.");
            }

            if (TeamOfSeat == null || TeamOfSeat.Length != PlayerCount)
            {
                throw new ArgumentException("TeamOfSeat must have one entry per seat.");
            }

            int dealt = CardsPerPlayer * PlayerCount;
            if (dealt > DeckSize)
            {
                throw new ArgumentException(
                    $"Deal of {CardsPerPlayer}×{PlayerCount}={dealt} exceeds the {DeckSize}-card deck.");
            }

            // Each deal strategy constrains how many leftover cards are allowed.
            switch (DealStrategy)
            {
                case DealStrategy.DealAll:
                    if (LeftoverCount != 0)
                    {
                        throw new ArgumentException(
                            $"DealAll requires 0 leftover cards, but {LeftoverCount} remain.");
                    }
                    break;

                case DealStrategy.RevealAndDraft:
                    // Each player drafts exactly one leftover in clockwise order.
                    if (LeftoverCount != PlayerCount)
                    {
                        throw new ArgumentException(
                            $"RevealAndDraft requires leftover == PlayerCount ({PlayerCount}), " +
                            $"but {LeftoverCount} remain.");
                    }
                    break;

                case DealStrategy.DrawPilePerTrick:
                case DealStrategy.BlindSurprise:
                    if (LeftoverCount < 0)
                    {
                        throw new ArgumentException("Leftover count cannot be negative.");
                    }
                    break;
            }
        }
    }
}

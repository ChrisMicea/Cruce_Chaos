using System;
using System.Collections.Generic;
using System.Linq;
using CruceChaos.Core.Game;
using CruceChaos.Core.Players;
using CruceChaos.Core.Players.AI;
using CruceChaos.Core.Players.AI.GreedyHeuristic;
using CruceChaos.Core.Rules;

namespace CruceChaos.Sandbox
{
    /// <summary>
    /// Standalone console harness for the pure-C# Core. Runs one full round of
    /// a chosen game mode and prints the play-by-play plus the final
    /// per-scoring-unit captured-point tallies.
    ///
    /// Usage:  dotnet run --project Sandbox -- [seed] [mode] [raise]
    ///   seed  : integer RNG seed (default 42)
    ///   mode  : 1v1 | 2v2 | 1v1v1 | ffa4   (default 1v1)
    ///   raise : the literal "raise" to enable the optional must-raise rule
    ///
    /// No Unity dependency; run with `dotnet run` from the repo root.
    /// </summary>
    internal static class Program
    {
        private static void Main(string[] args)
        {
            int seed = 42;
            if (args.Length > 0 && int.TryParse(args[0], out int parsed))
            {
                seed = parsed;
            }

            GameMode mode = ParseMode(args.Length > 1 ? args[1] : "1v1");
            bool mustRaise = Array.IndexOf(args, "raise") >= 0;
            bool singleRound = Array.IndexOf(args, "round") >= 0;

            var options = new RuleSetFactory.Options { MustRaiseWhenFollowing = mustRaise };
            MatchConfig config = GameModes.DefaultConfig(mode, options);

            (Player[] players, IPlayerController[] controllers) = BuildSeats(config.PlayerCount);

            Console.WriteLine($"=== Cruce Chaos — {mode} (seed {seed}, must-raise {(mustRaise ? "ON" : "off")}) ===\n");

            if (singleRound)
            {
                RunSingleRound(players, controllers, config, options, seed);
                return;
            }

            // Default: play a full match to the target score.
            var match = new MatchEngine(players, controllers, config, seed: seed, log: Console.WriteLine);
            MatchResult result = match.Play();

            string winners = string.Join(" / ", result.Winners.Select(p => p.Name));
            Console.WriteLine($"=== MATCH OVER — winner: {winners} (score {result.WinningScore}, target {config.TargetScore}) ===");
        }

        /// <summary>Detailed single-round inspection (per-trick log + per-player team view).</summary>
        private static void RunSingleRound(
            Player[] players, IPlayerController[] controllers, MatchConfig config,
            RuleSetFactory.Options options, int seed)
        {
            RuleSet ruleSet = config.DealStrategy == DealStrategy.DrawPilePerTrick
                ? RuleSetFactory.DrawPileRuleSet(options)
                : RuleSetFactory.NoDrawPile(options);

            var engine = new RoundEngine(players, controllers, config, ruleSet, seed, Console.WriteLine);
            RoundResult result = engine.Play();

            Console.WriteLine("=== Round over ===");
            if (result.WinningBid.HasValue)
            {
                Console.WriteLine($"  Contract: {players[result.BidWinnerSeat].Name} bid {result.WinningBid} (trump {result.Trump})");
            }

            int total = 0;
            foreach (ScoringUnit u in result.ScoringUnits)
            {
                Console.WriteLine($"  {u.Label}: {u.CapturedPoints} points captured");
                total += u.CapturedPoints;
            }

            Console.WriteLine($"\n  Total points distributed: {total} (should be 120)");
            if (total != 120)
            {
                Console.WriteLine("  WARNING: points do not sum to 120 — something is wrong.");
            }
        }

        private static GameMode ParseMode(string s)
        {
            switch (s.ToLowerInvariant())
            {
                case "2v2": return GameMode.TwoVsTwo;
                case "2v2draft": return GameMode.TwoVsTwoDraft;
                case "1v1v1": return GameMode.OneVsOneVsOne;
                case "ffa4": return GameMode.FourPlayerIndividual;
                case "1v1":
                default: return GameMode.OneVsOne;
            }
        }

        /// <summary>
        /// Build players and controllers for the given seat count. Seat 0 is the
        /// baseline (first-legal) engine; every other seat is the greedy engine,
        /// so the greedy AIs are measured against the dumb one.
        /// </summary>
        private static (Player[], IPlayerController[]) BuildSeats(int count)
        {
            string[] names = { "Alice", "Bob", "Cara", "Dan" };
            var players = new Player[count];
            var controllers = new IPlayerController[count];

            for (int i = 0; i < count; i++)
            {
                players[i] = new Player(names[i]);
                controllers[i] = i == 0
                    ? new FirstLegalMoveController()
                    : (IPlayerController)new GreedyHeuristicController();
            }

            return (players, controllers);
        }
    }
}

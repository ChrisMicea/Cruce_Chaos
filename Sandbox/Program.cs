using System;
using System.Collections.Generic;
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

            var options = new RuleSetFactory.Options { MustRaiseWhenFollowing = mustRaise };
            MatchConfig config = GameModes.DefaultConfig(mode, options);

            // The rule set depends on whether the mode uses a draw pile: the
            // draw-phase "anything goes" rule only belongs to draw-pile modes.
            RuleSet ruleSet = config.DealStrategy == DealStrategy.DrawPilePerTrick
                ? RuleSetFactory.TwoPlayerDrawPile(options)
                : RuleSetFactory.NoDrawPile(options);

            (Player[] players, IPlayerController[] controllers) = BuildSeats(config.PlayerCount);

            Console.WriteLine($"=== Cruce Chaos — {mode} (seed {seed}, must-raise {(mustRaise ? "ON" : "off")}) ===\n");

            var engine = new RoundEngine(players, controllers, config, ruleSet, seed, Console.WriteLine);
            RoundResult result = engine.Play();

            Console.WriteLine("=== Round over ===");
            if (result.WinningBid.HasValue)
            {
                string winner = players[result.BidWinnerSeat].Name;
                Console.WriteLine($"  Contract: {winner} bid {result.WinningBid} (trump {result.Trump})");
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

            // Per-player view the UI would use: own captured points vs the
            // participant's team total, looked up via RoundResult.UnitOf.
            Console.WriteLine("\n  Per-player (own / team):");
            foreach (RoundParticipant p in engine.Participants)
            {
                ScoringUnit unit = result.UnitOf(p);
                Console.WriteLine($"    {p.Player.Name}: own {p.CapturedPoints} / team {unit?.CapturedPoints} [{unit?.Label}]");
            }
        }

        private static GameMode ParseMode(string s)
        {
            switch (s.ToLowerInvariant())
            {
                case "2v2": return GameMode.TwoVsTwo;
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

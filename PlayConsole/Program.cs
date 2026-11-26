using System;
using System.Linq;
using CruceChaos.Core.Game;
using CruceChaos.Core.Players;
using CruceChaos.Core.Rules;

namespace CruceChaos.PlayConsole
{
    /// <summary>
    /// Command-line front-end for the pure-C# Core. The whole game is playable
    /// here: any game mode, any rule combination, and any seat occupied by a
    /// human or an AI. It serves two purposes — interactive debugging/play, and
    /// a scriptable headless harness (e.g. AI-vs-AI benchmarking, and later an
    /// ML training environment) — which is why configuration comes from either
    /// an interactive menu or command-line flags (see <see cref="Cli"/>).
    ///
    /// Run with no arguments for the setup menu, or <c>--help</c> for flags.
    /// No Unity dependency; run with `dotnet run --project PlayConsole`.
    /// </summary>
    internal static class Program
    {
        private static void Main(string[] args)
        {
            RunConfig run;
            try
            {
                run = Cli.Resolve(args);
            }
            catch (ArgumentException ex)
            {
                // A bad flag/menu value is a user error, not a crash: print the
                // reason and point at --help, without a stack trace.
                Console.Error.WriteLine($"Setup error: {ex.Message}");
                Console.Error.WriteLine("Run with --help for usage.");
                Environment.ExitCode = 1;
                return;
            }

            if (run == null)
            {
                return; // --help was printed, or setup was aborted.
            }

            (Player[] players, IPlayerController[] controllers) = run.BuildSeats();

            string raiseState = run.Match.RuleOptions.MustRaiseWhenFollowing ? "ON" : "off";
            Console.WriteLine($"=== Cruce Chaos — {run.Mode} (seed {run.Seed}, must-raise {raiseState}) ===");
            if (run.AnyHuman)
            {
                Console.Error.WriteLine("(You are playing. Your prompts appear here on stderr; the game log is on stdout.)");
            }

            Console.WriteLine();

            if (run.SingleRound)
            {
                RunSingleRound(players, controllers, run);
                return;
            }

            var match = new MatchEngine(players, controllers, run.Match, seed: run.Seed, log: Console.WriteLine);
            MatchResult result = match.Play();

            string winners = string.Join(" / ", result.Winners.Select(p => p.Name));
            Console.WriteLine(
                $"=== MATCH OVER — winner: {winners} (score {result.WinningScore}, target {run.Match.TargetScore}) ===");
        }

        /// <summary>Detailed single-round inspection (per-trick log + per-unit tally + 120-point checksum).</summary>
        private static void RunSingleRound(Player[] players, IPlayerController[] controllers, RunConfig run)
        {
            RuleSet ruleSet = run.Match.DealStrategy == DealStrategy.DrawPilePerTrick
                ? RuleSetFactory.DrawPileRuleSet(run.Match.RuleOptions)
                : RuleSetFactory.NoDrawPile(run.Match.RuleOptions);

            var engine = new RoundEngine(players, controllers, run.Match, ruleSet, run.Seed, Console.WriteLine);
            RoundResult result = engine.Play();

            Console.WriteLine("=== Round over ===");
            if (result.WinningBid.HasValue)
            {
                Console.WriteLine(
                    $"  Contract: {players[result.BidWinnerSeat].Name} bid {result.WinningBid} (trump {result.Trump})");
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
    }
}

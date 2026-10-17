using System;
using CruceChaos.Core.Game;
using CruceChaos.Core.Players;
using CruceChaos.Core.Rules;

namespace CruceChaos.Sandbox
{
    /// <summary>
    /// Standalone console harness for the pure-C# Core. Runs one full round of
    /// the simplified (no-trump) 2-player game and prints the play-by-play plus
    /// the final captured-point tallies.
    ///
    /// This has no Unity dependency; run it with `dotnet run` from Sandbox/.
    /// </summary>
    internal static class Program
    {
        private static void Main(string[] args)
        {
            // Fixed seed => reproducible shuffle, so the output is stable run to
            // run. Pass a seed as the first CLI arg to try a different deal.
            int seed = 42;
            if (args.Length > 0 && int.TryParse(args[0], out int parsed))
            {
                seed = parsed;
            }

            var players = new[]
            {
                new Player("Alice"),
                new Player("Bob")
            };

            var controllers = new IPlayerController[]
            {
                new FirstLegalMoveController(),
                new FirstLegalMoveController()
            };

            // Second CLI arg "raise" turns on the optional must-raise rule, to
            // demonstrate start-of-game rule customization.
            bool mustRaise = args.Length > 1 && args[1] == "raise";
            var options = new RuleSetFactory.Options { MustRaiseWhenFollowing = mustRaise };
            RuleSet ruleSet = RuleSetFactory.TwoPlayerDrawPile(options);

            Console.WriteLine($"=== Cruce Chaos — single round (seed {seed}, must-raise {(mustRaise ? "ON" : "off")}) ===\n");

            var engine = new RoundEngine(players, controllers, ruleSet, seed, Console.WriteLine);
            RoundParticipant[] result = engine.Play();

            Console.WriteLine("=== Round over ===");
            int total = 0;
            foreach (RoundParticipant p in result)
            {
                Console.WriteLine($"  {p.Player.Name}: {p.CapturedPoints} points captured");
                total += p.CapturedPoints;
            }

            Console.WriteLine($"\n  Total points distributed: {total} (should be 120)");

            if (total != 120)
            {
                Console.WriteLine("  WARNING: points do not sum to 120 — something is wrong.");
            }
        }
    }
}

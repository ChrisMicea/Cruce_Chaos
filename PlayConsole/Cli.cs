using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using CruceChaos.Core.Game;
using CruceChaos.Core.Players;
using CruceChaos.Core.Players.AI;
using CruceChaos.Core.Players.AI.GreedyHeuristic;
using CruceChaos.Core.Rules;

namespace CruceChaos.PlayConsole
{
    /// <summary>What kind of controller sits in a seat.</summary>
    internal enum SeatKind
    {
        Human,
        GreedyAi,
        FirstLegalAi
    }

    /// <summary>
    /// A fully-resolved run specification: everything the engine needs, however
    /// it was gathered (flags or the interactive menu both produce this). Keeping
    /// the two front-ends collapsing into one struct means <c>Program</c> runs
    /// the same code path regardless of how the user configured the game.
    /// </summary>
    internal sealed class RunConfig
    {
        public GameMode Mode;
        public MatchConfig Match;
        public SeatKind[] Seats;
        public int Seed;
        public bool SingleRound;

        /// <summary>Build the per-seat players and controllers from <see cref="Seats"/>.</summary>
        public (Player[] players, IPlayerController[] controllers) BuildSeats()
        {
            string[] names = { "Alice", "Bob", "Cara", "Dan" };
            var players = new Player[Seats.Length];
            var controllers = new IPlayerController[Seats.Length];

            for (int i = 0; i < Seats.Length; i++)
            {
                string name = names[i];
                players[i] = new Player(name);
                controllers[i] = Seats[i] switch
                {
                    SeatKind.Human => new HumanConsoleController(name),
                    SeatKind.FirstLegalAi => new FirstLegalMoveController(),
                    _ => (IPlayerController)new GreedyHeuristicController(),
                };
            }

            return (players, controllers);
        }

        public bool AnyHuman => Seats.Any(s => s == SeatKind.Human);
    }

    /// <summary>
    /// The two front-ends that produce a <see cref="RunConfig"/>: command-line
    /// flags (scriptable, for batch/benchmark/ML harness use) and an interactive
    /// setup menu (for debugging and casual play). Running the program with no
    /// arguments, or with <c>--menu</c>, opens the menu; any recognised flag
    /// runs headless.
    /// </summary>
    internal static class Cli
    {
        private const int DefaultSeed = 42;

        // ---- entry: decide between menu and flags -------------------------

        public static RunConfig Resolve(string[] args)
        {
            if (args.Length == 0 || HasFlag(args, "--menu") || HasFlag(args, "-m"))
            {
                return RunMenu();
            }

            if (HasFlag(args, "--help") || HasFlag(args, "-h"))
            {
                PrintHelp();
                return null;
            }

            return FromFlags(args);
        }

        // ---- flag parsing -------------------------------------------------

        /// <summary>
        /// Flags:
        ///   --mode  1v1 | 2v2 | 2v2draft | 1v1v1 | ffa4   (default 1v1)
        ///   --seed  &lt;int&gt;                              (default 42)
        ///   --seats h,g,g,...   per-seat kind: h=human, g=greedy, f=firstlegal
        ///                        (length must match the mode's player count;
        ///                         default: all greedy)
        ///   --raise             enable the must-raise optional rule
        ///   --round             play one detailed round instead of a full match
        /// </summary>
        private static RunConfig FromFlags(string[] args)
        {
            GameMode mode = ParseMode(GetValue(args, "--mode") ?? "1v1");
            int seed = ParseInt(GetValue(args, "--seed"), DefaultSeed);
            bool mustRaise = HasFlag(args, "--raise");
            bool singleRound = HasFlag(args, "--round");

            var options = new RuleSetFactory.Options { MustRaiseWhenFollowing = mustRaise };
            MatchConfig match = GameModes.DefaultConfig(mode, options);

            SeatKind[] seats = ParseSeats(GetValue(args, "--seats"), match.PlayerCount);

            return new RunConfig
            {
                Mode = mode,
                Match = match,
                Seats = seats,
                Seed = seed,
                SingleRound = singleRound,
            };
        }

        private static SeatKind[] ParseSeats(string spec, int count)
        {
            // Default: all greedy AI (the old AI-vs-AI benchmark behaviour).
            if (string.IsNullOrWhiteSpace(spec))
            {
                return Enumerable.Repeat(SeatKind.GreedyAi, count).ToArray();
            }

            string[] parts = spec.Split(',');
            if (parts.Length != count)
            {
                throw new ArgumentException(
                    $"--seats expects {count} entries for this mode, got {parts.Length}.");
            }

            return parts.Select(p => p.Trim().ToLowerInvariant() switch
            {
                "h" or "human" => SeatKind.Human,
                "g" or "greedy" => SeatKind.GreedyAi,
                "f" or "first" or "firstlegal" => SeatKind.FirstLegalAi,
                var other => throw new ArgumentException(
                    $"Unknown seat kind '{other}'. Use h (human), g (greedy), or f (firstlegal)."),
            }).ToArray();
        }

        // ---- interactive menu ---------------------------------------------

        private static RunConfig RunMenu()
        {
            Console.WriteLine("=== Cruce Chaos — setup ===\n");

            GameMode mode = ChooseMode();

            // Rule toggles. Each optional rule in RuleSetFactory.Options gets one
            // yes/no prompt here. ADD FUTURE OPTIONAL RULES IN BOTH PLACES:
            //   1) a bool field on RuleSetFactory.Options, and
            //   2) a prompt line below.
            var options = new RuleSetFactory.Options
            {
                MustRaiseWhenFollowing = AskYesNo("Enable must-raise (beat the highest card when following)?", false),
            };

            MatchConfig match = GameModes.DefaultConfig(mode, options);
            SeatKind[] seats = ChooseSeats(match.PlayerCount);
            int seed = AskInt("RNG seed", DefaultSeed);
            bool singleRound = AskYesNo("Play a single detailed round (instead of a full match)?", false);

            Console.WriteLine();
            return new RunConfig
            {
                Mode = mode,
                Match = match,
                Seats = seats,
                Seed = seed,
                SingleRound = singleRound,
            };
        }

        private static GameMode ChooseMode()
        {
            var modes = (GameMode[])Enum.GetValues(typeof(GameMode));
            Console.WriteLine("Game mode:");
            for (int i = 0; i < modes.Length; i++)
            {
                Console.WriteLine($"  [{i}] {modes[i]}");
            }

            return modes[AskIndex("Mode", 0, modes.Length)];
        }

        private static SeatKind[] ChooseSeats(int count)
        {
            string[] names = { "Alice", "Bob", "Cara", "Dan" };
            var seats = new SeatKind[count];
            var kinds = (SeatKind[])Enum.GetValues(typeof(SeatKind));

            Console.WriteLine($"\nAssign the {count} seats (human or AI):");
            for (int seat = 0; seat < count; seat++)
            {
                Console.WriteLine($"  Seat {seat} ({names[seat]}):");
                for (int k = 0; k < kinds.Length; k++)
                {
                    Console.WriteLine($"    [{k}] {kinds[k]}");
                }

                seats[seat] = kinds[AskIndex($"  Seat {seat}", (int)SeatKind.GreedyAi, kinds.Length)];
            }

            return seats;
        }

        // ---- small input helpers ------------------------------------------

        private static bool AskYesNo(string question, bool dflt)
        {
            string hint = dflt ? "Y/n" : "y/N";
            while (true)
            {
                Console.Write($"{question} [{hint}] ");
                string line = Console.ReadLine();
                if (string.IsNullOrWhiteSpace(line))
                {
                    return dflt;
                }

                switch (line.Trim().ToLowerInvariant())
                {
                    case "y" or "yes": return true;
                    case "n" or "no": return false;
                    default: Console.WriteLine("  Please answer y or n."); break;
                }
            }
        }

        private static int AskInt(string label, int dflt)
        {
            Console.Write($"{label} [{dflt}]: ");
            string line = Console.ReadLine();
            return ParseInt(line, dflt);
        }

        /// <summary>Ask for a menu index in [0, count), re-asking on bad input.</summary>
        private static int AskIndex(string label, int dflt, int count)
        {
            while (true)
            {
                Console.Write($"{label} [{dflt}]: ");
                string line = Console.ReadLine();
                if (string.IsNullOrWhiteSpace(line))
                {
                    return dflt;
                }

                if (int.TryParse(line.Trim(), out int idx) && idx >= 0 && idx < count)
                {
                    return idx;
                }

                Console.WriteLine($"  Please enter a number between 0 and {count - 1}.");
            }
        }

        // ---- shared parsing ------------------------------------------------

        private static GameMode ParseMode(string s)
        {
            switch (s.Trim().ToLowerInvariant())
            {
                case "2v2": return GameMode.TwoVsTwo;
                case "2v2draft": return GameMode.TwoVsTwoDraft;
                case "1v1v1": return GameMode.OneVsOneVsOne;
                case "ffa4": return GameMode.FourPlayerIndividual;
                case "1v1": return GameMode.OneVsOne;
                default:
                    throw new ArgumentException(
                        $"Unknown mode '{s}'. Use 1v1 | 2v2 | 2v2draft | 1v1v1 | ffa4.");
            }
        }

        private static int ParseInt(string s, int dflt) =>
            int.TryParse((s ?? string.Empty).Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int v)
                ? v
                : dflt;

        private static bool HasFlag(string[] args, string flag) =>
            Array.Exists(args, a => string.Equals(a, flag, StringComparison.OrdinalIgnoreCase));

        /// <summary>Value following a "--key value" flag, or null if absent.</summary>
        private static string GetValue(string[] args, string key)
        {
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (string.Equals(args[i], key, StringComparison.OrdinalIgnoreCase))
                {
                    return args[i + 1];
                }
            }

            return null;
        }

        private static void PrintHelp()
        {
            Console.WriteLine(@"Cruce Chaos — command-line game

Run with NO arguments (or --menu) for an interactive setup menu.
Run with flags for a scriptable, headless game:

  --mode   1v1 | 2v2 | 2v2draft | 1v1v1 | ffa4   (default 1v1)
  --seed   <int>                                 (default 42)
  --seats  <csv>   per-seat kind: h=human, g=greedy, f=firstlegal
                   one entry per seat for the mode; default all greedy
  --raise          enable the optional must-raise rule
  --round          play one detailed round instead of a full match
  --menu, -m       force the interactive menu
  --help, -h       show this help

Examples:
  dotnet run --project PlayConsole
  dotnet run --project PlayConsole -- --mode 2v2 --seats h,g,g,g
  dotnet run --project PlayConsole -- --mode ffa4 --seed 7 --seats g,g,g,g --round");
        }
    }
}

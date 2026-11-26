# PlayConsole

A standalone command-line front-end for the pure-C# game `Core`. The whole game
is playable here: **every mode, every rule combination, any seat human or AI.**

It serves two purposes:

- **Interactive play / debugging** — sit a human in any seat and play against AI.
- **Scriptable harness** — run headless AI-vs-AI games for benchmarking (and,
  later, as an ML training environment).

It has **no Unity dependency**. Its `.csproj` *links* the `Core/*.cs` sources
that Unity also compiles (under `Assets/_Project/Scripts/Core/`), so there is one
source of truth and no drift — nothing is copied.

## Requirements

The .NET SDK (the project targets `net10.0`). Check with `dotnet --version`.

## Running

All commands are run from the **repo root**.

```bash
# Interactive setup menu (pick mode, per-seat human/AI, rule toggles, seed):
dotnet run --project PlayConsole

# Flag reference:
dotnet run --project PlayConsole -- --help
```

> Note the `--` separator: everything after it is passed to the game rather than
> to `dotnet`.

## Two ways to configure a game

Both front-ends produce the exact same run; pick whichever fits.

### 1. Interactive menu

Run with **no arguments** (or `--menu` / `-m`). It walks you through:
game mode → rule toggles → per-seat human/AI assignment → RNG seed → full match
or single round.

### 2. Command-line flags (scriptable / headless)

| Flag            | Values                                           | Default     |
| --------------- | ------------------------------------------------ | ----------- |
| `--mode`        | `1v1` \| `2v2` \| `2v2draft` \| `1v1v1` \| `ffa4`| `1v1`       |
| `--seed`        | integer                                          | `42`        |
| `--seats`       | CSV, one per seat: `h`=human, `g`=greedy, `f`=firstlegal | all greedy |
| `--raise`       | (flag) enable the optional must-raise rule       | off         |
| `--round`       | (flag) one detailed round instead of a full match| full match  |
| `--menu`, `-m`  | force the interactive menu                       | —           |
| `--help`, `-h`  | show usage                                       | —           |

`--seats` must have one entry per seat for the chosen mode (2 for `1v1`, 3 for
`1v1v1`, 4 for the rest). A bad value prints a clean `Setup error` and exits `1`.

## Examples

```bash
# Play seat 0 as a human in 2v2 against three greedy AIs:
dotnet run --project PlayConsole -- --mode 2v2 --seats h,g,g,g

# Headless AI-vs-AI benchmark, one detailed round with the 120-point checksum:
dotnet run --project PlayConsole -- --mode ffa4 --seed 7 --seats g,g,g,g --round

# 1v1v1 full match, must-raise enabled, you in seat 1:
dotnet run --project PlayConsole -- --mode 1v1v1 --seats g,h,g --raise
```

## Playing as a human

When a seat is human, at each decision (bid, declare trump, draft, announce
marriage, play a card) you get a numbered menu; type the number and press Enter.

- **Prompts and your menus go to `stderr`; the game's play-by-play log goes to
  `stdout`.** This keeps the log clean for piping/redirection while you still see
  your prompts in the terminal.
- Your hand shows each card's points, e.g. `Ace of Red [11p]`; **trump cards are
  marked with a `*`**.
- When only one move is legal, it is auto-played.
- Invalid input re-asks. If stdin closes (piped input runs out), the game picks
  option `0` instead of hanging — handy for scripted runs.

## Output streams

- `stdout` — the clean game log (deal, bids, tricks, scores, result).
- `stderr` — human prompts and menus only.

Redirect to separate the two, e.g. keep just the log:

```bash
dotnet run --project PlayConsole -- --mode ffa4 --seats g,g,g,g 2>/dev/null
```

## Adding a new optional rule

Optional (player-toggleable) rules extend in **one** place each:

1. Add a `bool` field to `RuleSetFactory.Options` (in `Core/Rules/`) and honor it
   in `RuleSetFactory`.
2. Add one prompt line in `Cli.RunMenu` and, if you want a flag for it, one
   `HasFlag` check in `Cli.FromFlags`.

The engine, the human controller, and the AI controllers need no changes — they
all read the resulting `MatchConfig`.

## Files

| File                        | Responsibility                                             |
| --------------------------- | ---------------------------------------------------------- |
| `Program.cs`                | Entry point; resolves config, runs a match or single round.|
| `Cli.cs`                    | Flag parsing **and** the interactive menu → one `RunConfig`.|
| `HumanConsoleController.cs` | `IPlayerController` driven by a human at the terminal.     |
| `PlayConsole.csproj`        | Project file; links the shared `Core` sources.             |

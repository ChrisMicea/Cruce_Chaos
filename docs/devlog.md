## Development log

### Core game logic (first milestone, in progress) -- Christian

Added the pure-C# game domain under `Assets/_Project/Scripts/Core/` (no Unity
dependency, so it is testable on its own and the Unity layer sits on top later):

- **`Core/Cards/`**
  - `Suit` — the 4 suits: Green, Red, Acorn, Bell.
  - `Rank` — the 6 ranks; enum ordinal encodes trick strength (Nine < Two < Three < Four < Ten < Ace).
  - `Card` — immutable `(Suit, Rank)` value; exposes `Points` (A=11, 10=10, 4=4, 3=3, 2=2, 9=0) and `Strength` as separate concepts.
  - `Deck` — the 24 cards, Fisher–Yates shuffle (seedable for reproducibility), draw from top.
  - `Hand` — a player's cards: add/remove/contains plus `CardsOfSuit` / `HasSuit` for the (upcoming) follow-suit rules.
- **`Core/Game/`**
  - `Player` — persistent match-level identity + running `MatchScore` across rounds.
  - `RoundParticipant` — transient per-round state (hand + captured card points), created fresh each round.
  - `Trick` — one trick's plays and the winner rule.
  - `RoundEngine` — deals 8 each (8 in the draw pile), plays tricks, draws from the pile after each trick, reports captured points.
- **`Core/Players/`**
  - `IPlayerController` — the decision seam; the same engine code path drives both pass-and-play and vs-AI by swapping controllers.

**Simplified for now (to be added later):** no bidding or score conversion. The
round currently just reports captured card points out of 120. Follow-suit and
trump are now enforced via the rules pipeline below.

### Standalone console sandbox -- Christian

`Sandbox/` is a plain .NET console project **outside `Assets/`** so Unity never
compiles it. Its `.csproj` *links* the Core `.cs` files via a `<Compile Include>`
glob, so there is a single source of truth shared with the Unity project (no
copies, no drift). It contains a placeholder `FirstLegalMoveController` (always
plays its first legal card) and a `Program.cs` that runs one full round with a
fixed RNG seed and checks the captured points sum to 120.

#### Running the sandbox -- Christian

Requires the .NET SDK (the project targets `net10.0`). From the repo root:

```bash
# Run one round with the default seed (42):
dotnet run --project Sandbox

# Run with a specific seed (e.g. 7) to try a different deal:
dotnet run --project Sandbox -- 7
```

It prints the deal, the trick-by-trick play, and the final captured-point
tallies.


### Move-legality rules system (pipes-and-filters) -- Christian

Added `Assets/_Project/Scripts/Core/Rules/` — a composable pipeline that decides
which cards a player may legally play. The goal is modularity: legality varies by
**game mode** (1v1 draw-pile vs 2v2 / 1v1v1) and by **player-toggled options**,
and future power-ups must be able to add, remove, or modify rules *mid-match*.

- **`LegalMoveContext`** — a read-only snapshot (hand, trick in progress, trump
  suit, draw-pile size) that rules decide from. Rules never mutate state.
- **`RuleResult`** — a rule's output: the narrowed candidate set plus a `Stop`
  flag for short-circuiting the pipeline.
- **`ILegalMoveRule`** — one narrow constraint. Each rule has a stable `Id` so
  power-ups / config can target it ("remove the follow-suit rule") without
  holding an object reference.
- **`RuleSet`** — a mutable, id-addressable, ordered rule list (append, insert,
  remove-by-id). Order *is* the pipeline precedence. Mutability is what lets a
  power-up change the rules while a match runs.
- **`LegalMoveEvaluator`** — runs the pipeline from the full hand down.
- **`RuleSetFactory`** — the single assembly point turning "mode + player
  options" into a `RuleSet`. Keeps the **mode baseline** (rules that define the
  mode, not offered to the player) separate from **optional** rules exposed in a
  start-of-game menu. `TwoPlayerDrawPile(...)` and `NoDrawPile(...)` are the
  first two modes.
- **`Rules/Implementations/`**
  - `DrawPhaseAnythingGoesRule` — "cu carti de jos": while the draw pile has
    cards, play anything. Short-circuits the pipeline (returns `Final`).
  - `FollowSuitRule` — must follow the led suit if you hold it.
  - `TrumpObligationRule` — **STUB** (trump not modelled yet); positioned in the
    pipeline so its order is already fixed.
  - `MustRaiseRule` — **OPTIONAL**, player-toggled: when following suit you must
    beat the highest card on the table if you can.

Wired into `RoundEngine`: the old "all cards are legal" line is replaced by a
`LegalMoveContext` + `LegalMoveEvaluator` call, so the engine stays
mode-agnostic. The sandbox gained a second CLI arg to toggle must-raise:

```bash
# must-raise OFF (default) vs ON, same deal:
dotnet run --project Sandbox -- 42
dotnet run --project Sandbox -- 42 raise
```

With seed 42 this changed the outcome from a 120/0 shutout (pre-rules) to 83/37
(follow-suit), and 68/52 with must-raise on — demonstrating both mode behaviour
and the optional toggle. The 120-point checksum holds in all cases.

#### Two invariants enforced centrally (so individual rules stay simple)

1. **Never-empty floor.** If a rule narrows the set to empty, the evaluator
   discards that result and keeps the previous non-empty set. This encodes
   Cruce's natural *relaxation* fallback ("if you can't satisfy the obligation,
   fall back to the looser one") and guarantees the player always has a legal
   move. Crucially, an empty result means "ignore me", **not** "stop the
   pipeline" — relaxation and termination are kept as separate operations
   (termination is `RuleResult.Stop`). This is why "no led-suit card + must-raise
   on" does NOT force a raise: must-raise produces an empty set, which is
   discarded, so free play remains.
2. **Safety guard.** After the pipeline, if a non-empty hand somehow ended with
   zero legal moves (a misbehaving rule), the evaluator falls back to the full
   hand and emits a warning through an optional `warn` callback (wired to the
   engine's log). Turns a silent deadlock into a loud, debuggable one.

Known limitation: the floor only remembers one step back. Correct for standard
Cruce (every narrowing rule's fallback is "the set just before me"); a future
power-up needing an earlier fallback would require a fuller history. Deliberately
not built until something needs it.

### Trump (tromf) -- Christian

Added trump to the round. For now trump is simply **the suit of the first card
played in the round** (the "culoarea primei carti desemneaza tromful" rule).

**Where trump lives.** `RoundEngine` holds a `Suit? _trump`, null until
established, set exactly once in `EstablishTrumpIfUnset(card)`. Isolating
establishment in one method is deliberate: the planned later variant (the
winning bidder declares trump before any card is played, plus a "trump may only
be led on the first trick" rule) becomes a change to *where* that method is
called, not to any of the code that reads `_trump`. The engine logs the suit
once, on the play that sets it.

**Three readers of `_trump`:**

1. `LegalMoveContext.Trump` — the engine now passes `_trump` instead of the old
   hardcoded `null`, so the rules see the real trump.
2. `TrumpObligationRule` — filled in (was a stub).
3. `Trick` — the winner computation was upgraded (see below).

**`Trick.Winner` → `Trick.DetermineWinner(Suit? trump)`.** Changed from a
property to a method because the winner now depends on external round state
(the trump suit), which a property shouldn't silently require. New rule: if any
trump was played, the highest trump wins (trump beats every non-trump
regardless of led suit); otherwise the highest card of the led suit wins.

**Division of responsibility between the two obligation rules** (this was a
design correction — see below):

- `TrumpObligationRule` enforces ONLY the obligation to play *a* trump: when a
  player cannot follow the led suit but holds trump, non-trump cards are removed
  from the candidates. It says nothing about *which* trump.
- `MustRaiseRule` was generalized from "beat the highest *led-suit* card" to
  "beat the highest card of the **contested suit**", where the contested suit is
  read from the already-narrowed candidate set. So once `TrumpObligationRule`
  has narrowed the candidates to trump, must-raise naturally enforces
  *over-trumping* ("must beat an existing trump") — but ONLY when that optional
  rule is enabled. Over-trumping is a raising requirement, not a trump
  requirement, so it belongs here and nowhere else.

Pipeline order is therefore: `follow-suit → trump-obligation → must-raise`.
Each earlier rule establishes the contested suit that the next one refines.

**Verification.** Sandbox across seeds 42, 7, 99, 123, with must-raise off and
on — the 120-point checksum holds in all eight runs. Trump visibly changes play
(e.g. seed 42: a trump Ace now beats a led-suit Ten mid-game, and the round goes
62/58 instead of the pre-trump 83/37). The must-raise toggle changes some deals
(42, 99) and not others (7, 123), which is correct — it only bites when a player
holds a stronger card of the contested suit than they would otherwise play.

#### Known fragility (not fixed, flagged intentionally)

`MustRaiseRule` infers the contested suit from `candidates[0].Suit` and bails if
the candidates are of mixed suits. This is an **implicit contract** between
rules: must-raise relies on follow-suit / trump-obligation having already
narrowed the set to a single suit, but nothing enforces that. It is correct for
the current pipeline order, but reordering the rules or inserting a new rule
between them could make must-raise read the wrong contested suit silently. If
the pipeline grows, consider making the contested suit an explicit field on
`LegalMoveContext` (computed once by the engine) instead of inferred from the
candidate set.

#### Still simplified

The follow-suit/trump *legality* and the trick-winner logic are now trump-aware,
but there is still no bidding, no announcements (anuntari / 40-20), and no
conversion of captured card points into the 1–6 match score. A round still just
reports raw captured points out of 120.

### AI engines: structure, baseline relocation, greedy heuristic -- Christian

**AI engine structure.** Established a convention for AI opponents:

- Controllers live under `Assets/_Project/Scripts/Core/Players/AI/<Engine>/` and
  implement `IPlayerController` (pure C#, no Unity). The rules pipeline hands
  each one an already-legal move set, so an AI can never play illegally — it
  only chooses among legal options.
- Each engine has a plain-language design doc under `docs/ai/`, which is the
  **source of truth for its heuristics**: edit the doc first, then bring the
  code in line. `docs/ai/README.md` is the roster + the shared information
  model.

**Information model (what a controller may see).** Only public information, so
no AI can cheat structurally: its own hand, the current trick, the trump suit,
and (reserved for later) completed-trick history. Opponents' hands and the draw
pile's contents are never exposed. "Memory" is a graded, per-engine *consumption*
choice over the public trick history — low-memory engines look only at their own
won tricks, high-memory engines look at everything — not something the engine
enforces. The history substrate itself is deferred (YAGNI) until the first
engine that actually uses it; only `Trump` and `DrawPileCount` were added to the
context now.

**Baseline relocated.** `FirstLegalMoveController` moved from `Sandbox/` into
`Core/Players/AI/`. It was always going to be shipped game code (the real game
plugs AIs in), not a test-only driver, so it belongs in Core. The sandbox picks
it up automatically through the existing `<Compile Include>` glob — no csproj
change.

**`ITrickContext` extended.** Added `Suit? Trump` and `int DrawPileCount`, both
public round facts controllers need. `Trick` holds them (set by the engine when
each trick is created). Subtlety: on the first trick of a round, trump is null
when the leader chooses — correct, since the leader is the one establishing it;
the engine refreshes the trick's trump right after it is set so the second
player sees it.

**Greedy heuristic engine implemented.** See
[`docs/ai/greedy-heuristic.md`](ai/greedy-heuristic.md) for the full spec. In
one breath: when following, win with the cheapest card that wins, else dump the
lowest-point card (keeping trump); when leading during the draw phase, bank the
highest-value non-trump card (there is no follow-suit obligation yet and you
refill your hand, so hoarding is pointless); when leading after the pile is
empty, lead the cheapest non-trump; never lead trump. The draw-phase lead rule
also governs the opening lead, so trump ends up being the suit of the
highest-value non-trump card led — a deliberate simplification.

**Measured vs. the baseline (50 seeds each), 120-pt checksum held throughout:**

| Greedy seat         | Greedy wins | Baseline wins | Ties | Greedy avg | Baseline avg |
| ------------------- | ----------- | ------------- | ---- | ---------- | ------------ |
| Second player       | 30          | 19            | 1    | 63         | 56           |
| First player (lead) | 40          | 10            | 0    | 75         | 44           |

Honest reading: the engine is medium-tier by design, not a blowout from seat 1,
because the "cu carti de jos" draw phase suspends follow-suit for the first third
of the round, which caps how much a current-trick-only heuristic can control.
But its strength is concentrated in **leading**, so holding the opening lead
every round (seat 0) makes it dominant (40/10). This surfaces a fairness
requirement for later multi-round matches: **rotate the opening lead between
rounds**, mirroring the real game's rotating dealer. Not addressed yet (there is
no multi-round match loop).

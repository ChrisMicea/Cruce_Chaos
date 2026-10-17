# AI engines

This folder is the design source-of-truth for Cruce Chaos AI opponents. Each
engine gets one page here describing its logic in plain language. **The doc is
authoritative for the heuristics**: edit the doc first, then the implementation
is brought in line with it. The rationale (why a rule exists) lives here because
git diffs on `.cs` files capture *what* changed but rarely *why*.

Each engine's controller lives under
`Assets/_Project/Scripts/Core/Players/AI/<EngineName>/` and implements
`IPlayerController`. Controllers are pure C# (no Unity dependency) and are
handed an already-legal set of moves by the rules pipeline, so an AI can never
play illegally — it only chooses *among* legal options.

## What a controller can see (information model)

A controller chooses from `ChooseCard(self, legalMoves, context)`. The context
exposes **only public information**, so no AI can cheat structurally:

| Visible                                  | Hidden (never exposed)        |
| ---------------------------------------- | ----------------------------- |
| Its own hand (`self`)                    | Opponents' current hands      |
| The current trick in progress            | Contents/order of draw pile   |
| The trump suit                           |                               |
| Completed tricks this round (who won)    |                               |
| Each participant's captured-points tally |                               |

### Memory as a graded capability

The engine records **all** completed tricks truthfully. How much of that record
an AI consults models its *memory skill* — this is a consumption choice per
engine, not something the engine enforces:

- **Low-memory (honest player):** consults only the tricks **it won** — the
  equivalent of a real player glancing at their own capture pile. No counting
  skill assumed.
- **High-memory (card counter):** consults **all** completed tricks — every
  card that has been played — which in real life requires remembering.

Both views are public information; the distinction is purely how much a given AI
bothers to look.

## Roster

| Engine                                     | Doc                                        | Memory      | Status  |
| ------------------------------------------ | ------------------------------------------ | ----------- | ------- |
| First legal move (baseline)                | [first-legal-move.md](first-legal-move.md) | none        | done    |
| Greedy heuristic                           | [greedy-heuristic.md](greedy-heuristic.md) | low (unused)| done    |

(Add a row per new engine. "Status": `spec` = doc written, not yet implemented;
`done` = implemented and matches its doc.)

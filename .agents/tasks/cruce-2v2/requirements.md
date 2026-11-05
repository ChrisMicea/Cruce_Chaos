# Requirements — 2v2 ("Cruce") Playable Mode

## Summary

Add a playable 2v2 mode to the Cruce Unity card game, reusing the existing 1v1
scene and UI. For now 2v2 is **1 human + 1 AI vs 2 AI**, with teammates seated
**across** the table (Cruce = cross): team A = seats 0 (human) and 2, team B =
seats 1 and 3. Two new main-menu buttons launch 2v2 — a **Classic** variant
(`GameMode.TwoVsTwo`: 4 players, 6 cards each, deal-all, no draw pile) and a
**"With Surprises"** / draft variant (`GameMode.TwoVsTwoDraft`: 5 cards each,
4 leftover revealed and drafted one apiece).

The pure-C# Core is already fully N-player / 2v2-team capable and must **not** be
rewritten. The work lives almost entirely in the Unity presentation layer
(`Assets/_Project/Scripts/Unity/`), whose central controller `GameManager.cs` is
hard-coded for exactly two seats and must be generalized to N seats driven by the
`MatchConfig` for the selected `GameMode`. Scene/asset authoring that needs the
Unity editor (Inspector drag-wiring, RectTransform placement) is **out of scope
for direct YAML editing** and must instead be delivered as a precise manual-wiring
checklist for the user to execute by hand.

### Hard constraint (overriding)

**1v1 behavior MUST be preserved exactly.** When `PlayerCount == 2` the
generalized code path must reduce to today's 1v1 behavior with no observable
change: same bidding rotation and forced-1 rule, same opening-lead / trump /
marriage handling, same `(leader + i) % N` trick rotation, same `DrawFromPile`
behavior (draw pile only for `DealStrategy.DrawPilePerTrick`), same per-round and
per-match scoring numbers, and the sort / shuffle / auto-sort buttons and keyboard
input working on the human hand exactly as they do now. This is the primary
acceptance gate: any regression in the existing 1v1 scene fails the task.

### Verified ground truth (from reading the code, before any change)

- `GameModes.DefaultConfig` already returns correct configs for `TwoVsTwo` and
  `TwoVsTwoDraft` (confirmed in `Core/Game/GameMode.cs`), and the `MatchConfig`
  invariants validate both (4×6=24 / 0 leftover for DealAll; 4×5=20 / 4 leftover
  == PlayerCount for RevealAndDraft).
- The no-draw-pile rule set factory is **`RuleSetFactory.NoDrawPile(options)`**
  (confirmed in `Core/Rules/RuleSetFactory.cs` and in how `Sandbox/Program.cs`
  selects it for every non-`DrawPilePerTrick` strategy). 1v1 uses
  `RuleSetFactory.DrawPileRuleSet(options)`.
- `RoundEngine.cs` is the reference N-seat driver (bidding rotation + forced-1,
  trump declaration, `RunDraftIfNeeded` for RevealAndDraft, `(leader + i) % N`
  trick loop, `DrawFromPile` guarded to `DrawPilePerTrick` only). The Unity layer
  does not call it (it must pause for async human input) but must match its logic.
- The GreedyHeuristic AI lives at
  `CruceChaos.Core.Players.AI.GreedyHeuristic.GreedyHeuristicController` and is
  already used by `GameManager` for the single AI seat.
- Scoring is team-aware in Core via `ScoringUnit` (points sum over a team's
  participants) and `StandardRoundScorer` (VP = totalPoints / 33; bidding team
  made its bid → +earned, missed → −bid; non-bidding team always +earned).
- The Unity `GameManager`/`MatchManager`/`ScoreHUD` do **not** use `ScoringUnit`;
  they re-implement the same math over two scalars (human vs AI). This 2-entity
  assumption is what must become team-based.
- Headless verification works: `dotnet build CruceChaos.Core.csproj` succeeds, and
  `dotnet run --project Sandbox -- <seed> 2v2 round` and `... 2v2draft round` both
  run a full round and distribute exactly 120 points. The Unity assembly cannot be
  compiled headless (no editor available here).
- Scenes are listed in `ProjectSettings/EditorBuildSettings.asset` (SampleScene
  disabled, "Main Menu" and "1vsAI" enabled). Scene `.meta` files are plain
  `DefaultImporter` stubs carrying a single `guid`.

### Assumptions (flagged for the design review to confirm)

- A1. The new 2v2 scene is produced by **duplicating `1vsAI.unity`** rather than
  authored from scratch, so all existing UI/prefab wiring is inherited. The name
  used below is a placeholder (e.g. `2v2`); the final scene name is a design/user
  decision.
- A2. **One** 2v2 scene serves both variants; the Classic vs Surprises choice is
  carried into the scene (see Functional Area 1) rather than built as two scenes.
- A3. The human is always seat 0; the human's AI teammate is seat 2; opponents are
  seats 1 and 3. This follows `teamOfSeat {0,1,0,1}` and "teammates across".
- A4. Match target score for 2v2 stays at the config default (`TargetScore = 11`);
  no new match-length rules are requested.
- A5. "With Surprises" is the user-facing label for the draft variant; the exact
  on-screen wording is a UI-copy decision, not a logic requirement.

---

## Functional Requirements

Grouped by area. Each item is a testable statement. "Reduces to 1v1" means: with
`PlayerCount == 2` the behavior is byte-for-byte equivalent to the current build.

### Area 1 — Cross-scene mode carrier

- FR1.1 There is a mechanism to carry the chosen `GameMode`
  (`OneVsOne` / `TwoVsTwo` / `TwoVsTwoDraft`) from the main menu into the game
  scene, since Unity scene loads do not pass arguments.
- FR1.2 `GameManager.StartRound` reads the carried `GameMode` and builds its
  `MatchConfig` via `GameModes.DefaultConfig(mode, ruleOptions)` instead of the
  current hard-coded `GameMode.OneVsOne`.
- FR1.3 When no mode has been set (e.g. the game scene is opened directly in the
  editor), `GameManager` defaults to `GameMode.OneVsOne` so the existing 1v1
  workflow is unchanged.
- FR1.4 The carrier must survive the scene load that occurs between the main-menu
  button click and `GameManager.Start`, and must be resettable so a later launch
  of a different mode is not polluted by the previous choice.

### Area 2 — N-seat generalization of `GameManager`

- FR2.1 `GameManager` builds `PlayerCount` players and `PlayerCount` controllers
  from the active `MatchConfig`: seat 0 = human (`HumanPlayerController`), every
  other seat = a `GreedyHeuristicController`. One controller instance per AI seat.
- FR2.2 Per-round state currently stored as scalar pairs
  (`_humanParticipant`/`_aiParticipant`, `_leaderIndex` 0/1, `_bidWinnerSeat` 0/1)
  becomes seat-indexed collections of length `PlayerCount`.
- FR2.3 The bidding phase iterates over all `PlayerCount` seats using the same
  active-seat cycle, min-legal-bid validation, pass handling, and **forced-1**
  rule (last passer forced to 1) as `RoundEngine.RunBidding`. The human seat routes
  through the existing `BidPanel` + `HumanPlayerController.BeginBid` path; AI seats
  call `GreedyHeuristicController.PlaceBid`.
- FR2.4 Trump declaration asks the bid winner's controller: human → existing
  `TrumpPickerPanel` path; AI → `GreedyHeuristicController.DeclareTrump`.
- FR2.5 The trick loop plays `PlayCount` cards per trick in
  `(leaderIndex + i) % PlayerCount` order, matching `RoundEngine.PlayOneTrick`.
  Marriage announcement is offered only to the current leader (human via
  `MarriagePanel`, AI via controller), with the opening-lead restriction to a
  trump marriage preserved.
- FR2.6 Legal-move computation uses the rule set selected by the config's
  `DealStrategy`: `DrawPileRuleSet(options)` for `DrawPilePerTrick` (1v1),
  **`NoDrawPile(options)`** for `DealAll` and `RevealAndDraft` (both 2v2 variants).
- FR2.7 `DrawFromPile` runs **only** when `DealStrategy == DrawPilePerTrick`. For
  `DealAll` and `RevealAndDraft` there is no per-trick draw and no draw-pile count
  display (deck is empty after the deal/draft). This matches `RoundEngine`.
- FR2.8 Trick resolution awards the trick's points to the winning participant and
  sets `leaderIndex` to the winner's seat (not a 0/1 human-won branch).
- FR2.9 Round scoring is computed **per team** from the seats' `TeamOfSeat`
  grouping (captured + marriage points summed per team), producing team VP deltas
  that match `StandardRoundScorer` for any team count — including the 1v1 case
  where each seat is its own team.
- FR2.10 **1v1 reduction invariant:** with `PlayerCount == 2`,
  `DealStrategy == DrawPilePerTrick`, `teamOfSeat {0,1}`, the generalized bidding,
  trump, trick loop, draw, and scoring produce identical results and identical
  on-screen behavior to the current build.
- FR2.11 The sort / shuffle / auto-sort handlers (`OnShuffleHandClicked`,
  `OnAutoSortToggleClicked`, `ApplyAutoSort`, `RefreshAutoSortButton`) and the
  `_handDisplayOrder` tracking continue to operate on the **human** hand only,
  unchanged, in every mode. Keyboard input (`KeyboardInputHandler`) continues to
  drive the human hand and bid panel.

### Area 3 — Draft / "With Surprises" variant with human UI choice

- FR3.1 When `DealStrategy == RevealAndDraft`, after trump is declared and before
  the trick loop, `GameManager` runs a draft phase matching
  `RoundEngine.RunDraftIfNeeded`: reveal all leftover cards publicly, then starting
  at the bid winner and going `(bidWinner + i) % PlayerCount`, each seat drafts
  exactly one revealed card into its hand.
- FR3.2 AI seats draft via `GreedyHeuristicController.ChooseDraftCard`.
- FR3.3 The **human must choose** its draft card through the UI. The current
  `HumanPlayerController.ChooseDraftCard` (logs a warning and auto-picks
  `available[0]`) must be replaced by a Begin/Submit async pattern consistent with
  the other human decisions, backed by a draft-selection UI showing the revealed
  cards.
- FR3.4 After the human (or any seat) drafts, the revealed card is removed from the
  offer and added to that seat's hand; the human's `_handDisplayOrder` is updated
  and the human hand is rebuilt/re-fanned, with auto-sort and trump highlight
  reapplied, so the drafted card appears correctly.
- FR3.5 A defensive fallback (take first available) is retained if a controller
  returns a card not on offer, matching `RoundEngine`.

### Area 4 — Opponent / teammate face-down hand display

- FR4.1 For every non-human seat (1, 2, 3) the game renders a **face-down** fanned
  hand that shrinks as that seat plays cards, generalizing today's single
  `RebuildAIHand`.
- FR4.2 Each non-human seat renders at a distinct table position with an
  appropriate rotation: seat 1 (right opponent), seat 2 (top teammate, across from
  the human), seat 3 (left opponent). In the 1v1 case a single opponent seat
  renders at the existing top position with the existing layout, unchanged.
- FR4.3 Playing a card from a non-human seat removes one face-down card from that
  seat's fan (generalizing `RemoveAICardBack`).
- FR4.4 Hand-area RectTransforms for the extra seats are **scene assets**: the
  code references them by seat index, but their creation/placement is manual
  editor work (see Area 7 and the manual-wiring checklist). The code must degrade
  gracefully (skip rendering, no crash) when a seat's hand area is not wired.

### Area 5 — Team-based `ScoreHUD` + `MatchManager`

- FR5.1 `ScoreHUD` displays **two team scores** (Your Team vs Opponents) rather
  than strictly "You" vs "AI". The per-round result banner, VP deltas, and running
  match standings are expressed per team.
- FR5.2 `MatchManager` tracks **team-based** VP (`_humanMatchScore` /
  `_aiMatchScore` become the human's team vs the opposing team) using the same
  made/missed-bid VP rule, and its match-over condition (reach target AND lead by
  ≥ 2) is evaluated on team scores.
- FR5.3 In 1v1, each team is a single player, so the team-based HUD and
  `MatchManager` produce **identical numbers** to today. Label wording in 1v1 must
  remain acceptable (see Open Question OQ3).
- FR5.4 Round/match scoring reported to `MatchManager` and shown by `ScoreHUD` must
  agree with `StandardRoundScorer` for both team counts.

### Area 6 — Two main-menu buttons

- FR6.1 The main menu gains two buttons that launch 2v2: **"2v2 Classic"**
  (→ `GameMode.TwoVsTwo`) and **"2v2 — With Surprises"** (→ `GameMode.TwoVsTwoDraft`).
- FR6.2 Both buttons set the carried mode (Area 1) and load the **same** new 2v2
  scene. The existing 1v1 button and its behavior are unchanged.
- FR6.3 The menu wiring reuses the existing patterns (`MainMenuController` methods
  and/or `MenuButtonClick`), extended as needed to set the mode before the scene
  load. If `MenuButtonClick` alone cannot set the mode, menu buttons route through
  controller methods that set the mode then load the scene.

### Area 7 — New 2v2 Unity scene scaffold + manual-wiring checklist

- FR7.1 A new scene file for 2v2 is created by duplicating `1vsAI.unity`, with a
  **regenerated GUID** in its `.meta`, so Unity does not see a duplicate-GUID
  collision.
- FR7.2 The new scene is **registered in `EditorBuildSettings.asset`** (enabled),
  alongside the existing scenes, so it can be loaded by name at runtime.
- FR7.3 Main-menu button **scene-name strings** are set to the new scene's name.
- FR7.4 Anything that cannot be safely and verifiably edited as YAML — adding the
  seat-1/2/3 hand-area RectTransforms, the draft-selection UI, re-labelling HUD
  text, and all Inspector reference drag-wiring for the new fields on
  `GameManager` / `ScoreHUD` / `MatchManager` — is delivered as a **precise,
  ordered manual-wiring checklist** telling the user exactly what to add/wire in
  the Unity editor. The agent must not fake these in YAML.
- FR7.5 The checklist explicitly distinguishes edits the agent makes in YAML
  (duplicate scene, regen `.meta` GUID, build-settings registration, menu scene-name
  strings) from edits the user must make by hand (Inspector wiring, RectTransform
  placement, new UI objects).

---

## Non-Functional Requirements

- NFR1. **No Core rewrites.** `Assets/_Project/Scripts/Core/` is read-only for this
  task except for a genuine bug that provably blocks 2v2; any such change must be
  justified and minimal.
- NFR2. **Headless verifiability.** Core changes (if any) must keep
  `dotnet build CruceChaos.Core.csproj` green and the Sandbox `2v2` / `2v2draft`
  rounds distributing 120 points. Unity C# cannot be compiled headless, so its
  correctness is argued by (a) compiling against Core, (b) self-review showing the
  N-seat paths reduce to the known-good 1v1 paths at `PlayerCount == 2` and match
  `RoundEngine` for N seats, and (c) the manual-wiring checklist.
- NFR3. **Pattern fit.** New Unity code follows existing conventions: the
  Begin/Submit async callback bridge in `HumanPlayerController`, `[SerializeField]`
  Inspector references, coroutine-driven phases in `GameManager`, and the existing
  logging via `GameLogger`.
- NFR4. **Scope containment.** Only `Assets/_Project/Scripts/Core/`,
  `Assets/_Project/Scripts/Unity/`, and `Assets/Scenes/` (plus
  `ProjectSettings/EditorBuildSettings.asset` for scene registration) are touched.
- NFR5. **No unrequested gameplay changes.** Deal sizes, rule options, bid ceiling,
  and target score come from `GameModes.DefaultConfig`; the task adds no new rules.

---

## Acceptance Criteria

1. From the main menu, clicking **"2v2 Classic"** loads the new 2v2 scene and
   starts a 4-seat round with config `PlayerCount 4`, `CardsPerPlayer 6`,
   `DealStrategy.DealAll`, `TeamOfSeat {0,1,0,1}`, no draw pile.
2. From the main menu, clicking **"2v2 — With Surprises"** loads the same scene and
   starts a 4-seat round with `CardsPerPlayer 5`, `DealStrategy.RevealAndDraft`,
   `TeamOfSeat {0,1,0,1}`, running the reveal-and-draft phase before trick play.
3. In both 2v2 variants, bidding cycles through all four seats in order with
   correct min-legal-bid/pass handling and the forced-1 rule; the bid winner
   declares trump; the human uses the existing bid/trump/marriage panels and each
   AI seat decides via `GreedyHeuristicController`.
4. In both 2v2 variants, each trick plays four cards in
   `(leader + i) % 4` order, the correct seat wins and leads next, and **no**
   per-trick draw occurs (deck empty after deal/draft).
5. At round end, two team totals are computed from `TeamOfSeat {0,1,0,1}`
   (human+seat-2 vs seat-1+seat-3), VP deltas follow the made/missed-bid rule, and
   the team whose summed points are shown matches a Sandbox `2v2` / `2v2draft` run
   for the same deal (spot-checked: totals sum to 120).
6. The **1v1** button still loads `1vsAI` and plays exactly as before: same
   bidding, forced-1, opening-lead trump rule, marriage handling, 8+8 draw-pile
   deal, per-trick draw of two cards (winner first), and identical round/match
   scoring and HUD numbers. No regression is observable.
7. Sort, shuffle, and auto-sort buttons and all keyboard bindings operate on the
   human hand correctly in **all three** modes (verified at least in 1v1 and one
   2v2 variant).
8. Non-human seats 1, 2, and 3 each show a face-down fan at a distinct, readable
   table position; each fan shrinks by one as that seat plays; in 1v1 the single
   opponent fan is unchanged.
9. In the draft variant, the human selects its draft card through the UI (not an
   auto-pick), the selected card enters the human hand and fan correctly, and AI
   seats draft via the controller; the draft order starts at the bid winner and
   proceeds clockwise.
10. `ScoreHUD` and `MatchManager` show two **team** scores in 2v2 and correct
    single-entity scores in 1v1; the match ends when a team reaches the target and
    leads by ≥ 2.
11. `dotnet build CruceChaos.Core.csproj` succeeds with no new warnings/errors, and
    `dotnet run --project Sandbox -- <seed> 2v2 round` and `... 2v2draft round`
    still each distribute 120 points. (If Core was untouched, this is a
    no-regression check.)
12. The new scene exists, has a **unique** `.meta` GUID, is **registered and
    enabled** in `EditorBuildSettings.asset`, and the menu buttons reference its
    exact name.
13. A **manual-wiring checklist** is delivered that lists every Unity-editor action
    the user must perform (new hand-area RectTransforms for seats 1/2/3, draft UI,
    HUD relabelling, and every new Inspector reference), clearly separated from the
    YAML edits the agent performed. Nothing requiring Inspector wiring is faked in
    YAML.

---

## Open Design Questions (the design phase must resolve)

- OQ1. **No-draw-pile rule set factory.** Confirmed to be
  `RuleSetFactory.NoDrawPile(options)` by reading the factory and Sandbox. The
  design must state where `GameManager` chooses between `DrawPileRuleSet` and
  `NoDrawPile` based on `config.DealStrategy` (mirroring `Sandbox.RunSingleRound`).
- OQ2. **Draft interaction UI shape.** What does the human's draft chooser look
  like — a dedicated panel listing the revealed cards, reuse of the trick/hand
  area, or clickable revealed-card views? And the Begin/Submit contract that
  replaces the auto-pick `ChooseDraftCard`. Also: how/whether the four revealed
  cards are shown to the player publicly before/while drafting.
- OQ3. **1v1 vs 2v2 HUD label choice.** Team-based labels must still read correctly
  in 1v1 (where a "team" is one player). Decide the labelling strategy: generic
  "Your Team / Opponents" everywhere, or mode-aware labels ("You / AI" in 1v1 vs
  team names in 2v2). This governs `ScoreHUD` and `MatchManager` text.
- OQ4. **Scene edits: safe-in-YAML vs manual.** Exactly which parts of duplicating
  and registering the scene are safe to do as YAML (scene copy, `.meta` GUID
  regen, `EditorBuildSettings` entry, menu scene-name strings) versus which must be
  manual (new RectTransforms, UI objects, all Inspector reference wiring). The
  design must draw this line precisely and feed it into the manual-wiring checklist.
- OQ5. **Mode carrier mechanism.** Choose the concrete carrier (e.g. a tiny static
  holder, a `DontDestroyOnLoad` singleton, or `PlayerPrefs`) and justify it,
  including how it defaults to 1v1 and how it resets between launches (FR1.3/FR1.4).
- OQ6. **Seat-to-screen-position mapping.** The exact mapping of seats 1/2/3 to
  table positions and fan rotations for face-down hands (right / top / left), and
  whether a data-driven per-seat layout replaces the two hard-coded fan methods.

---

## Out of Scope

- Rewriting or redesigning Core engine logic (`RoundEngine`, scoring, rules). Core
  is already N-player/2v2 capable and is treated as read-only barring a blocking
  bug.
- Human-vs-human or online/multiplayer 2v2; the human partner and all opponents are
  AI for now.
- The other modes present in the enum (`OneVsOneVsOne`, `FourPlayerIndividual`) —
  not requested, though the N-seat generalization should not preclude them later.
- New AI behavior or difficulty levels; 2v2 reuses the existing
  `GreedyHeuristicController` for every AI seat.
- New gameplay rules, bid ceilings, deal sizes, or match lengths beyond the
  `GameModes.DefaultConfig` presets.
- Performing the Unity-editor Inspector wiring, RectTransform placement, or new UI
  object creation on the user's behalf (delivered as a checklist instead).
- Art, animation, sound, and visual polish beyond reusing existing prefabs/sprites
  and placing the extra face-down hands.

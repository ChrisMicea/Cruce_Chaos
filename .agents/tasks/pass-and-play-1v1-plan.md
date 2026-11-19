# Implementation Plan — 1v1 Pass-and-Play (hotseat)

Design: OPTION B — generalize seat identity. Which seats are human becomes a
set (`_humanSeats` in GameManager, fed by `GameSelection.HumanSeats`), not a
hardcoded seat 0. A single `_activeHumanSeat` names the human seat currently
deciding; `_humanController` serves each human seat serially (the coroutine
only ever has one human mid-decision). This extends unchanged to future
2v2/1v1v1 hotseat when a seat-selection screen fills `HumanSeats`.

## Verification facts discovered during exploration

- `dotnet` 10.0.115 is installed. The Unity scripts live in the asmdef-backed
  project **`CruceChaos.Unity.csproj`** (NOT `Assembly-CSharp.csproj`, which has
  zero `_Project` files). `dotnet build CruceChaos.Unity.csproj` succeeds today
  (0 errors) against the Unity 6000.6.5f1 DLLs under
  `/home/chris/Unity/Hub/Editor/6000.6.5f1/...`. **This is the real
  type-check/compile command for every step below.**
- `CruceChaos.Unity.csproj` uses **explicit `<Compile Include>` entries** (not a
  glob). A brand-new source file is invisible to `dotnet build` until its entry
  exists. Unity regenerates the csproj on next focus, but because we verify with
  `dotnet build` BEFORE opening Unity, step 2 adds the `PassDevicePanel.cs`
  compile entry manually. The csproj is a generated file, not a
  `.unity/.prefab/.meta`, so editing it is within constraints; treat it as a
  verification-only edit that Unity will overwrite harmlessly.
- Core APIs to REUSE unchanged (do not reimplement): `RoundParticipant.Hand`
  (`.Cards`, `.Contains`, `.MarriageSuits()`, `.IsEmpty`, `.Count`),
  `IPlayerController`, `GreedyHeuristicController`, `LegalMoveEvaluator`,
  `StandardRoundScorer`, `MatchConfig` (`PlayerCount`, `TeamOfSeat`,
  `StartingBidderSeat`, `IsTeamPlay`, `DealStrategy`), `GameModes.DefaultConfig`.
  The task only changes the Unity-layer seat/identity/rendering/gating.

## Line-by-line inventory of seat-0-is-human sites in GameManager.cs

(Line numbers from the file read for this plan; verify by context, not by the
number alone, since earlier edits shift later lines.)

| Line | Current | Seat-aware replacement |
|------|---------|------------------------|
| 121 | `_humanParticipant => _participants[0]` | Add `_localParticipant => _participants != null ? _participants[_activeHumanSeat] : null`; keep nothing hardcoded to 0. Replace all `_humanParticipant` uses with `_localParticipant`. |
| 186–189 | `_controllers[seat] = seat == 0 ? _humanController : new GreedyHeuristicController()` | `_controllers[seat] = _humanSeats[seat] ? (IPlayerController)_humanController : new GreedyHeuristicController()` |
| 203 | `SeatName`: `if (seat == 0) return "You";` | Keep — "You" is the local-perspective label. See step 3 note: naming stays seat-relative; with one human at seat 0, output is identical. For hotseat, each human seat is still "You" from its own turn's perspective (acceptable for 1v1; the pass gate names the *next* seat via `SeatName`). Leave logic as-is. |
| 268 | `_humanParticipant.Hand.ToString()` (LogDeal) | `_localParticipant.Hand.ToString()` (active seat at round start = first human seat). |
| 450 | `if (seat == 0) // human` in BiddingPhase | `if (_humanSeats[seat])`. Before showing the bid panel for a human seat, run the pass-device gate if the previous actor was a *different* human seat (step 3). Set `_activeHumanSeat = seat` and render that seat's hand before the panel. |
| 522, 539 | `if (_bidWinnerSeat == 0)` in DeclareTrumpPhase | `if (_humanSeats[_bidWinnerSeat])`. Gate before the human declarer if bid winner is a human seat different from the last device holder. `declarer` string: `_humanSeats[_bidWinnerSeat] ? "You declare" : $"{SeatName(_bidWinnerSeat)} declares"`. |
| 582, 608 | `if (seat == 0)` in DraftPhase (×2) | `if (_humanSeats[seat])`. Keep draft correct for future modes even though 1v1 never drafts. Gate before a human drafter when the previous actor was a different human seat; set `_activeHumanSeat = seat` and rebuild face-up hand before `BeginDraft`. |
| 717 | `bool isHuman = (seat == 0);` in PlayOneTrick | `bool isHuman = _humanSeats[seat];` |
| 721–723 | `if (isHuman) HumanPlays(...)` | Before entering `HumanPlays`, if `isHuman` and the previous actor was a different human seat, run the gate and set `_activeHumanSeat = seat`; then render active hand. Pass `seat` into `HumanPlays`. |
| 747, 769, 780, 802 | `_humanParticipant` in HumanPlays | `_localParticipant` (now keyed to `_activeHumanSeat`). `PlayCard(0, ...)` at 802 becomes `PlayCard(_activeHumanSeat, _localParticipant, chosen)`. |
| 866–868 | `if (seat == 0) RemoveCardViewFromHand else RemoveOpponentCardBack` in PlayCard | `if (seat == _activeHumanSeat)` — the face-up hand belongs to the active human seat; any other seat (AI or a not-currently-active human) shows card backs. |
| 933–935 | `if (seat == 0) _handDisplayOrder.Add(drawn)` in DrawFromPile | `if (seat == _activeHumanSeat)`. |
| 951–953 | `if (participant == _humanParticipant) _handDisplayOrder.AddRange(dealt)` in DealCards | `if (participant == _localParticipant)` (active = first human seat during the deal). |
| 965, 967 | `_humanParticipant.Hand` in RebuildHumanHand | `_localParticipant.Hand`. |
| 1059 | `_participants[seat]` in BuildCardBackFan | No change (renders a given non-active seat's backs). RebuildAIHand must skip `_activeHumanSeat` instead of only skipping seat 0 (step 3). |

Sites that are NOT human checks and must stay unchanged: `activeCount == 0`
(495), `isLeading = (i == 0)` (718), `count == 0` guards (971, 1059, 1183, 1218),
`_deck.Count == 0` (921). `MyTeamIndex() => _config.TeamOfSeat[0]` (line ~334)
and `ResolveTrick` win/lose feedback: keep reading the local human's team so
single-human output is byte-identical; for 1v1 hotseat both seats are human and
on opposing teams — drive the flash from `_config.TeamOfSeat[_activeHumanSeat]`
(the seat about to receive the device / who just acted) so "my side won" reads
from the current viewer. Document this as the chosen perspective rule.

---

## Steps

- [ ] 1. Extend `GameSelection` with the human-seat contract and the 1v1 flag.
      Add `public static bool[] HumanSeats { get; set; } = null;` and
      `public static bool PassAndPlay { get; set; } = false;`, each XML-documented
      in the existing `SelectedMode` comment style. Document: `HumanSeats` non-null
      and length == PlayerCount wins; else if `PassAndPlay` → GameManager marks all
      seats human (1v1 ⇒ {0,1}); else single human at seat 0 (direct editor Play
      still works). Reset semantics (document + implement): GameManager clears the
      flags at the end of `Start()` after reading them (`PassAndPlay = false;
      HumanSeats = null;`) so a later direct scene entry is never stale. Note this
      in the doc comment so the menu-set-then-read-then-reset contract is explicit.
      Files: Assets/_Project/Scripts/Unity/GameSelection.cs
      Verify: `dotnet build CruceChaos.Unity.csproj -nologo -clp:ErrorsOnly` → 0 errors.

- [ ] 2. Create `PassDevicePanel` mirroring `BidPanel` conventions, and register it
      for compilation. Namespace `CruceChaos.Unity`; `MonoBehaviour`;
      `[SerializeField]` TMPro `_heading`, TMPro `_subtitle`,
      `UnityEngine.UI.Button _yesButton`, optional TMPro `_whoIsNextLabel`.
      Defaults: heading "Have you passed the device?", subtitle
      "so your opponent can't see your hand" (set in `Awake`/inspector-overridable).
      `Awake` wires `_yesButton.onClick.AddListener(OnYesClicked)`; `Start` calls
      `gameObject.SetActive(false)`. API: `public void Show(string nextPlayerName,
      System.Action onConfirmed)` — sets `_whoIsNextLabel?.text` (e.g.
      "Pass to: {nextPlayerName}"), stores callback, `SetActive(true)`;
      `public void Hide()` → `SetActive(false)`. `OnYesClicked` guards against
      double-invoke (bool `_consumed`), hides, invokes the stored callback once,
      clears it. Include a BidPanel-style SCENE SETUP doc comment describing the
      manual structure: full-screen opaque blocking Image (raycast target covering
      the canvas) as the panel root, with heading + subtitle + Yes button children.
      Then add `<Compile Include="Assets/_Project/Scripts/Unity/PassDevicePanel.cs" />`
      to `CruceChaos.Unity.csproj` (next to the BidPanel entry) so `dotnet build`
      sees the new file; note in the plan this entry is what Unity would auto-generate.
      Files: Assets/_Project/Scripts/Unity/PassDevicePanel.cs,
      CruceChaos.Unity.csproj
      Verify: `dotnet build CruceChaos.Unity.csproj -nologo -clp:ErrorsOnly` → 0
      errors and the new file compiles (confirm by temporarily introducing a
      deliberate typo and seeing the build flag PassDevicePanel.cs, then revert).

- [ ] 3. Generalize GameManager's seat identity, rendering, and add the pass gate.
      This is one coherent change to one file; do it as a unit so the file stays
      compilable. Sub-parts, all in GameManager.cs:
      (a) Add fields `private bool[] _humanSeats;`, `private int _activeHumanSeat;`,
          and `private int _lastDeviceSeat = -1;` (tracks who last held the device,
          for gate decisions). Add `[Header("UI Panels")]`-grouped
          `[SerializeField] private PassDevicePanel _passDevicePanel;`.
      (b) Replace accessor at line 121 with
          `_localParticipant => _participants != null ? _participants[_activeHumanSeat] : null;`
          and replace every `_humanParticipant` usage (lines 268, 747, 769, 780,
          802, 951, 965, 967) with `_localParticipant`.
      (c) In `Start()`: after `_playerCount` is known, build `_humanSeats`:
          if `GameSelection.HumanSeats != null && GameSelection.HumanSeats.Length
          == _playerCount` use it; else if `GameSelection.PassAndPlay` set every
          element true; else only index 0 true. Set `_activeHumanSeat` to the first
          true index (fallback 0). Assign controllers via `_humanSeats[seat]`
          (replaces lines 186–189). At the END of `Start()` reset
          `GameSelection.PassAndPlay = false; GameSelection.HumanSeats = null;`.
      (d) In `StartRound()` set `_activeHumanSeat` to the first human seat and
          `_lastDeviceSeat = -1` so a fresh round re-gates on the first human.
      (e) Add `private IEnumerator PassDeviceGate(int nextSeat)`: hide/clear ALL
          face-up cards (call `ClearAllCardViews()` or equivalent so no hand shows),
          show `_passDevicePanel?.Show(SeatName(nextSeat), () => done = true)`, yield
          `WaitUntil(() => done)`, hide panel, set `_activeHumanSeat = nextSeat`,
          then `ApplyAutoSort(); RebuildHumanHand(); RebuildAIHand();
          ApplyTrumpHighlights();` so the new seat's hand renders face-up and the now-
          inactive human seat renders as backs. Null-guard with `_passDevicePanel?.`.
          Add `private IEnumerator MaybeGate(int nextSeat)` helper: if
          `_humanSeats[nextSeat]` AND `_lastDeviceSeat >= 0` AND
          `_lastDeviceSeat != nextSeat` AND `_humanSeats[_lastDeviceSeat]` → yield
          the gate. In ALL cases where a human seat takes control, after it acts set
          `_lastDeviceSeat = nextSeat`. Do NOT gate on transitions to/from AI, nor
          when the same human seat acts twice with no other human between.
      (f) Fire `MaybeGate(seat)` / set active seat at these points:
          - BiddingPhase (line 450): before `_bidPanel?.Show`, for a human seat,
            `yield return StartCoroutine(MaybeGate(seat)); _activeHumanSeat = seat;`
            then render (the gate already renders when it fires; when it does not
            fire, add an explicit render so the correct face-up hand is shown).
            Update `_lastDeviceSeat = seat` after the bid resolves for a human seat.
          - DeclareTrumpPhase (line 522): change to `if (_humanSeats[_bidWinnerSeat])`;
            gate before the picker if the declarer is a human seat and differs from
            `_lastDeviceSeat`; set `_activeHumanSeat = _bidWinnerSeat`; update
            `_lastDeviceSeat`. Fix the `declarer` string (line 539) to the
            `_humanSeats[...]`-based form.
          - DraftPhase (lines 582, 608): change both `seat == 0` to
            `_humanSeats[seat]`; gate before a human drafter; set active seat; update
            `_lastDeviceSeat`. (Dead path for 1v1 but kept correct.)
          - PlayOneTrick (line 717): `bool isHuman = _humanSeats[seat];`. Before
            `HumanPlays`, `yield return StartCoroutine(MaybeGate(seat));
            _activeHumanSeat = seat;` then update `_lastDeviceSeat = seat` after the
            human plays. Change `HumanPlays(...)` signature to take `int seat` and use
            it (replace the hardcoded `PlayCard(0, ...)` at 802 with
            `PlayCard(_activeHumanSeat, _localParticipant, chosen)`).
      (g) Rendering follows the active seat: in `PlayCard` (line 866) use
          `if (seat == _activeHumanSeat)`; in `DrawFromPile` (line 933) use
          `if (seat == _activeHumanSeat)`; in `RebuildAIHand` iterate all seats and
          render backs for every seat EXCEPT `_activeHumanSeat` (today it skips seat
          0 implicitly via the 1..N-1 loop — change the loop/guard to
          `if (seat == _activeHumanSeat) continue;` and map remaining seats to
          `_opponentHandAreas`). `RebuildHumanHand` reads `_localParticipant`.
      (h) `_handDisplayOrder` is per-hand state. On active-seat change (inside the
          gate, after setting `_activeHumanSeat`): `_handDisplayOrder.Clear();
          _handDisplayOrder.AddRange(_localParticipant.Hand.Cards);` then
          `ApplyAutoSort(); RebuildHumanHand();` so sort/shuffle/auto-sort,
          `ApplyTrumpHighlights()`, and `_keyboardInput?.SetCardViews(...)` (driven
          by RebuildHumanHand/RefanHumanHand) all operate on the new seat's hand.
      (i) Perspective: `MyTeamIndex()` and `ResolveTrick` win/lose feedback read the
          local/active human seat (`_config.TeamOfSeat[_activeHumanSeat]`), keeping
          single-human-at-seat-0 output identical. Document the chosen rule in a
          brief comment.
      Keep every Core API call untouched; change nothing in scoring/legal-move/
      bidding/deck logic.
      Files: Assets/_Project/Scripts/Unity/GameManager.cs
      Verify: `dotnet build CruceChaos.Unity.csproj -nologo -clp:ErrorsOnly` → 0
      errors. Plus the manual self-review in step 6 (gate fire/no-fire matrix).

- [ ] 4. Add the hotseat menu entry in `MainMenuController`. Add
      `public void OnOneVsOneHotseatClicked()` mirroring `OnOneVsOneClicked`:
      `GameSelection.SelectedMode = GameMode.OneVsOne; GameSelection.PassAndPlay =
      true; BeginTransition(_oneVsOneScene);`. In every existing singleplayer entry
      (`OnOneVsOneClicked`, `OnTwoVsTwoClassicClicked`, `OnTwoVsTwoSurprisesClicked`,
      `OnOneVsOneVsOneClicked`) add a defensive `GameSelection.PassAndPlay = false;`
      before `BeginTransition`. Do NOT add or restructure menu columns/scenes — this
      only adds one handler method the user will wire to a new button manually.
      Files: Assets/_Project/Scripts/Unity/MainMenuController.cs
      Verify: `dotnet build CruceChaos.Unity.csproj -nologo -clp:ErrorsOnly` → 0 errors.

- [ ] 5. Document the seat model in `agents.md`. Append a short section in the file's
      plain style: GameManager is seat-aware via `_humanSeats`/`_activeHumanSeat`;
      `GameSelection.HumanSeats` (bool[] indexed by seat) is the contract a future
      seat-selection screen fills for 2v2/1v1v1 hotseat; `GameSelection.PassAndPlay`
      is the 1v1 convenience flag (all seats human); game scenes are REUSED for
      singleplayer vs pass-and-play — only menu/selection scenes are added for new
      choices, never duplicated game scenes.
      Files: agents.md
      Verify: re-read the appended section; confirm it names `_humanSeats`,
      `_activeHumanSeat`, `HumanSeats`, `PassAndPlay`, and the scene-reuse rule. (Doc
      only — no build impact.)

- [ ] 6. Full build + manual self-review (no runtime/Unity-play available here).
      Run the build once more on the whole Unity assembly. Then self-review the gate
      logic against this matrix and confirm each holds in code:
      (1) single human at seat 0, no flags → `_humanSeats = {true,false}`,
          `_lastDeviceSeat` starts -1 so NO gate fires on the first human (seat 0 is
          first actor), behaviour identical to today;
      (2) 1v1 hotseat → `_humanSeats = {true,true}`; gate fires before seat 1's first
          action following seat 0, and before seat 0 again next trick; never fires
          AI→human or human→AI (no AI present in 1v1 hotseat) and never when the same
          seat acts twice consecutively;
      (3) face-up hand always equals `_activeHumanSeat`'s hand after any gate;
      (4) no `.unity/.prefab/.meta` file was touched (only the four source files +
          the generated csproj verification entry).
      Files: (none new)
      Verify: `dotnet build CruceChaos.Unity.csproj -nologo -clp:ErrorsOnly` → 0
      errors. Runtime behaviour (actual pass-and-play flow, panel blocking) is NOT
      verifiable in this environment — it requires pressing Play in the Unity editor
      after the manual scene wiring in the report; state this honestly rather than
      claiming a runtime check.

## Manual Unity-editor steps for the report (user does these; do NOT edit scenes)

1. In the 1v1 game scene (`1vsAI`), create a full-screen panel under the Canvas:
   add a UI `Image` that covers the whole canvas, opaque, **Raycast Target = on**
   (this blocks clicks on the hand underneath). Name it `PassDevicePanel` and add
   the `PassDevicePanel` component.
2. As children add: a TMP heading text ("Have you passed the device?"), a TMP
   subtitle text ("so your opponent can't see your hand"), an optional TMP
   "who is next" label, and a `Button` ("Yes"). Wire the component's serialized
   fields (`_heading`, `_subtitle`, `_whoIsNextLabel`, `_yesButton`) to these.
   Leave the panel GameObject active in the hierarchy; its `Start()` deactivates it.
3. On the `GameManager` component, assign the new `_passDevicePanel` reference
   (under the "UI Panels" header) to this panel.
4. Add a new "1v1 Pass & Play" button to the Main Menu scene and wire its
   `OnClick` to `MainMenuController.OnOneVsOneHotseatClicked`. (No new column/scene
   this step, per the user.)
5. Let Unity recompile (it regenerates `CruceChaos.Unity.csproj`, which makes the
   manual compile-include entry from step 2 redundant — harmless). Press Play from
   the Main Menu, choose 1v1 Pass & Play, and confirm the blocking panel appears
   between the two human turns and hides each player's hand until "Yes".

---

## Verification performed (implementation iteration 1)

Compile WAS verifiable. `dotnet` 10.0.115 is installed. Built the real Unity
assembly repeatedly during the change:
`dotnet build CruceChaos.Unity.csproj -nologo -clp:ErrorsOnly` → **0 warnings,
0 errors** after every step and on the final state. Confirmed `PassDevicePanel.cs`
is actually in the compile graph by inserting a deliberate typo and seeing the
build flag that exact file, then reverted. The `CruceChaos.Unity.csproj`
compile-include edit is gitignored (generated file), so it is NOT committed —
Unity regenerates it, exactly as the plan noted.

Files changed (committed): `GameSelection.cs`, `PassDevicePanel.cs` (new),
`GameManager.cs`, `MainMenuController.cs`, `agents.md`. No `.unity`/`.prefab`/
`.meta` file was touched (verified via `git status --ignored`).

Manual self-review of the seat/gate logic:
- Every former `seat == 0` / `_humanParticipant` / `_bidWinnerSeat == 0` human
  check was replaced with `_humanSeats[...]` / `_localParticipant` /
  `seat == _activeHumanSeat`. Only `SeatName`'s `seat == 0 => "You"` label stays
  (documented local-perspective label). `grep` confirms no stale sites remain.
- Default single-human (no flags): `_humanSeats = {true,false}`,
  `_activeHumanSeat = 0`, `_lastDeviceSeat = -1`. Gate never fires (first human
  has no prior device holder; same seat each turn). Rendering, perspective
  (`MyTeamIndex` = `TeamOfSeat[0]`), and controller assignment are byte-identical
  to the original. `StartRound` opener logic resolves to seat 0 in round 1 and to
  the first human (seat 0) in later rounds — unchanged.
- 1v1 hotseat (`PassAndPlay` ⇒ `_humanSeats = {true,true}`): gate fires on every
  human→different-human handover — before seat 1's first bid following seat 0,
  before the trump declarer if it differs from the last actor, and before each
  human's trick turn when the other human acted last. It never fires on the first
  actor of a round, never on same-seat-twice, and (no AI present) never on an AI
  transition. `PassDeviceGate` calls `ClearAllCardViews()` BEFORE showing the
  panel and only switches the face-up render AFTER "Yes", so no hand is visible
  across the handover. `StartRound` seeds `_activeHumanSeat` to the round's
  starting bidder when human, so the opener's own hand is the first shown (no
  earlier seat's hand leaks at round start on rotated rounds).
- Active-seat change rederives `_handDisplayOrder` from the new seat's actual
  `Hand.Cards`, then `ApplyAutoSort` / `RebuildHumanHand` / `RebuildAIHand` /
  `ApplyTrumpHighlights` run, so sort/shuffle/auto-sort, trump highlight, and
  keyboard input all follow the active seat. `RebuildAIHand` now renders backs for
  every seat except `_activeHumanSeat`, mapping them to `_opponentHandAreas` in
  order (identical "seat 1 → area 0" mapping when the active seat is 0).
- `_passDevicePanel` is null-guarded, so a scene that hasn't wired it still runs
  (the gate just skips the blocking wait).

NOT verifiable here: actual runtime pass-and-play flow and the panel visually
blocking input require pressing Play in the Unity editor AFTER the manual scene
wiring above. That was not run in this environment.

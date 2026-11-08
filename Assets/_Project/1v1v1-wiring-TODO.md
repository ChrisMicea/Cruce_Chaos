# 1v1v1 — Unity Editor Wiring Checklist (Step D)

All code for 1v1v1 is done. Two kinds of editor work remain:

- **Part 1** — a one-time re-drag in the EXISTING 1v1 and 2v2 scenes, because the
  ScoreHUD changed from two fixed score labels to a `Score Labels` array.
- **Part 2** — create and wire the new `1v1v1` scene.
- **Part 3** — add the 1v1v1 button to the Main Menu.

Do them in order.

---

## Part 1 — Re-wire ScoreHUD in the EXISTING scenes (required)

The ScoreHUD no longer has "Human Points Label" / "Ai Points Label". It now has a
single **Score Labels** array (index 0 = the human's side, then opponents in team
order). Both existing scenes must point that array at their two labels, or the
scores will stop updating.

### 1v1 scene (`1vsAI.unity`)
- [ ] Open the scene, select the object with the **ScoreHUD** component.
- [ ] You'll see **Score Labels** (was the two old slots, now empty/size 0).
- [ ] Set **Score Labels → Size = 2**.
- [ ] **Element 0** → the label that used to be "Human Points Label" (your points).
- [ ] **Element 1** → the label that used to be "Ai Points Label" (AI points).
- [ ] Save.

### 2v2 scene (`2v2.unity`)
- [ ] Same as above: select ScoreHUD, **Score Labels → Size = 2**.
- [ ] **Element 0** → the "your team" label.
- [ ] **Element 1** → the "opponents" label.
- [ ] Save.

**MatchManager ALSO changed** — it now uses a per-team array too (same model as
ScoreHUD), so its match-score labels line up with the running-score labels. Wire
it in every scene:

### MatchManager labels (all scenes)
- [ ] 1v1 scene: select **MatchManager** → **Match Score Labels** → **Size = 2** →
      Element 0 = your match label, Element 1 = AI match label.
- [ ] 2v2 scene: **Match Score Labels** → **Size = 2** → Element 0 = your team,
      Element 1 = opponents.
- [ ] (1v1v1 scene: covered in Part 2.3b below — Size 3.)

> Both label arrays (ScoreHUD's Score Labels AND MatchManager's Match Score Labels)
> are indexed by the SAME team order: element 0 = you, then opponents in team order.
> Names are set from code at runtime (e.g. "You: 0", "Opponent A: 0"); whatever
> placeholder text ("New Text", etc.) the labels show in the editor is overwritten.

---

## Part 2 — Create and wire the 1v1v1 scene

### 2.1 Create the scene
- [ ] In the Project window, open `Assets/Scenes/`.
- [ ] Select `2v2.unity` (closest starting point — it already has the draft reveal
      area and opponent areas). Press **Ctrl+D** to duplicate.
- [ ] Rename the copy to **`1v1v1`** (exact — the menu loads the scene named `1v1v1`).
- [ ] Open `1v1v1.unity`.
- [ ] **File → Build Settings → Add Open Scenes** so `LoadScene("1v1v1")` works.

### 2.2 Opponent hand areas (2 opponents)
1v1v1 has two opponents. Screen layout: **seat 1 = LEFT, seat 2 = RIGHT** (you're
at the bottom; there is NO top/partner seat).

- [ ] In the Hierarchy you have three opponent areas from the 2v2 scene
      (Left / Top / Right). **Delete the Top area** — 1v1v1 has no partner seat.
      Keep the Left and Right areas.
- [ ] Select the **GameManager** object → **Opponent Hand Areas** → **Size = 2**.
- [ ] **Element 0** → the **LEFT** area  (seat 1)
- [ ] **Element 1** → the **RIGHT** area (seat 2)
- [ ] Leave each area's Z-rotation at 0 (the code tilts the cards: seat 1 +90°,
      seat 2 −90°).

> Order matters: Element 0 = seat 1 (left), Element 1 = seat 2 (right). The code
> rotates seat 1's fan to the left and seat 2's to the right automatically.

### 2.3 Score labels (3 players)
1v1v1 shows three running scores: you + two opponents.

- [ ] Select the **ScoreHUD** object → **Score Labels** → **Size = 3**.
- [ ] You already have 2 labels from the 2v2 duplicate. Duplicate one of them once
      (Ctrl+D) to get a third; reposition the three so they don't overlap
      (e.g. one near each player: bottom = you, left, right — or a tidy column).
- [ ] Assign them in order:
  - [ ] **Element 0** → your score label (shows "You: 0")
  - [ ] **Element 1** → left opponent's label (shows "Opponent A: 0")
  - [ ] **Element 2** → right opponent's label (shows "Opponent B: 0")

> The code fills these by team index: element 0 = you, 1 = seat 1 (left), 2 = seat 2
> (right). The names are set at runtime; editor placeholder text doesn't matter.

### 2.3b MatchManager match-score labels (3 players)
The match-score (VP) labels use the SAME per-team array and order as 2.3.
- [ ] Select the **MatchManager** object → **Match Score Labels** → **Size = 3**.
- [ ] Assign your three VP labels in the SAME order as the score labels:
  - [ ] **Element 0** → your VP label
  - [ ] **Element 1** → left opponent's VP label
  - [ ] **Element 2** → right opponent's VP label

### 2.4 Draft reveal area (used by 1v1v1)
1v1v1 is a reveal-and-draft mode, so the draft phase runs every round (3 leftover
cards). The 2v2 scene you duplicated already has a **Draft Reveal Area** — confirm
it's still assigned:
- [ ] Select **GameManager** → confirm **Draft Reveal Area** points at the center
      reveal `RectTransform`. If empty, drag it in.

### 2.5 Verify carried-over fields
Because you duplicated 2v2, these should still be assigned on the 1v1v1 GameManager.
Confirm none read "None" / "Missing":
- [ ] Sprite Registry, Card Prefab, Human Hand Area, Trick Area View, Score HUD,
      Human Controller, Bid/Trump/Marriage Panels, Card Back Sprite,
      Auto Sort Highlight, Match Manager, Game Logger, Keyboard Input.
- [ ] On **ScoreHUD**, also re-check **Score Labels** has the 3 entries from 2.3.

---

## Part 3 — Main Menu button

- [ ] Open `Assets/Scenes/Main Menu.unity`.
- [ ] Duplicate one of the existing clickable menu entries (e.g. the "2 V 2" one).
- [ ] Set its text to **`1 V 1 V 1`**.
- [ ] In its Button **OnClick ()**: target the **MainMenuController** object,
      function **`MainMenuController → OnOneVsOneVsOneClicked`**.
- [ ] Select the **MainMenuController** object and confirm its Inspector now shows:
  - [ ] One Vs One Scene = `1vsAI`
  - [ ] Two Vs Two Scene = `2v2`
  - [ ] One Vs One Vs One Scene = `1v1v1`

---

## Part 4 — Playtest

- [ ] From the menu, click **1 V 1 V 1**. You should be dealt 7 cards; two opponent
      fans appear left and right; bidding cycles through all three seats.
- [ ] After trump is declared, 3 cards are revealed center-screen for the draft;
      you click one to draft.
- [ ] HUD shows three running scores (You / Opponent A / Opponent B); the round
      result banner shows three rows.
- [ ] Match ends when one player reaches the target AND leads the runner-up by 2.
- [ ] Re-check 1v1 and 2v2 still work after the Part 1 re-wire (scores update,
      result banner reads correctly).

---

## Tuning knobs (code, if layout looks off)
- Opponent fan rotation for 1v1v1: `GameManager.SeatFanRotation` (seat 1 = +90°,
  seat 2 = −90°).
- Seat/opponent names: `GameManager.SeatName`.
- Draft reveal spacing: `GameManager.BuildDraftReveal`.

Tell me what looks off after the playtest and I'll adjust the code.

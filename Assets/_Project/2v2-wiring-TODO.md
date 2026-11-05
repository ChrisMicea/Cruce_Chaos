# 2v2 — Unity Editor Wiring Checklist

All code for 2v2 is done. What remains is editor-only work that can't be scripted
safely from outside Unity: creating the 2v2 scene, laying out the extra hand
areas, wiring serialized fields, and adding the menu buttons.

Do these in order. Each box is a discrete action.

---

## 0. Prerequisite (already done)
- [x] 1v1 scene re-wired: `AIHandArea` dragged into GameManager → **Opponent Hand Areas → Element 0**.

---

## 1. Create the 2v2 scene (safe GUID handling)

Duplicating **inside the editor** is what makes the GUID correct — don't copy the
`.unity` file in a file manager.

- [ ] In the Project window, open `Assets/Scenes/`.
- [ ] Select `1vsAI.unity`. Press **Ctrl+D** (duplicate). A `1vsAI 1.unity` appears.
- [ ] Rename it to **`2v2`** (exact name — the menu loads the scene called `2v2`).
- [ ] Open `2v2.unity` (double-click).
- [ ] Add it to the build list: **File → Build Settings → Add Open Scenes** (or drag it
      into the Scenes In Build list). Without this, `SceneManager.LoadScene("2v2")` fails.

The duplicate already contains everything the 1v1 scene had: GameManager, the HUD,
the panels (Bid / Trump / Marriage), the human hand area, one opponent area
(`AIHandArea`), buttons, etc. We only need to add the 2v2-specific pieces.

---

## 2. Opponent hand areas (seats 1, 2, 3)

In 2v2 there are three non-human seats. Screen layout is **clockwise**:
**seat 1 = LEFT, seat 2 = TOP (your partner), seat 3 = RIGHT.**

The existing `AIHandArea` sits at the top — reuse it as the TOP (seat 2) area and
add two more for left and right.

- [ ] In the Hierarchy, find `AIHandArea` (under the Canvas). This becomes the **TOP /
      seat 2** area. Optionally rename it `OpponentArea_Top` for clarity.
- [ ] Duplicate it twice (Ctrl+D). Rename the copies `OpponentArea_Left` and
      `OpponentArea_Right`.
- [ ] Position the three areas on the table (anchored positions, Canvas-dependent —
      nudge to taste):
  - [ ] **Left** (seat 1): far-left, vertically centered.
  - [ ] **Top** (seat 2): top-center (where `AIHandArea` already is).
  - [ ] **Right** (seat 3): far-right, vertically centered.
- [ ] **Leave each area's Z-rotation at 0.** The code rotates the *cards* per seat
      (left +90°, top 0°, right −90°); the area itself must stay unrotated or the
      tilt will be doubled.

Then wire them (ORDER MATTERS — the list index maps to the seat):

- [ ] Select the GameManager object → Inspector → **Opponent Hand Areas** → **Size = 3**.
- [ ] **Element 0** → `OpponentArea_Left`  (seat 1)
- [ ] **Element 1** → `OpponentArea_Top`   (seat 2, partner)
- [ ] **Element 2** → `OpponentArea_Right` (seat 3)

> If the order is wrong, fans face the wrong way / land in the wrong spot. Element 0
> is always seat 1 (left), Element 1 seat 2 (top), Element 2 seat 3 (right).

---

## 3. Draft reveal area (only used by the "With Surprises" variant)

The draft variant reveals 4 leftover cards center-screen for the player to pick from.
Classic 2v2 and 1v1 never use this, so it's optional — but since the same `2v2`
scene runs both variants, set it up here so "With Surprises" works.

- [ ] Create an empty UI object under the Canvas: right-click Canvas →
      **Create Empty** (or UI → Empty). Name it `DraftRevealArea`.
- [ ] Add a **RectTransform** (UI objects have one by default). Center it on screen,
      roughly where the trick area is but it can overlap briefly — it only shows
      during the draft.
- [ ] Give it enough width for ~4 cards in a row (the code spaces them 140px apart).
- [ ] Select GameManager → Inspector → **Draft Reveal Area** → drag `DraftRevealArea` in.

> Leave this empty in the 1v1 scene — the draft phase never runs there.

---

## 4. Verify the other GameManager fields carried over

Because you duplicated the 1v1 scene, these should already be assigned. Confirm
none show "None" / "Missing" on the 2v2 GameManager:

- [ ] Sprite Registry, Card Prefab
- [ ] Human Hand Area, Trick Area View, Score HUD, Human Controller
- [ ] Bid Panel, Trump Picker Panel, Marriage Panel
- [ ] Card Back Sprite
- [ ] Auto Sort Highlight, Match Manager, Game Logger, Keyboard Input

If any is Missing (sometimes happens after a script changes shape), re-drag the
corresponding Hierarchy object into the slot.

---

## 5. HUD labels for team play (automatic — just verify)

No wiring needed: `GameManager.Start()` calls `SetTeamPlay(true)` on the ScoreHUD
and MatchManager when the mode is 2v2, switching labels to "Your Team" / "Opponents".
1v1 stays "You" / "AI".

- [ ] (After first playtest) Confirm the HUD reads "Your Team" / "Opponents" in 2v2.

---

## 6. Main Menu — add the two 2v2 buttons

The menu already has a "1 V 1" clickable text wired to
`MainMenuController.OnOneVsOneClicked`. Duplicate it twice for the 2v2 modes.

- [ ] Open `Assets/Scenes/Main Menu.unity`.
- [ ] In the Hierarchy, find the "1 V 1" clickable element (the TMP text with the
      Button/onClick that calls `OnOneVsOneClicked`).
- [ ] Duplicate it (Ctrl+D). Set its text to **`2 V 2`** (or "2v2 Classic"). Position
      it below the 1v1 button.
  - [ ] In its Button component's **OnClick ()**: set the target object to the
        **MainMenuController** object, and the function to
        **`MainMenuController → OnTwoVsTwoClassicClicked`**.
- [ ] Duplicate again. Set its text to **`2 V 2 — With Surprises`** (label is up to you;
      "Surprises" / "Draft" both fine). Position it below the classic button.
  - [ ] OnClick → **MainMenuController → `OnTwoVsTwoSurprisesClicked`**.
- [ ] Select the **MainMenuController** object and confirm its Inspector shows:
  - [ ] **One Vs One Scene** = `1vsAI`
  - [ ] **Two Vs Two Scene** = `2v2`

> Both 2v2 buttons load the SAME `2v2` scene; they differ only in the GameMode they
> set (classic vs draft). That's handled in code via `GameSelection`.

---

## 7. Playtest

- [ ] From the Main Menu, click **2 V 2** → a round should deal 6 cards to you and
      show three opponent fans (left / top / right). Bidding should cycle through
      all four seats. HUD says "Your Team" / "Opponents".
- [ ] Play a full round; confirm the trick rotation goes you → left → top → right,
      trick winner leads next, and team scores pool correctly.
- [ ] From the Main Menu, click **With Surprises** → after trump is declared, 4 cards
      appear center-screen; you click one to draft; the round then plays out.
- [ ] Play 1v1 once more to confirm nothing regressed (8 cards, draw pile, "You"/"AI").

---

## Known tuning knobs (code, if needed after seeing it)

- Side-fan rotation (±90°): `GameManager.SeatFanRotation(seat)`.
- Draft reveal spacing (140px): `GameManager.BuildDraftReveal`.
- Opponent fan density/radius: the `_fanRadius` / fan-angle fields on GameManager.

Tell me what looks off after the playtest and I'll adjust the code.

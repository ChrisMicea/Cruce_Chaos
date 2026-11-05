# Game Scene Setup Guide

Follow these steps in the Unity Editor to wire up the `GameScene`.

---

## 1. Create the CardSpriteRegistry asset

1. Right-click in the Project window → **Create → Cruce Chaos → Card Sprite Registry**
2. Name it `CardSpriteRegistry` and save it anywhere (e.g. `Assets/_Project/`)
3. Select it in the Inspector and click **"Auto-Fill from Cards/ Folder"**
   - This scans `Assets/Cards/` and fills all 24 sprite slots automatically
4. Confirm every entry has a sprite assigned (no yellow warnings in the Console)

---

## 2. Create the Card Prefab

1. In the Project window, create a new **UI → Image** as a Prefab (or use the existing `Assets/Card.prefab`)
2. Set the RectTransform size to **120 × 180**
3. Add these components to the root:
   - `Image` (already there — this shows the card face)
   - `Button` (add via Add Component)
   - `CardView` (add via Add Component → search "CardView")
4. Create a child GameObject named **`Highlight`**:
   - Add an `Image` component to it
   - Set color to white with alpha ~0
   - Uncheck **Raycast Target** (it's just a visual overlay)
5. In the `CardView` component, wire:
   - **Face Image** → the root `Image`
   - **Highlight Overlay** → the child `Highlight` Image
6. Save as a Prefab in `Assets/_Project/Prefabs/CardPrefab.prefab`

You also need a **card back sprite** (a plain rectangle or a back design).
If you don't have one, use `Assets/Cards/Square.png` as a placeholder.

---

## 3. Build the Game Scene

1. Open `Assets/Scenes/SampleScene.unity` (rename it to "Game" if you like)
2. Add `File → Build Settings` — add both "Main Menu" and "Game" scenes

### Canvas setup

Create a **Canvas** (right-click Hierarchy → UI → Canvas):
- Render Mode: **Screen Space – Overlay**
- Canvas Scaler: **Scale With Screen Size**, reference 1920 × 1080

Inside the Canvas, create these child GameObjects:

#### a) HumanHandArea
- Empty GameObject, `RectTransform`
- Anchor: **bottom-center**, Pivot: **bottom-center**
- Position: `(0, 20)`, Size: `(1200, 220)`

#### b) AIHandArea
- Empty GameObject, `RectTransform`
- Anchor: **top-center**, Pivot: **top-center**
- Position: `(0, -20)`, Size: `(1000, 170)`

#### c) TrickArea
- Empty GameObject with `TrickAreaView` component
- Anchor: **center**, Position: `(0, 0)`
- Add two child Images named **HumanSlot** and **AISlot** (size ~120×180 each), positioned at `(0, -100)` and `(0, 100)`
- Optionally add a child Image named **TrumpIcon** (size ~60×60) at `(-500, 200)` for the trump suit icon
- Optionally add a child Image named **WinnerFlash** covering the whole trick area for the flash effect
- Wire all references in the `TrickAreaView` Inspector fields

#### d) HUD
- Empty GameObject with `ScoreHUD` component
- Add `TextMeshProUGUI` children:
  - **HumanPointsLabel** — anchor bottom-left, position `(160, 80)`, text "Your Points: 0"
  - **AIPointsLabel** — anchor top-right, position `(-160, -80)`, text "AI Points: 0"
  - **DeckLabel** — anchor bottom-right, position `(-160, 80)`, text "Deck: 8"
  - **TrumpLabel** — anchor top-left, position `(160, -80)`, text "Trump: —"
- Wire them in the `ScoreHUD` Inspector fields
- Add a child **ResultBanner** panel (dark semi-transparent Image + `TextMeshProUGUI` label)
  - Set it **inactive** by default (`SetActive(false)` is done in code)
  - Add two Buttons: **"Play Again"** and **"Back to Menu"**

---

## 4. Create the GameManager object

1. Create an empty GameObject named `GameManager`
2. Add components:
   - `GameManager`
   - `HumanPlayerController`
   - `MatchManager` (optional — enables cross-round match scoring)

3. Wire `GameManager` Inspector fields:
   | Field | Value |
   |-------|-------|
   | Sprite Registry | `CardSpriteRegistry` asset |
   | Card Prefab | `CardPrefab.prefab` |
   | Human Hand Area | `HumanHandArea` RectTransform |
   | AI Hand Area | `AIHandArea` RectTransform |
   | Trick Area View | `TrickArea` GameObject |
   | Score HUD | `HUD` GameObject |
   | Human Controller | `GameManager` object's `HumanPlayerController` |
   | Card Back Sprite | `Square.png` or your card-back sprite |
   | Match Manager | `GameManager` object's `MatchManager` (optional) |
   | Main Menu Scene | `"Main Menu"` |

4. Wire **Result Banner buttons**:
   - "Play Again" `OnClick` → `GameManager.OnPlayAgainClicked`
   - "Back to Menu" `OnClick` → `GameManager.OnMainMenuClicked`

---

## 5. Suit icons for the trump indicator (optional but nice)

If you want to show suit icons on the trump indicator and HUD, create 4 small sprites (one per suit) and assign them to:
- `TrickAreaView._suitIcons` (array of 4, order: Green, Red, Acorn, Bell)
- `ScoreHUD._suitIcons` (same order)

---

## 6. Link "Play" from the Main Menu

In your existing Main Menu scene, wire the "Play" button's `OnClick` to:
```
SceneManager.LoadScene("Game")
```
Or use a small `MenuController` script:
```csharp
public void OnPlayClicked()
{
    UnityEngine.SceneManagement.SceneManager.LoadScene("Game");
}
```

---

## Quick compile check

After saving everything, check the Unity Console for errors.
Expected: zero errors. The `CruceChaos.Core` assembly has `noEngineReferences: true`
(pure C#), and `CruceChaos.Unity` references it plus `Unity.TextMeshPro`.

If you see *"Assembly 'CruceChaos.Core' will not be compiled"*, make sure the
`.asmdef` files were imported (they appear as Assembly Definition assets in the
Project window).

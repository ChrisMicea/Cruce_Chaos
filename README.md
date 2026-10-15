Cruce_Chaos

Repository for the project for the FCPL subject, consisting in a Unity card game, with special power-ups and mechanics.

idei: skin-uri pentru masa si imprejurimi/environment: sat romanaesc, masa ca pe Balatro, masa de cazino etc. etc. multiplayer capability - facem noi self host? - LAN si/sau comunicare directa prin Bluetooth cu device-ul celuilalt jucator

power-up-uri: multe idei - schimbi tronful, 1 data pe tura poti schimba 2 carti din mana ta cu a adversarului ("Uite acolo si cand se uita ii schimbi cartile") mini-game: incerci sa faci semne, daca te prinde adversarul - debuff, comeback mechanic: vezi cartile adversarului? reflexia prin sticla

facem sprite-urile un obiect, la start of game se genereaza obiactul, nu e json sau altceva hardcodat - research: ca pe Slay the Spire nu sunt hardcodate, doar dai descrierea, numele etc si se genreaza grafica? - Slay the Spire 2 e open source, ne uitam cum genereaza ei sprite-urile

health bar la adversar? singleplayer VS AI, 1 v 1, 2 v 2, 1 v 1 v 1 (?)

#Milestones

1. Have the basic game logic functional, debug mode, maybe a power-up

---

## Development log

### Core game logic (first milestone, in progress)

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

**Simplified for now (to be added later):** no trump, no follow-suit
enforcement (all cards are legal while implementing the base loop), no bidding
or score conversion. The round currently just reports captured card points out
of 120.

### Standalone console sandbox

`Sandbox/` is a plain .NET console project **outside `Assets/`** so Unity never
compiles it. Its `.csproj` *links* the Core `.cs` files via a `<Compile Include>`
glob, so there is a single source of truth shared with the Unity project (no
copies, no drift). It contains a placeholder `FirstLegalMoveController` (always
plays its first legal card) and a `Program.cs` that runs one full round with a
fixed RNG seed and checks the captured points sum to 120.

#### Running the sandbox

Requires the .NET SDK (the project targets `net10.0`). From the repo root:

```bash
# Run one round with the default seed (42):
dotnet run --project Sandbox

# Run with a specific seed (e.g. 7) to try a different deal:
dotnet run --project Sandbox -- 7
```

It prints the deal, the trick-by-trick play, and the final captured-point
tallies.

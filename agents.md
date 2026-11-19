This is a Unity card game inspired by the romanian "Cruce" game.

DON'T try to manually code / implement things that should be done manually in the Unity editor. Instead, you should point out exactly what needs to be done manually.

The game's core logic and underlying implementations are found in Assets/_Project/Scripts/Core/.
The Unity visual and interaction layer built on top of these is found in Assets/_Project/Scripts/Unity/.
DON'T reimplement things for Unity that have already been implemented in Core/. Instead, always try to reuse the logic and implementations like an API.

Pass-and-play (hotseat) seat model:
GameManager is seat-aware. Which seats are human is a set (bool[] _humanSeats, indexed by seat), not a hardcoded seat 0. One human seat is "active" at a time (int _activeHumanSeat) — that seat's hand renders face-up; every other seat shows card backs. The single HumanPlayerController serves each human seat serially (only one human is mid-decision in the round coroutine). Between two different human seats taking control, GameManager runs a pass-the-device gate (PassDevicePanel) that hides all hands, prompts "Have you passed the device?", and only switches the face-up hand after the player confirms. The gate never fires on AI handovers or when the same human seat acts twice in a row.
GameSelection.HumanSeats (bool[]) is the contract a future seat-selection screen fills to choose exactly which seats are human for 2v2 / 1v1v1 hotseat. GameSelection.PassAndPlay is the 1v1 convenience flag that marks all seats human without a selection screen. Both are read once by GameManager in Start() and then reset, so a direct editor scene entry (no menu) stays the original single-human-at-seat-0 game.
Game scenes are REUSED for singleplayer vs pass-and-play — the same 1v1/2v2/etc. scene runs both; only menu/selection scenes are added for new choices. Never duplicate a game scene per player-arrangement.
This is a Unity card game inspired by the romanian "Cruce" game.

DON'T try to manually code / implement things that should be done manually in the Unity editor. Instead, you should point out exactly what needs to be done manually.

The game's core logic and underlying implementations are found in Assets/_Project/Scripts/Core/.
The Unity visual and interaction layer built on top of these is found in Assets/_Project/Scripts/Unity/.
DON'T reimplement things for Unity that have already been implemented in Core/. Instead, always try to reuse the logic and implementations like an API.
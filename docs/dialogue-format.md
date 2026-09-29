# Dialogue scripts

Put `.txt` files in `BepInEx/config/MiSideMod/dialogues/` (three samples are created on first run).
Play them from the sandbox panel (F2, **Mod chats** tab) or press **F3** for a random one.
Enter or Space advances, Escape cancels.

```
# a comment (also: // comment)
Mita: Hello, [player]!          <- "Name: text" shows a speaker name; [player] = config PlayerName
Just narration with no speaker. <- no "Name:" prefix
@anim Jump                      <- play the Chibi Mita animation whose name contains "Jump"
@coins 50                       <- give 50 coins
```

Animation names in the Tamagotchi scene include: Hello, Ready, Hungry, WaitFood, Idle, Jump, GetMonitor,
TryScarf, TryStop, GetGift. If nothing matches, a warning is written to `LogOutput.log`.
No voice acting yet; that is planned.

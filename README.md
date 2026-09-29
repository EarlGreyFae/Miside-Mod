# MiSide Minigame Expansion

A mod to expand the Mita minigame in MiSide (the one before she pulls you into her world):
new activities, more dialogue/reactions, and extended gameplay.

## Status
Scaffold only. Step one is mapping the minigame's scene objects and scripts.

## Setup (Windows)
1. Install the [.NET SDK](https://dotnet.microsoft.com/download) (6.0 or newer).
2. Install **BepInEx 6 (IL2CPP, x64)** into the MiSide folder from the
   [BepInEx bleeding-edge builds](https://builds.bepinex.dev/projects/bepinex_be).
   Check the game folder first: `GameAssembly.dll` means IL2CPP; a `Managed` folder means Mono
   (then this project needs to be changed to the Mono flavour).
3. Launch the game once so BepInEx generates `BepInEx/interop/`.
4. Copy `GameDir.props.example` to `GameDir.props` and set your game path.
5. `dotnet build src` builds and copies the DLL into `BepInEx/plugins`.

## Exploring the minigame
In game, press **F8** (configurable) to dump the active scene hierarchy and components to
`BepInEx/LogOutput.log`. Use that, plus a decompiler on `BepInEx/interop/Assembly-CSharp.dll`
(e.g. dnSpy/ILSpy), to find the minigame's classes, then patch them with Harmony.

**F9** writes the members of all `Tamagotchi*` / `*Chibi*` classes to `BepInEx/MiSideMod_types.txt`.

**F12** logs loaded scenes, `Scene_Load` components (what triggers the jump to `Scene 2 - InGame`), the active quest, money/energy, and every shop item with its price and what its purchase triggers (to find the TV purchase that ends the minigame). (`Location14_Days` does not exist in this scene.)

**F10** gives +100 coins and **F11** re-rolls energy/hunger/mood via `NewDay` (not a story day; the day counter lives elsewhere). Game events (new day,
minigame start/stop, dialogue start) are logged to `LogOutput.log`.

**F2** opens the sandbox panel: economy cheats, unlock the whole shop, launch any minigame, play any
Chibi Mita animation, replay any dialogue, or activate any quest. All of it reuses content the game
already has. **F5** switches on the final quest (Day 37) so the ending can be tested in seconds.

**F6** toggles `BlockRealmTransition` (also a config option, off by default): it switches off the saved
event on the final quest (Day 37 "Wait", `Quest 10`) that calls `World.GoScene` and pulls you into
`Scene 2 - InGame`. Experimental: what the game does afterwards has not been tested.

## What the scene dump showed
The minigame is the Tamagotchi-style game with Chibi Mita (scene `Scene 1 - RealRoom`): day-based
quests/cutscenes, and four minigames (`Cartridge`, `Chip`, `Sorting`, `Cooking`) driven by
`Tamagotchi_Main`, `Tamagotchi_MiniGame`, `Tamagotchi_Dialogue`, `Mob_ChibiMita`.

## Roadmap
- [ ] Locate minigame scene and controller classes
- [ ] Hook Mita's reaction/dialogue system for new lines
- [ ] Add new activities
- [ ] Extend or remove the session limit / trigger for the transition

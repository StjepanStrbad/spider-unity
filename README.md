# Spideroid

3D Unity platformer where you control a spider that sticks to surfaces.
Navigate levels full of moving obstacles, checkpoints, and hazards (spinning
saws, moving pipes) to reach the goal. Originally built as a Croatian
university *završni rad* (final thesis) project, now maintained on git after
migrating from Unity Version Control.

## Gameplay

- **Wall-stick movement** — the spider clings to surfaces and can traverse walls and ceilings (`GroundStick.cs`).
- **Simple-arc jumping** between surfaces (`SimpleArcJump.cs`).
- **Hazards** — animated saws (`SawAnimToggle.cs`), moving obstacles (`ObstacleMover.cs`), circularly-moving pipes (`PipeCircularMover.cs`). Touching them triggers `PlayerDeath.cs`.
- **Checkpoint system** — checkpoints record progress within a level (`Checkpoint.cs`, `CheckpointManager.cs`, `CheckpointVisual.cs`); deaths respawn at the most recent one.
- **Level progression** — Main Menu → Level One → Level Two, with a completion sequence on the final level (`PlayEnding.cs`, `LevelCompleteUI.cs`).
- **Stats tracking + save/load** — per-level stats persisted across sessions (`LevelStatsTracker.cs`, `SaveSystem.cs`, `GameSaveData.cs`).

## Scenes

| Scene | Role |
|---|---|
| `MainMenu.unity` | Title screen, level select, settings (driven by UI Toolkit — `MainMenuUIToolkit.cs`, `MainMenuSpiderBridge.cs`) |
| `LevelOne.unity` | First playable level |
| `LevelTwo.unity` | Second level with ending sequence |
| `SampleScene.unity` | Unity template default (can be deleted if unused) |

## Custom scripts

```
Assets/MyScripts/
  SpiderCamera.cs          Third-person camera that follows the spider
  GroundStick.cs           Surface-clinging movement
  SimpleArcJump.cs         Jump with arc trajectory
  PlayerDeath.cs           Death + respawn at last checkpoint
  ObstacleMover.cs         Generic moving obstacle driver
  SawAnimToggle.cs         Saw rotation on/off control
  PipeCircularMover.cs     Circular path movement for pipe hazards
  PipeAmbianceSound.cs     Pipe ambient audio loops
  OrbitAroundTarget.cs     Orbit-style transform behaviour
  DiscoLight.cs            Color-cycling light effect
  CombineStaticMeshes.cs   Editor utility for combining static geometry
  PlayEnding.cs            End-of-game cutscene trigger

  save_checkpoint/
    Checkpoint.cs              Individual checkpoint trigger
    CheckpointManager.cs       Tracks active checkpoint per scene
    CheckpointVisual.cs        Activation visuals
    GameSaveData.cs            Serializable save payload
    SaveSystem.cs              Disk I/O for save data
    LevelStatsTracker.cs       Per-level stats (deaths, time, etc.)
    LevelCompleteUI.cs         Completion screen
    LoadingScreen.cs           Inter-scene loading screen
    MainMenuUIToolkit.cs       UI Toolkit main menu logic
    MainMenuSpiderBridge.cs    Bridges main menu UI to spider scene
```

## Tech / packages

- Unity (URP — Universal Render Pipeline)
- Cinemachine — cameras
- ProBuilder — in-editor level geometry
- AI Navigation — pathfinding (for any AI-driven obstacles)
- Animation Rigging — procedural rig adjustments (spider legs)
- Input System (new) — input handling
- UI Toolkit — main menu and HUD
- TextMesh Pro — text rendering (Orbitron font)
- Mirza Beig asset pack — visual effects (under `Assets/Mirza Beig/`)

## Asset folders

```
Assets/
  Audio/             Congratulations.mp3, SawAudio.flac
  Fonts/             Orbitron variable font + SDF asset
  LevelModel/        FBX level geometry + per-material variants
  Materials/         Player, checkpoint, saw, danger, hitbox materials
  Mirza Beig/        Third-party VFX asset pack
  Model/             Misc 3D models
  MyScripts/         All gameplay code (see above)
  Resources/         Runtime-loaded resources
  Saws/              Saw animation controller + animations
  Scenes/            Game scenes
  Settings/          URP render pipeline assets
  Textures/          Standalone textures
  UI Toolkit/        Main menu UXML / USS
```

## Version control note

This repo started life on **Unity Version Control (Plastic SCM)**. The migration
to git brought across the working copy as the initial commit. The original
`ignore.conf` (Plastic's ignore rules) remains in the tree for reference and
its rules have been ported into `.gitignore`. The `.plastic/` workspace
metadata is left on disk but untracked — it can be deleted once you're certain
git is the keeper.

## Building / running

Open the folder in Unity (matching the editor version recorded in
`ProjectSettings/ProjectVersion.txt`). Open `Assets/Scenes/MainMenu.unity` and
press Play.

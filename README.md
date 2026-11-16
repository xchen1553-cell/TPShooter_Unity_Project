# TPShooter — Third-Person Shooter Prototype

A small third-person shooter prototype built for a coursework project in Unity 6 (6000.6.3f1), using the Universal Render Pipeline (URP 17.6.0).

## What this prototype includes

### Core gameplay
- Third-person player controller with a follow camera
- WASD movement, mouse look, mouse-button shooting, jump
- Enemy AI that patrols, chases the player and attacks within range
- Solid enemy collision: enemies no longer walk through walls or each other
- Player model auto-rotates toward the movement direction
- Object pool for bullets; bullets only damage targets in the direction of travel
- Two playable levels; walk through the portal to proceed from Level 1 to Level 2

### Presentation and feedback
- Realtime health HUD (bar plus numeric HP), score and lives
- Emissive player and enemy materials with bloom glow
- Yellow muzzle flash on every shot
- Glowing, pulsing portal that marks the level objective
- Win and Game Over result screen with final score, Play Again and Quit buttons
- Arena dressing: warm directional light with soft shadows, linear fog, procedural ground texture, pillars, crate stacks and boulders

### Weather system (thesis topic)
Four states with 1.5 s smooth transitions, particle VFX and gameplay rules:

| Weather | Visual | Gameplay rule |
| --- | --- | --- |
| Clear | warm sun, long view | baseline |
| Winter | cold blue light, snowfall, white snowy ground | player speed times 0.65 |
| Rain | grey light, rain streaks, wet dark ground | grip 0.30, the player slides instead of stopping |
| Fog | dense fog from 4 to 22 m, floating ground mist | enemy vision times 0.45, you can approach unseen |

Weather cycles automatically every 30 s (Winter, Rain, Fog) and can be overridden at any time with the keys below.

## Controls

| Input | Action |
| --- | --- |
| W / A / S / D | Move |
| Mouse | Look around |
| Left mouse button | Shoot |
| Space | Jump |
| 1 / 2 / 3 / 4 | Switch weather: Clear / Winter / Rain / Fog |

## Project structure

- `Assets/Scenes` — Level1, Level2, GameOverScene, WinScene
- `Assets/Scripts/Characters` — Player, Enemy, shared Character base
- `Assets/Scripts/Components` — Bullet, CameraFollow, shooting, muzzle flash, portal, destructible box, damage flash
- `Assets/Scripts/Managers` — GameManager, UIManager, LevelManager, BulletPool, ResultScreen
- `Assets/Scripts/Environment` — ArenaEnvironment (scene dressing), WeatherManager (weather states, VFX and rules)
- `Assets/Editor/BuildScript.cs` — command line and menu build
- `Assets/Prefabs` — Player, Enemy, Bullet, DestructibleBox
- `Assets/Models` — character and prop FBX models
- `Assets/Materials`, `Assets/Settings` — emissive materials and post-processing volume
- `Packages`, `ProjectSettings` — Unity package manifest and project settings

## Build

A prebuilt Windows 64-bit standalone build is attached to the latest [Release](https://github.com/xchen1553-cell/TPShooter_Unity_Project/releases/tag/v1.0).
Download `TPShooter_Build.zip`, extract it, and run `TPShooter.exe`.

The source archive `TPShooter_SourceCode.zip` contains `Assets`, `Packages` and `ProjectSettings`; open the folder with Unity 6 (6000.6.3f1) to rebuild.

Command line build (Unity must be closed first):

```
"E:\unity\6000.6.3f1\Editor\Unity.exe" -quit -batchmode ^
  -projectPath "<project path>" -executeMethod BuildScript.BuildWindows ^
  -logFile "<project path>\build_windows.log"
```

## Notes

- This is a course prototype focused on a runnable baseline; it does not include sound effects, full animation state machines, menus, reloading, or navigation mesh pathfinding.
- Scene dressing and the weather system are applied at runtime, so the scene files stay minimal and every change is script-driven.

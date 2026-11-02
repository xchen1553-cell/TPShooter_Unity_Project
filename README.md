# TPShooter — Third-Person Shooter Prototype

A small third-person shooter prototype built for a coursework project in Unity 6 (6000.6.3f1), using the Universal Render Pipeline (URP 17.6.0).

## What this prototype includes

- Third-person player controller with a follow camera
- WASD movement, mouse look, mouse-button shooting, jump
- Enemy AI that patrols, chases the player and attacks within range
- Object pool for bullets
- Two playable levels; walk through the portal to proceed from Level 1 to Level 2
- Game over and win scenes

## Controls

| Input | Action |
| --- | --- |
| W / A / S / D | Move |
| Mouse | Look around |
| Left mouse button | Shoot |
| Space | Jump |

## Project structure

- `Assets/Scenes` — Level1, Level2, GameOverScene, WinScene
- `Assets/Scripts/Characters` — Player, Enemy, shared Character base
- `Assets/Scripts/Components` — Bullet, CameraFollow, shooting, portal, destructible box, damage flash
- `Assets/Scripts/Managers` — GameManager, UIManager, LevelManager, bullet pool
- `Assets/Prefabs` — Player, Enemy, Bullet, DestructibleBox
- `Assets/Models` — character and prop FBX models
- `Packages`, `ProjectSettings` — Unity package manifest and project settings

## Build

A prebuilt Windows 64-bit standalone build is attached to the latest [Release](https://github.com/xchen1553-cell/TPShooter_Unity_Project/releases/tag/v1.0).
Download `TPShooter_Build.zip`, extract it, and run `TPShooter.exe`.

## Notes

- This is a course prototype focused on a runnable baseline; it does not include sound effects, full animation state machines, menus, reloading, or navigation mesh pathfinding.
- Open the project in Unity 6 (6000.6.3f1) and start from `Assets/Scenes/Level1.unity`.

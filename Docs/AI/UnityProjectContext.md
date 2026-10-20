# Dancing Cat project context

Inspected 2026-10-20 at baseline commit `55d597b`. Unity root: `The Cat/`.

- Unity 6000.5.4f1, URP 17.5.0, Input System 1.19.0 (both input backends enabled), Cinemachine 2.10.7. Zenject and DOTween are imported source/plugins. No active networking usage found.
- Game scripts use the default Assembly-CSharp, global namespaces, serialized private fields and Zenject scene installers. Vendor assemblies have their own asmdefs.
- Only `Assets/Scenes/Level_10/Level_10.unity` is enabled in Build Settings. Levels 01–10 contain authored tile layouts; matching Generator scenes use an amplitude threshold on a fixed BPM grid. Spectrum scene is a visualizer, not the chart generator.
- `LevelSecuenceController.StartGame` starts background audio and player movement. Player advances continuously in Z; dragging changes X. Short tiles launch jumps; long tiles support travel; moving platforms are attached to a player follow point while ridden.
- No Unity MCP/Pipeline package found. Unity CLI and the matching local Editor are available. Use batch Editor validation without installing a bridge or changing dependencies.
- Unity Test Framework 1.7.0 is installed. Existing tests belong to Zenject; there was no first-party movement regression suite at baseline. Real-scene validation is required for gameplay claims.
- `cat.unitypackage` contains only Lowpoly Humanoid Cat visuals already in the project. It does not contain Dancing Cats or movement source. Five materials differ because the project converted them to URP; all GUIDs are preserved.
- Work directly in this existing checkout, current branch `feature/dancing-cat-offline` (renamed from the initial diagnostic branch). The user replaced the repair request with a new implementation under `Assets/DancingCat`; old gameplay source remains unchanged. Preserve legacy scenes, prefab GUIDs, music, saves and imported assets. Validation fixtures must back up and restore preferences, including absent keys.
- The reference is Dancing Cats (`com.rhythmdance.taptile.dancingcats`). Its internal chart algorithm is unknown until supported by artifact/runtime evidence. Public descriptions do not prove automatic generation.

Relevant code: `Assets/Scripts/Player/MovementController.cs`, `Tile/*`, `General/Emerging.cs`, `General/LevelSecuenceController.cs`, `Sound/BackgroundSoundPlayer.cs`, `RhythmGenerator.cs`.

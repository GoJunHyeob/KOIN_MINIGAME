# Unity project context

Analyzed 2026-10-30. Project: `C:/Users/Go/Desktop/GameJam2027` (no Git directory found).

Confirmed from ProjectVersion.txt, manifest.json, ProjectSettings.asset and Assets:

- Unity 6000.3.25f1 (e1dba0a9aba4), URP 17.3.0 with 2D renderer.
- Input System 1.20.0, activeInputHandler=1; template InputSystem_Actions asset.
- Unity Test Framework 1.6.0 installed; no first-party tests or scripts at baseline.
- Baseline Assets contains Scenes/SampleScene (Camera and Global Light 2D), Settings and URP template assets only.
- Build Settings enables SampleScene. No existing game architecture, namespaces, asmdefs or first-party coding conventions.
- No BCSD art, prefabs, networking usage, CI or project-specific instructions found.
- No available Unity MCP tools or installed MCP package detected. Unity Editor executable exists at C:/Program Files/Unity/Hub/Editor/6000.3.25f1/Editor/Unity.exe.
- Baseline player defaults are landscape. Gravity Relay uses a portrait camera viewport; global player settings remain intact. Device orientation/build targets are unverified.

Implementation uses GravityRelay namespace, private serialized fields, Allman braces and explicit component references. RelayPlayer owns movement/state/score; RelayPresentation owns template input clone and HUD; RelaySceneBuilder creates the isolated prototype scene and performs deterministic movement validation. Runtime code uses the existing default Assembly-CSharp; Editor code is under Editor/.

Existing SampleScene, input asset, packages and settings are preserved. GravityRelay scene must be opened explicitly (and added to build scenes if exporting a player). No procedural endless progression is included in this fixed three-planet prototype.

Evidence: ProjectSettings/ProjectVersion.txt, ProjectSettings/ProjectSettings.asset, ProjectSettings/EditorBuildSettings.asset, Packages/manifest.json, Assets/Scenes/SampleScene.unity, Assets/Settings/InputSystem_Actions.inputactions, Assets/Settings/Renderer2D.asset.

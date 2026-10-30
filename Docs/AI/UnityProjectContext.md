# Unity project context

Analyzed 2026-10-30. Project: `C:/Users/Go/Desktop/GameJam2027`. Git main tracks origin/main at https://github.com/GoJunHyeob/KOIN_MINIGAME.git.

Confirmed from ProjectVersion.txt, manifest.json, ProjectSettings.asset and Assets:

- Unity 6000.3.25f1 (e1dba0a9aba4), URP 17.3.0 with 2D renderer.
- Input System 1.20.0, activeInputHandler=1; template InputSystem_Actions asset.
- Unity Test Framework 1.6.0 installed; no first-party tests or scripts at baseline.
- Baseline Assets contains Scenes/SampleScene (Camera and Global Light 2D), Settings and URP template assets only.
- Build Settings enables SampleScene. No existing game architecture, namespaces, asmdefs or first-party coding conventions.
- No BCSD art, prefabs, networking usage, CI or project-specific instructions found.
- No available Unity MCP tools or installed MCP package detected. Unity Editor executable exists at C:/Program Files/Unity/Hub/Editor/6000.3.25f1/Editor/Unity.exe.
- Baseline player defaults are landscape. Gravity Relay uses a portrait camera viewport; global player settings remain intact. Device orientation/build targets are unverified.

Implementation uses GravityRelay namespace, private serialized fields, Allman braces and explicit component references. RelayPlayer owns movement/state/score; RelayPresentation owns template input clone and HUD; RelaySceneBuilder creates the isolated prototype scene with no automated test harness. Runtime code uses the existing default Assembly-CSharp; Editor code is under Editor/.

RelayPlanet uses ExecuteAlways to synchronize explicitly referenced surface/orbit renderers with Radius and RelayPlayer.OrbitRadius. RelayPresentation draws tangent arrows, the actual capture boundary, launch stretch, landing pop/pulse and failure flashes. RelayPlayerEditor provides balance presets and explicit scene-reference migration. Punchy baseline: speed=15 (user slider range 10..20 retained), gravity=60, max acceleration=40, orbit speed=150, capture margin=0.30, capture duration=0.06. See Docs/GravityRelayBalance.md for previous values and endless-generation handoff. Workflow: feature branch, git status, feat: commit, push, create/update a main-target PR; do not merge automatically.

Existing SampleScene, input asset, packages and settings are preserved. GravityRelay scene must be opened explicitly (and added to build scenes if exporting a player). RelayEndlessCourse uses a 12-planet pool, upward seeded generation, shared-runtime reachability checks, frozen flight bounds and camera reframing only while orbiting. RelayPlayer owns score/state and resets the course; RelayPresentation uses monotonic target numbers and a camera-ready launch indicator. Legacy fixed-route mode remains available when the course reference is absent. See Docs/GravityRelayEndless.md. Automated validation scripts and test menus have been removed at the user request.

Placement difficulty keeps the first 3 moves easy and reaches capped geometry at score 15. Distance and lateral-step ranges are sampled only at new-target generation; player physics are never changed. RelayEndlessCourseEditor previews stage/current ranges. Current scene preserves the user's latest orbit speed=150. See Docs/GravityRelayDifficulty.md for exact limits.

Queue 4 real upcoming planets and refill only at the far end. Queued positions stay fixed across landings, and their difficulty is sampled at the future departure score. Preview reachability requires wider launch windows in both orbit directions and is rechecked at actual arrival. See Docs/GravityRelayHorizon.md.

Evidence: ProjectSettings/ProjectVersion.txt, ProjectSettings/ProjectSettings.asset, ProjectSettings/EditorBuildSettings.asset, Packages/manifest.json, Assets/Scenes/SampleScene.unity, Assets/Settings/InputSystem_Actions.inputactions, Assets/Settings/Renderer2D.asset.

2026-10-31 layout: planet radius=0.5, clearance=1.0 (orbit radius=1.5), vertical gaps 4.2..5.4, absolute x limit=2.2, minimum lateral step=1.2. No tests/Play/build validation were executed for this change.

User workflow: skip automated tests, Play checks and build/compile validation after edits unless explicitly requested. Test harnesses, test menus and test-only diagnostic getters were removed. Continue git status, feat: commit, push and PR updates.

2026-10-31 timing stages: next successful move 1/2-4/5-8/9-15/16-25/26+ requires a contiguous launch window of 0.40/0.30/0.20/0.15/0.11/0.11 seconds. Runtime generation samples a circular interval in both orbit directions and stores fixed per-target capture assistance. Endless patterns combine within existing caps; physics speed/gravity/orbit speed remain unchanged. No test harness, Play or compilation validation was added/executed. Static meteors remain optional future content.

2026-10-31 run flow: RelayPresentation owns Start/Playing/Results IMGUI screens and PlayerPrefs key GravityRelay.BestScore.v1. RelayPlayer gameplayActive gates simulation and launch; Start shows a frozen prebuilt course. UI transitions require full input release and a fresh later press. Best scores update in memory on new records and flush at results/restart/title/pause/focus loss/disable/quit. Restart reuses the existing pool without scene reload or an added delay. Existing serialized scene references are sufficient. See Docs/GravityRelayFlow.md. No tests/Play/compile validation executed.


2026-10-31 연속 이동 개정: 최신 공전·생성·난이도 기준은 Docs/GravityRelayDifficulty.md를 따른다. 공전 반경 1.8(clearance 1.3), 높이 차 초반 5.6~5.9/후반 6.1~6.6, 좌우 흐름 유지 확률 0.85/최대 방향 변화 60°, 성공 구간 0.42/0.36/0.30/0.26/0.24/0.24초. 착륙 시 빠른 공전 방향 선택, 발사 입력 버퍼 0.35초, 카메라 sharpness 28/lookahead 3.6. 발사·중력·공전 속도는 유지. 테스트·Play·컴파일 검사를 실행하지 않음.

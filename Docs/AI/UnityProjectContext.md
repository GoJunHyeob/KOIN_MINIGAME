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


2026-10-31 생성 실패 보정: 넓어진 간격에서 단계별 성공 구간·양방향 도달·포획 상한을 동시에 만족하지 못하면 생성이 실패할 수 있다. 기존 코드는 앞쪽 4개 중 먼 행성 하나만 실패해도 ResetCourse/Advance를 실패시켜 NO REACHABLE ROUTE → RUN OVER로 전환했다. 생성 후보가 없으면 결정적 좌우·높이 격자를 탐색하고 목표 성공 시간을 100%/75%/50%/Fallback Launch Window(기본 0.20초)까지 단계적으로 완화한다. 목표 행성에는 실제 채택한 시간을 저장한다. 표의 단계별 시간은 우선 목표이고, 대체 배치는 이 하한을 사용한다. 도달 가능성·포획 반경 비겹침·화면 경계·배치 상한은 우회하지 않는다. 당장 이동할 목표가 남아 있으면 먼 미리보기 생성 실패로 죽이지 않고 다음 착륙 때 다시 보충한다. 테스트·Play·컴파일 검사는 실행하지 않았으며 실제 재현 확인은 하지 않았다.


2026-10-31 플레이어 표시: BCSD Placeholder의 저장된 로컬 Scale을 0.42에서 0.63으로 확대(가로·세로 1.5배). BCSD Player 루트가 아닌 자식 표시 오브젝트 크기를 변경하며, 기존 발사 늘어남·착륙 튕김은 이 크기를 기준으로 동작한다. 추가 크기 조정은 해당 자식 Transform > Scale에서 한다. RelayPlanet 공전 가이드는 Play 중 Orbiting 상태의 CurrentPlanet만 표시한다. 미도착·비행·착륙 보정 중·사망 시에는 숨기고, 착륙 완료 후 표시한다. 편집 중에는 배치용 가이드를 유지한다. 목표 포획 범위인 금색 링은 기존 표시 정책을 유지한다. 테스트·Play·컴파일 검사는 실행하지 않았다.


2026-10-31 크기 추가 조정: 행성 반경 0.5 → 0.575(15% 확대), BCSD 표시 Scale 0.63 → 0.725(약 15% 확대). 공전 여유 1.3을 유지하므로 실제 공전 반경은 1.875이며 가이드·포획 반경에도 동일하게 반영된다. 씬의 세 행성 템플릿과 생성 기본값을 함께 변경했다. 실제 공전 중에만 가이드를 보이는 정책은 유지한다. 테스트·Play·컴파일 검사는 실행하지 않았다.


2026-10-31 art/UI: black-purple BCSD placeholder, lavender planets, purple orbit/capture rings and deep purple navy background. Bottom-only minimal score HUD, safe-area fitting, thumb-reachable menu buttons, full-screen gameplay touch, existing UI/Cancel-driven pause/resume/back navigation. Bundled official Pretendard v1.3.9 Regular/SemiBold OTF fonts + SIL OFL, direct serialized references. See Docs/GravityRelayArtUI.md. No tests, Play, compilation or visual validation executed.


2026-10-31 크기 재조정: 행성 반경 0.575 → 0.66, BCSD 표시 Scale 0.725 → 0.835(각각 약 15% 확대). 공전 가이드와 실제 경로를 그대로 유지하기 위해 Orbit Clearance를 1.3 → 1.215로 보정했다. 합산 공전 반경은 1.875(가이드 지름 3.75)로 유지되며 포획 반경·가이드 표시 조건도 유지한다. 씬 세 행성 템플릿과 생성 기본값을 함께 적용했다. 테스트·Play·컴파일 검사는 실행하지 않았다.


2026-10-31 space backdrop: generated purple/navy nebula PNG in Art/Resources/GravityRelaySpace.png. Presentation loads the local Sprite once, RelaySpaceBackground reuses three reflected tiles behind gameplay with 0.12 camera parallax and brightness 0.9; existing failure flash is forwarded. No scene edits; user scene changes stay uncommitted. No tests/Play/compile/visual validation executed.

2026-10-31 background binding: saved Space Background root and three sprite tiles in GravityRelay scene, explicit artwork/camera/Presentation references, ExecuteAlways preview of existing tiles and runtime reuse. Earlier implementation created the backdrop only at runtime. Existing user scene tuning is preserved and excluded from this commit. No Play/tests/compilation/editor visual validation executed.


2026-10-31 combo/feedback: RelayPlayer owns Combo/PeakCombo and Launched/Landed/Died events. RelayFeedback subscribes for bounded pooled particles, two soft ring glows, placeholder face expressions and three original synthesized placeholder WAV clips. Presentation exposes replacement Sprite/AudioClip slots, volume and visual cap. Bottom opaque high-contrast score/combo HUD and pointer-only UI with a pause button; pause hit rect excluded from launch, IMGUI keyboard activation suppressed. Native Android Back only in device builds. Original BCSD art/audio unavailable. No tests/Play/compile/audiovisual validation executed. See Docs/GravityRelayComboFeedback.md.


2026-10-31 BCSD original: user-supplied transparent 512px PNG copied unchanged to Art/BCSDCharacter.png, directly wired in scene SpriteRenderer and Presentation. PPU320, child scale1.15, white tint; placeholder eyes disabled. Dedicated Feedback Particle Sprite retains Disc; dynamic tangent arrow offset respects sprite bounds. User provided one pose only, so expression slots remain optional and no procedural face overlays are drawn on the logo. User scene tuning preserved. No tests/Play/compile/editor visual validation executed.


2026-10-31 revised BCSD sprite: image_gen edit of user reference, charcoal round body, purple ring and white face; no hands/arms/gray feet/legs/pen/notebook. Art/BCSDCharacterClean.png directly wired into scene and scene builder; original asset retained. Visible width kept near1.55 units by importer PPU, child scale1.15 unchanged. User scene tuning preserved. See Docs/GravityRelayCharacter.md for full generation prompt. No Unity Play/tests/compile/game visual validation executed.


2026-10-31 responsive Canvas: RelayScreenLayout unifies safe-area fitted 9:16 viewport, generation/flight world bounds and pointer hit area. RelayCanvasView uses saved uGUI Canvas/Scaler1080x1920/pointer-only InputSystem EventSystem hierarchy and matching Resources prefab; IMGUI removed. Safe-area play-frame scaling, bottom HUD anchor and four decorative space-extension RawImages. Portrait/default window1080x1920 settings, no forced physical resolution. Arithmetic examples recorded for9:16/19.5:9/20:9/tablet/insets, not device validation. User scene tuning retained; no Unity tests/Play/compile/build/visual checks. See Docs/GravityRelayScreenLayout.md.

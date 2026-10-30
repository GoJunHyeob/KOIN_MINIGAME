# Gravity Relay 프로토타입

## 실행

Unity 6000.3.25f1에서 `Assets/Scenes/GravityRelay.unity`를 열고 Play. Game 뷰는 9:16 권장. 카메라가 다른 화면 비율에도 세로 플레이 영역을 유지한다.

클릭/터치/Space(또는 기존 Attack 게임패드 바인딩)로 접선 발사. 비행/포획 중 추가 발사는 무시. 사망 후 같은 입력으로 즉시 재시작, R은 어느 상태에서든 재시작한다. 포획 완료마다 1점. 금색 링이 다음 목표이며 1→2→3→1로 반복한다. HUD의 BCSD 표기는 원본 미제공으로 임시 캐릭터임을 구분한다.

## 씬 오브젝트

- Main Camera: 직교 크기 8, 9:16 영역, 중앙 (0,0,-10).
- Planets: RelayPlanet 3개, 중심 (-1,-3.2), (1,0), (-0.75,3.2), 반경 0.8. 각 Surface / Orbit guide / Crater.
- Next target halo: 다음 행성 표시.
- BCSD Player: RelayPlayer, 임시 SpriteRenderer와 눈 장식. Rigidbody2D/Collider 불필요.
- Relay Input and HUD: RelayPresentation. 카메라/플레이어/마커/기존 InputActionAsset 참조가 저장되어 있다. Bcsd Sprite에 실제 이미지를 넣으면 임시 눈 장식이 숨겨진다. 교체 이미지의 PPU 또는 자식 스케일로 크기를 맞춘다.

## Inspector 초기값 (BCSD Player > RelayPlayer)

|항목|값|의미|
|---|---:|---|
|Orbit Clearance|0.38|공전 반경 = 행성 반경 + 여유|
|Orbit Degrees Per Second|110|공전 각속도 (착륙 시 진행 방향 보존)|
|Launch Speed|6|units/s, 접선 방향|
|Gravity Strength|18|목표 행성에만 중력 적용|
|Softening|0.8|가속도 = min(max, strength/(거리²+softening²))|
|Max Acceleration|12|units/s² 상한|
|Capture Margin|0.22|포획 반경 = 행성 반경 + 여유 + 이 값|
|Capture Duration|0.15|초, SmoothStep 착륙 보정|
|Flight Time Limit|4|초|
|Screen Exit Margin|0.25|화면 경계 바깥 허용 거리|

FixedUpdate에서 직접 위치/속도를 적분한다. 포획은 이동 선분과 반경 검사로 빠른 이동 누락을 방지한다. Capturing 동안 입력을 저장하지 않으며 포획 완료 시에만 점수를 올린다. 비행 시간은 발사 시 초기화. 재시작은 씬 재로드 없이 모든 이동/점수/예약 입력을 초기화한다.

## 확인 방법

1. 입력하지 않고 원형 공전이 유지되는지 확인.
2. 금색 링 방향으로 접선이 향할 때 클릭. 직후 추가 클릭은 비행을 바꾸지 않아야 한다.
3. 목표 접근 시 Capturing → Orbiting, 점수 +1, 다음 목표 변경 확인. 세 행성을 두 바퀴 이상 반복.
4. 반대 방향 발사로 화면 이탈, 또는 비행 시간 초과를 유도해 Dead 확인. 탭/R로 즉시 초기화.
5. 비행/포획 도중 R 재시작 후 다음 입력 한 번에 발사하는지 확인.
6. 9:16 및 가로 Game 뷰에서 모든 행성/점수/안내 표시 확인. 실제 터치는 기기 빌드에서 별도 확인.

`Gravity Relay > Validate Movement (outside Play Mode)`는 실제 Simulate 경로로 6번 착륙, 점수, Capturing 진입, 시간 초과, 화면 이탈, 재시작을 검증한다. 현재 GravityRelay 씬을 열고 Play 밖에서 실행한다. `Create Prototype Scene`은 씬이 이미 있으면 덮어쓰지 않는다.

## 파일

Runtime/RelayPlanet.cs: 행성 설정. Runtime/RelayPlayer.cs: 4상태 이동/포획/실패/점수. Runtime/RelayPresentation.cs: 입력/HUD/세로 화면. Editor/RelaySceneBuilder.cs: 씬/단순 도형 자산 생성 및 이동 검증. Art/: Disc.png, Ring.png, PrototypeSprite.mat. Assets/Scenes/GravityRelay.unity: 저장된 플레이 씬. Docs/AI/UnityProjectContext.md: 기존 프로젝트 조사 결과.

Editor/RelayPlayValidation.cs: 명시적으로 실행하는 배치 Play 입력 검증 도구. 평소 프로젝트를 여는 것만으로는 실행되지 않는다. 실행 메서드는 `GravityRelay.Editor.RelayPlayValidation.RunBatch`. 숨겨진 에디터의 포커스 제약을 피하기 위해 검증 세션에서만 InputSettings를 복제하고 모든 장치 입력을 허용한다. 원본 입력 자산/설정은 저장하거나 수정하지 않는다.

## 검증 결과 (2026-10-30)

- Unity 6000.3.25f1 실제 컴파일 성공, GravityRelay 씬 저장 및 메타/참조 생성 완료.
- 같은 이동 코드의 고정 간격 시뮬레이션: 6회 연속 착륙(두 바퀴), Capturing 진입, 정확한 점수 증가, 시간 초과, 화면 이탈, 예약 입력 제거 및 사망 후 재시작 통과.
- 실제 Play 모드: 씬 시작, 가상 마우스 클릭 발사, 사망 후 가상 터치로 재시작, Space 발사, 비행 중 R 재시작 통과. 검증 중 게임 런타임 오류 없음.
- 최초 자동 Play 입력 검사는 숨겨진 Game 뷰 포커스 제약으로 실패했으며 검증 세션 입력 설정 보정 후 재실행 통과. 터치의 기존 tap 바인딩은 복제본에서 press로 교체하여 누름/뗌 중복 명령을 방지했다.
- 로그: `C:/Users/Go/Documents/ChatGPT/GameJam2027/unity-validation.log`, `C:/Users/Go/Documents/ChatGPT/GameJam2027/unity-play-validation-2.log`.
- 자동 검사는 그래픽 없는 배치 환경에서 실행했다. 실제 화면 외관, 물리 터치 기기, 모바일 플레이어 빌드와 화면 방향은 미검증이다. 아래 수동 확인 절차로 확인한다.

기존 기획 대화의 상세 본문은 참조 텍스트만 반환되어 복원되지 않았다. 현재 요청에 명시된 물리 범위를 기준으로 초기값을 설정했다. 원본 BCSD 이미지, 무한 행성 생성, 모바일 배포/방향 설정, 사운드/이펙트는 포함하지 않는다. 기존 Build Settings는 SampleScene을 유지하므로 플레이어 빌드 시 GravityRelay를 직접 선택해야 한다.

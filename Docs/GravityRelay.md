# Gravity Relay 프로토타입

## 실행

Unity 6000.3.25f1에서 `Assets/Scenes/GravityRelay.unity`를 열고 Play. Game 뷰는 9:16 권장. 카메라가 다른 화면 비율에도 세로 플레이 영역을 유지한다.

클릭/터치/Space(또는 기존 Attack 게임패드 바인딩)로 접선 발사. 하늘색 화살표는 현재 접선 발사 방향이다(중력 곡선이나 성공 예측은 아님). 비행/포획 중 추가 발사는 무시. 사망 후 같은 입력으로 즉시 재시작, R은 어느 상태에서든 재시작한다. 포획 완료마다 1점과 녹색 확산 링, 캐릭터 강조, LANDED! +1 문구를 표시한다. 실패 시 붉은 화면 플래시와 사유를 표시한다. 금색 링은 다음 목표의 실제 포획 반경이며 목표 번호는 2→3→4…로 계속 증가한다. 착륙 후 카메라가 다음 구간을 비추는 동안에는 발사를 대기한다. HUD의 BCSD 표기는 원본 미제공으로 임시 캐릭터임을 구분한다.

현재 씬은 위로 계속 진행하는 무한 코스다. 생성·카메라·행성 회수·재시작과 Inspector 항목은 [GravityRelayEndless.md](GravityRelayEndless.md)에 정리했다.

## 씬 오브젝트

- Main Camera: 직교 크기 8, 9:16 영역, 중앙 (0,0,-10).
- Planets: 원래 RelayPlanet 3개를 템플릿으로 보존. Play 시 총 8개 풀을 구성해 위쪽 목표로 재배치/재사용. 반경 0.8, 각 Surface / Orbit guide / Crater.
- Endless Course: RelayEndlessCourse. 생성 범위, 재현 시드, 카메라, 회수 여유를 설정. BCSD Player의 Endless Course 참조로 연결.
- Next target halo: 다음 행성 표시.
- BCSD Player: RelayPlayer, 임시 SpriteRenderer와 눈 장식. Rigidbody2D/Collider 불필요.
- Relay Input and HUD: RelayPresentation. 카메라/플레이어/마커/기존 InputActionAsset 참조가 저장되어 있다. Bcsd Sprite에 실제 이미지를 넣으면 임시 눈 장식이 숨겨진다. 교체 이미지의 PPU 또는 자식 스케일로 크기를 맞춘다.

## Inspector 초기값 (BCSD Player > RelayPlayer)

아래 표는 "빵 쏘고 탁 받는" 조작감을 위한 코드/씬 기준값이다. 이전 값과 조정 근거, 무한 진행 인계 조건은 [GravityRelayBalance.md](GravityRelayBalance.md)에 기록한다. 재미에 대한 최종 튜닝은 에디터에서 직접 플레이해 결정한다.

|항목|값|의미|
|---|---:|---|
|Orbit Clearance|0.38|공전 반경 = 행성 반경 + 여유|
|Orbit Degrees Per Second|150|공전 각속도 (착륙 시 진행 방향 보존)|
|Launch Speed|15|units/s, 접선 방향, 조정 범위 10~20|
|Gravity Strength|60|목표 행성에만 중력 적용|
|Softening|0.8|가속도 = min(max, strength/(거리²+softening²))|
|Max Acceleration|40|units/s² 상한|
|Capture Margin|0.30|포획 반경 = 행성 반경 + 여유 + 이 값|
|Capture Duration|0.06|초, 빠른 착륙 보정|
|Flight Time Limit|4|초|
|Screen Exit Margin|0.25|화면 경계 바깥 허용 거리|

## 빠른 밸런스 비교와 화면 표시

BCSD Player의 Relay Player Inspector 맨 아래에 쉬움/기본: 빵·탁/정밀 버튼이 있다. 버튼은 Undo 가능하며 아래 네 값과 가속도 상한(40), 착륙 보정 시간(0.06초)을 변경한다.

|프리셋|Launch Speed|Gravity Strength|Capture Margin|Orbit Degrees Per Second|
|---|---:|---:|---:|---:|
|쉬움|12|55|0.42|125|
|기본: 빵·탁|15|60|0.30|150|
|정밀|18|70|0.18|170|

프리셋은 비교 시작점이며 실제 난이도와 조작감은 직접 플레이로 확인한다. 먼저 기본 설정으로 발사 방향을 익힌 다음 한 번에 한 값씩 조정한다. Play 중 수정한 값은 종료하면 되돌아가므로 수치를 기록하고 종료 후 재입력/씬 저장한다.

Planet 1~3 > Relay Planet > Radius를 변경하면 Surface 크기와 공전 가이드가 편집 모드와 Play 모드 모두 갱신된다. BCSD Player의 Orbit Clearance 변경도 각 가이드에 반영된다. 금색 목표 링은 Play 중 Capture Margin까지 반영하므로 포획 가능한 범위를 눈으로 확인할 수 있다. 반경을 크게 바꾸면 행성 간격이나 화면 배치를 함께 조정해야 한다.

Relay Input and HUD > Relay Presentation의 Arrow Length/Arrow Width는 방향 화살표 크기, Feedback Duration은 착륙 강조·실패 플래시 시간이다. 궤적 계산과 착륙 판정에는 영향을 주지 않는다.

발사 순간 0.10초 늘어남과 착륙 순간 0.14초 크기 튕김을 추가했다. 방향·크기 연출은 캐릭터 그림에만 적용하며 이동 물리나 게임 시간을 변경하지 않는다.

FixedUpdate에서 직접 위치/속도를 적분한다. 포획은 이동 선분과 반경 검사로 빠른 이동 누락을 방지한다. Capturing 동안 입력을 저장하지 않으며 포획 완료 시에만 점수를 올린다. 비행 시간은 발사 시 초기화. 재시작은 씬 재로드 없이 모든 이동/점수/예약 입력을 초기화한다.

## 확인 방법

1. 입력하지 않고 원형 공전이 유지되는지 확인.
2. 금색 링 방향으로 접선이 향할 때 클릭. 직후 추가 클릭은 비행을 바꾸지 않아야 한다.
3. 목표 접근 시 Capturing → Orbiting, 점수 +1, 위쪽 다음 목표 생성 확인. 3번째 이후에도 계속 위로 진행하는지 확인.
4. 반대 방향 발사로 화면 이탈, 또는 비행 시간 초과를 유도해 Dead 확인. 탭/R로 즉시 초기화.
5. 비행/포획 도중 R 재시작 후 다음 입력 한 번에 발사하는지 확인.
6. 9:16 및 가로 Game 뷰에서 모든 행성/점수/안내 표시 확인. 실제 터치는 기기 빌드에서 별도 확인.
7. 편집 모드/Play 모드 각각에서 Radius와 Orbit Clearance를 바꾸고 그림·가이드가 반영되는지 확인. Play 중 Capture Margin 변경 시 금색 포획 링 반영 확인.
8. 발사 직전 하늘색 화살표 방향과 실제 초기 비행 방향 비교. 착륙 시 녹색 링/+1, 실패 시 붉은 플래시·사유 확인. R 이후 색/링/안내가 초기화되는지 확인.

`Gravity Relay > Validate Endless Progression`은 실제 Simulate 경로의 임시 복제본에서 100회 착륙, 위쪽 목표, 카메라 고정, 풀 유지, 점수, 시간 초과, 화면 이탈, 재시작을 검증한다. 현재 GravityRelay 씬을 열고 Play 밖에서 실행한다. 기존 Validate Movement 메뉴도 무한 코스가 연결돼 있으면 이 검사로 연결된다. `Create Prototype Scene`은 씬이 이미 있으면 덮어쓰지 않는다.

## 파일

Runtime/RelayPlanet.cs: 행성 설정. Runtime/RelayPlayer.cs: 4상태 이동/포획/실패/점수. Runtime/RelayPresentation.cs: 입력/HUD/세로 화면. Editor/RelaySceneBuilder.cs: 씬/단순 도형 자산 생성 및 이동 검증. Art/: Disc.png, Ring.png, PrototypeSprite.mat. Assets/Scenes/GravityRelay.unity: 저장된 플레이 씬. Docs/AI/UnityProjectContext.md: 기존 프로젝트 조사 결과.

Editor/RelayPlayerEditor.cs: Inspector 프리셋과 기존 씬 표시 참조 연결 도구. 현재 씬은 연결 완료 상태로 저장한다. 이후 기존 씬을 복사하거나 표시 오브젝트를 교체했다면 Gravity Relay > Refresh Balance Visual References를 명시적으로 실행한 뒤 씬을 저장한다. 행성 표시 갱신은 직접 저장된 참조를 사용하며 런타임 이름 검색을 하지 않는다.

Editor/RelayPlayValidation.cs: 명시적으로 실행하는 배치 Play 입력 검증 도구. 평소 프로젝트를 여는 것만으로는 실행되지 않는다. 실행 메서드는 `GravityRelay.Editor.RelayPlayValidation.RunBatch`. 숨겨진 에디터의 포커스 제약을 피하기 위해 검증 세션에서만 InputSettings를 복제하고 모든 장치 입력을 허용한다. 원본 입력 자산/설정은 저장하거나 수정하지 않는다.

## 검증 결과 (2026-10-30)

- Unity 6000.3.25f1 실제 컴파일 성공, GravityRelay 씬 저장 및 메타/참조 생성 완료.
- 같은 이동 코드의 고정 간격 시뮬레이션: 6회 연속 착륙(두 바퀴), Capturing 진입, 정확한 점수 증가, 시간 초과, 화면 이탈, 예약 입력 제거 및 사망 후 재시작 통과.
- 실제 Play 모드: 씬 시작, 가상 마우스 클릭 발사, 사망 후 가상 터치로 재시작, Space 발사, 비행 중 R 재시작 통과. 검증 중 게임 런타임 오류 없음.
- 최초 자동 Play 입력 검사는 숨겨진 Game 뷰 포커스 제약으로 실패했으며 검증 세션 입력 설정 보정 후 재실행 통과. 터치의 기존 tap 바인딩은 복제본에서 press로 교체하여 누름/뗌 중복 명령을 방지했다.
- 로그: `C:/Users/Go/Documents/ChatGPT/GameJam2027/unity-validation.log`, `C:/Users/Go/Documents/ChatGPT/GameJam2027/unity-play-validation-2.log`.
- 자동 검사는 그래픽 없는 배치 환경에서 실행했다. 실제 화면 외관, 물리 터치 기기, 모바일 플레이어 빌드와 화면 방향은 미검증이다. 아래 수동 확인 절차로 확인한다.

기존 기획 대화의 상세 본문은 참조 텍스트만 반환되어 복원되지 않았다. 현재 요청에 명시된 물리 범위를 기준으로 초기값을 설정했다. 원본 BCSD 이미지, 모바일 배포/방향 설정, 사운드는 포함하지 않는다. 기존 Build Settings는 SampleScene을 유지하므로 플레이어 빌드 시 GravityRelay를 직접 선택해야 한다.

## 밸런스·피드백 작업 확인

열린 Unity 에디터에서 변경 스크립트가 컴파일되어 새 Inspector와 연결 메뉴가 표시됨을 확인했다. 기존 씬에 행성 Surface/Orbit guide/플레이어 참조를 연결하고 저장했으며, 씬의 사용자 튜닝값 7/22/120을 유지했다. 별도 배치 실행은 같은 프로젝트가 열려 있어 진행하지 못했고, 현재 에디터를 통해 씬 설정을 완료했다. 이번 변경의 조작감과 화살표·착륙·실패 효과의 실제 Play 확인은 사용자가 수행한다. 이전 자동 이동·입력 검증 결과는 아래 업데이트의 런타임 검증 결과를 의미하지 않는다.

# 화면 크기 · Canvas · 앱 화면 기준

2026-10-31. Unity 6000.3, 기존 uGUI 2.0/Input System 1.20 재사용. 추가 패키지 없음.

## 통일한 기준

RelayScreenLayout이 유일한 화면 계산 소유자다. Unity Player 창 크기와 Screen.safeArea의 교차 영역 안에 9:16 직사각형을 최대로 맞춘다. 세로 게임 카메라의 viewport와 aspect, 터치/마우스 허용 범위, 행성 생성의 미래 화면 경계, 비행 시작 시 고정하는 실패 경계가 이 기준을 공유한다. 카메라 orthographicSize=8 기준 플레이 세계는 모든 화면에서 9×16 units다. 비행 중 카메라/실패 경계 고정 정책과 물리·생성 수치·공전 반경은 유지한다.

입력은 공통 플레이 영역 안에서 받고 UI 버튼은 실제 RectTransform 영역으로 먼저 제외한다. 배경용 여백과 안전 영역 밖을 눌러도 발사하지 않는다. 화면/안전 영역이 바뀌면 진행 중 입력을 소비하고 완전히 뗀 뒤 새 입력을 받는다.

## 정식 UI

IMGUI/OnGUI/GUIStyle/수동 좌표 히트 테스트를 제거했다. GravityRelay 씬에 편집 가능한 Canvas·CanvasScaler·GraphicRaycaster·일반 Text/Image/Button·InputSystemUIInputModule·EventSystem을 저장했다. Scale With Screen Size, Reference Resolution=1080×1920, Match Width Or Height=0.5. 카메라와 동일한 Safe 9x16 Play Frame 안에서 1080×1920 Design Root를 맞춘다. 하단 HUD는 아래쪽 앵커를 사용하고, 시작·결과·일시정지는 각 패널로 분리한다. Pretendard Regular/SemiBold, 선명한 점수판, 클릭/터치 전용 버튼·즉시 재시작·입력 중복 방지를 유지한다. UI 이동/Submit/Cancel 키보드 액션과 버튼 Navigation은 비활성화한다. Android 시스템 Back은 기존 별도 처리다.

같은 UI를 UI/Resources/GravityRelayCanvas.prefab에도 저장해 새 씬 생성과 이전 씬의 런타임 대체 경로에서 사용한다. UI는 OnEnable/OnDisable에 버튼 리스너를 대칭적으로 연결/해제하며 화면마다 새로 생성하지 않는다. 빈 배경과 설명 Text는 Raycast Target=false라서 플레이 터치를 막지 않는다.

## 배경과 빌드 설정

9:16 영역 밖은 같은 우주 배경을 4개의 재사용 RawImage로 잘라 표시한다. 기기 비율을 채우는 배경 전용 영역이며 플레이 세계를 늘리거나 추가 행성을 노출하지 않는다. 안쪽의 기존 3타일 시차 배경은 유지한다. 바깥 배경은 더 어둡게 표시하며 입력을 받지 않는다.

PlayerSettings는 세로 Portrait와 기본 창 1080×1920으로 변경했다. 실제 기기의 렌더 픽셀 해상도를 1080×1920으로 강제하지 않으며 Screen.SetResolution을 호출하지 않는다. 앱에 포함할 때는 호스트 앱의 Unity 창과 네이티브 안전 영역/회전/뒤로 가기 전달을 실제 연동에서 확인해야 한다.

## 대표 비율 배치 계산

다음은 동일한 fit 수식으로 산출한 수치다. 실제 기기/Unity 화면을 실행한 결과가 아니다. 좌표 원점은 Unity 화면의 좌하단이다.

|사례|Unity 창 크기|플레이 영역 위치·크기(px)|세계 크기|
|---|---|---|---|
|9:16|1080×1920|(0, 0), 1080×1920|9×16 units|
|9:19.5|1080×2340|(0, 210), 1080×1920|9×16 units|
|9:20|1080×2400|(0, 240), 1080×1920|9×16 units|
|Tablet 3:4|1536×2048|(192, 0), 1152×2048|9×16 units|
|9:20 safe area|1080×2400|(0, 234), 1080×1920|9×16 units|
|Embedded window example|800×1400|(28.75, 40), 742.5×1320|9×16 units|

안전 영역 사례는 임의의 상·하단 여백을 준 계산 예시이며 특정 휴대폰 사양을 뜻하지 않는다. 9:16·9:19.5·9:20·태블릿에서 같은 게임 세계를 유지하고 여백만 변한다. 추후 사용자가 Device Simulator/실기기에서 노치/홈 표시줄, 버튼 터치, 화면 밖 실패, 앱 복귀·회전을 확인해야 한다.

사용자 요청에 따라 Unity Play·컴파일·빌드·자동 테스트·실기기 화면 검사는 실행하지 않았고 테스트 전용 코드를 추가하지 않았다. 기존 사용자 씬 튜닝은 보존하고 이번 Canvas 추가 부분만 커밋한다.

참고: https://docs.unity3d.com/Packages/com.unity.ugui@2.0/manual/script-CanvasScaler.html

안전 영역: https://docs.unity3d.com/6000.3/Documentation/ScriptReference/Screen-safeArea.html

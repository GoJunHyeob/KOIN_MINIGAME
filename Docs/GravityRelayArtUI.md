# 아트와 UI 기준

2026-10-31. 현재 GravityRelay 씬과 새 씬 생성 기본값에 반영.

## 색상

검정에 가까운 BCSD 몸체와 보라색 눈, 연보라 기본 행성, 보라색 중력/공전 고리, 짙은 보랏빛 남색 배경으로 통일했다. 다음 목표의 포획 범위도 보라색이며 현재 실제 공전 중인 행성의 공전 가이드만 표시하는 규칙은 유지한다. 착륙 시 보라색 파동과 크기 튕김을 사용하고 검정 몸체 색은 유지한다. 실패는 분홍색 틴트로 알린다. 파랑·분홍 특수 행성은 실제 특수 기능과 함께 도입할 향후 표현으로 남겨 둔다. 원본 BCSD 이미지가 아직 연결되지 않아 기존 임시 캐릭터의 색을 변경했다. Presentation의 BCSD Sprite에 원본을 넣으면 해당 이미지를 사용한다.

## 화면과 입력

- 플레이 중에는 점수·최고 점수와 짧은 +1만 하단 작은 영역에 표시한다. 상단 제목·목표 번호·상시 설명·캐릭터 이름 표시는 제거했다. 위쪽 다음 목표와 접근 경로를 비워 둔다.
- Screen.safeArea와 세로 화면 영역의 교차 영역 안에 UI를 맞춰 노치·홈 표시 영역을 피한다. 카메라·생성·발사 물리와 비행 중 화면 경계는 바꾸지 않는다.
- 시작·재시작·계속 버튼은 하단 엄지 영역에 큰 크기로 배치한다. 결과에서 바로 재시작하며 중간 화면이나 씬 재로딩을 넣지 않는다.
- 플레이 중 터치 발사는 화면 전체에서 허용하고 클릭은 게임 뷰 안에서 허용한다. 버튼으로 화면이 바뀔 때는 입력을 완전히 뗀 후 새로 누르도록 해 중복 발사를 막는다.
- 기존 Input System UI/Cancel 액션으로 뒤로 가기를 처리한다. 플레이 중 일시정지, 일시정지 중 재개, 결과에서 시작 화면으로 이동한다. 시작 화면에서 앱 강제 종료를 호출하지 않는다. 일시정지는 플레이어 시뮬레이션만 멈추며 Time.timeScale을 변경하지 않는다. 실제 Android 시스템/호스트 앱 뒤로 가기 전달과 물리 터치 동작은 아직 확인하지 않았다.

## Pretendard

공식 Pretendard v1.3.9 배포본의 정적 Regular/SemiBold OTF를 Assets/GravityRelay/Art/Fonts에 포함했다. 로컬 폰트를 씬에 직접 참조하므로 설치 폰트나 실행 중 네트워크 다운로드에 의존하지 않는다. 설명은 Regular, 제목·점수·버튼은 SemiBold를 사용한다. SIL Open Font License 원문은 같은 폴더의 LICENSE.txt에 포함한다.

출처: https://github.com/orioncactus/pretendard/releases/tag/v1.3.9

UI/색상 변경에 대한 테스트·Play·빌드·컴파일·화면 검사는 사용자 요청에 따라 실행하지 않았다. 실제 폰트 렌더링과 기기 안전 영역 배치는 에디터/기기에서 확인되지 않았다.


2026-10-31 크기 재조정: 행성 반경 0.575 → 0.66, BCSD 표시 Scale 0.725 → 0.835(각각 약 15% 확대). 공전 가이드와 실제 경로를 그대로 유지하기 위해 Orbit Clearance를 1.3 → 1.215로 보정했다. 합산 공전 반경은 1.875(가이드 지름 3.75)로 유지되며 포획 반경·가이드 표시 조건도 유지한다. 씬 세 행성 템플릿과 생성 기본값을 함께 적용했다. 테스트·Play·컴파일 검사는 실행하지 않았다.


## 우주 배경 (2026-10-31)

이미지 생성 내장 도구로 짙은 남색/보라색 우주, 양쪽의 은은한 보라 성운과 작은 별을 제작했다. 중앙은 어둡게 비워 기존 행성과 캐릭터의 가독성을 우선했다. 최종 파일은 Assets/GravityRelay/Art/Resources/GravityRelaySpace.png이며 프로젝트에 포함한다.

RelayPresentation이 시작할 때 Resources의 기본 Sprite 또는 Inspector의 Space Background를 한 번 불러오고 RelaySpaceBackground에 전달한다. 카메라 뒤쪽에 SpriteRenderer 타일 3개를 한 번 생성해 재사용하며, 위아래를 번갈아 반사해 경계의 픽셀이 이어지도록 한다. 카메라 이동량의 12%로 흐르는 배경이며 밝기는 0.9다. 멈춘 카메라에서는 정지하고 재시작 시 카메라 이동과 함께 원래 구간으로 돌아온다. 별도 충돌체·중력·실패 판정은 없다. 렌더 순서 -100으로 게임 요소 뒤에 표시하며 기존 실패 색상 피드백도 반영한다. 기존 씬의 사용자 변경(포획 여유 등)은 보존했고 이번 배경 커밋에 포함하지 않았다.

Inspector: Relay Input and HUD > Relay Presentation > Space Background / Background Brightness / Background Parallax. Sprite를 지정하지 않아도 기본 배경을 사용한다. 실제 Play·빌드·컴파일·화면 검사는 실행하지 않았다.

생성 방식: 내장 image_gen. 최종 프롬프트:

> Use case: stylized-concept. Create one finished background texture asset for a minimalist portrait 2D mobile space arcade game called Gravity Relay. Tall portrait composition, ideally 1024x1792. Pure background only: very deep indigo/navy and blackened purple space, delicate diffuse purple nebula clouds mostly toward the outer sides, restrained sparse tiny lavender and cool white stars, a few subtly brighter points without big flares. Smooth clean 2D painted gradients, premium quiet atmospheric minimalism, no photorealistic NASA look, no grain. The middle two-thirds must remain very dark and calm with generous negative space so black mascot with purple eyes, pastel lavender planets and purple orbit rings stand out. Overall dim and low contrast, no large bright areas. Avoid all planets, moons, rings, characters, ships, text, logos, borders and UI. Top and bottom edges should be uniformly very dark indigo, with minimal distinctive features near edges to support seamless vertical repeated scrolling. Not a game screenshot or concept sheet, just one usable opaque background texture.


2026-10-31 배경 씬 연결 수정: 기존 방식은 Start에서만 배경 오브젝트를 생성하므로 편집 중 씬에 표시되지 않았다. GravityRelay 씬에 Space Background 루트와 3개 SpriteRenderer 타일을 직접 저장하고 이미지·카메라·Presentation 참조를 연결했다. RelaySpaceBackground는 ExecuteAlways로 편집 중에도 기존 타일의 표시와 위치를 갱신하며 편집 중 오브젝트를 생성하지 않는다. Play 시작 시에는 저장된 배경을 재사용해 중복 생성을 피한다. 사용자의 기존 씬 변경은 보존하고 배경 추가 부분만 커밋한다. Play·테스트·컴파일·에디터 화면 검사는 실행하지 않았다.

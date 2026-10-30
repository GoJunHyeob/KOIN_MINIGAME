# 콤보 · 시청각 피드백 · 클릭 UI

2026-10-31. 원본 자산을 검색했으며 현재 프로젝트에는 BCSD 원본 이미지/표정 시트/사운드가 없다. 기존 Disc/Ring, 검정 몸체와 보라색 눈, Pretendard, 우주 배경을 재사용했다.

## 콤보 규칙

RelayPlayer가 착륙 완료마다 Combo를 1 증가시키고 PeakCombo를 갱신한다. 첫 착륙은 1콤보이며 대기 시간 제한은 없다. 실패 시 Combo=0, 결과에는 이번 플레이 최대 콤보를 표시한다. 재시작은 Combo/PeakCombo=0이다. 콤보는 점수 배수나 물리값을 바꾸지 않는다.

## 시청각 변화

- 발사: 기존 짧은 늘어남, 뒤쪽 보라 파티클, 상승하는 짧은 효과음, 집중한 눈.
- 착륙: 기존 튕김·파동, 원형 입자, 짧은 화음, 웃는 표정. 콤보가 늘면 숫자 크기, 고리의 부드러운 발광, 입자 수/크기, 소리 음높이가 증가한다.
- 5콤보 이상: 평소에도 자신감 있는 눈과 더 큰 웃음. 실패: 분홍 입자와 기존 틴트, 내려가는 효과음, 슬픈 표정.
- 기본 Combo Feedback Cap=15에서 연출 강화를 멈춘다. 점수·콤보 수치는 계속 증가한다. 입자는 종류별 96개 상한과 시스템 3개를 재사용하며 착륙 발사량은 최대 48개다. 음높이는 1~1.35배다. 고리 발광은 후처리 Bloom 대신 겹친 반투명 선 2개를 사용한다.
- 방향 화살표는 캐릭터 바깥에서 시작한다. 정확한 포획 경계는 선명한 보라색으로 유지하며 발광이 판정 반경을 바꾸지 않는다. 연출은 플레이어 이벤트를 구독하며 대기 코루틴·히트스톱·Time.timeScale 변경이 없다. 빠른 상태 전환도 이벤트당 한 번 소리/입자를 발생시킨다.
- 재시작은 기존 풀을 재사용하고 이전 소리·입자를 즉시 지운다. 원본 자산은 시작 시 한 번 로드하며 재시작 중 새로 로드하지 않는다.

## 교체할 임시 자산

Assets/GravityRelay/Audio/Resources/GravityRelaySfx/PlaceholderLaunch.wav, PlaceholderLanding.wav, PlaceholderFailure.wav는 이번에 합성한 임시 효과음이며 실제 BCSD 제공 사운드가 아니다. 작은 PCM WAV를 프로젝트에 포함한다.

씬의 Relay Input and HUD > Relay Presentation:

- BCSD Sprite: 원본 기본 캐릭터. 미지정 시 기존 검정 원형 임시 캐릭터와 보라색 눈/선으로 만든 임시 표정을 사용한다.
- Launch / Landing / Combo / Failure Expression: 선택적 표정 Sprite. 원본 기본 Sprite와 함께 같은 크기·피벗 기준으로 교체한다. 없는 표정은 기본 Sprite를 사용한다.
- Launch / Landing / Failure Sound: 원본 AudioClip 교체 위치. 미지정 시 위 Placeholder WAV를 자동으로 사용한다.
- Effects Volume=0.45, Combo Feedback Cap=15: 볼륨과 강화 상한.

임시 캐릭터를 실제 BCSD 아트로 주장하지 않으며 원본 캐릭터 파일은 추가하지 않았다.

## UI와 조작

플레이 하단 점수판을 불투명한 짙은 패널과 연보라 테두리로 바꿨다. 현재 점수는 흰색 Pretendard SemiBold 36, 최고 점수도 밝은 색으로 표시한다. 콤보는 최대 크기를 제한해 같은 패널 안에서 확대된다.

시작·재시작·시작 화면·계속·일시정지는 화면 버튼 클릭/터치로 조작한다. 일시정지 버튼을 누르는 영역은 발사 입력에서 제외하고, 화면 전환 후 입력을 완전히 뗀 다음 새 입력을 받아 중복 발사를 막는다. Space/Enter/R 및 게임패드 UI 단축키를 제거하고 IMGUI 키보드 버튼 활성화도 차단했다. 발사도 마우스/터치만 받는다. Android 실제 빌드에서만 시스템 뒤로 가기를 일시정지/재개에 연결하며 데스크톱 Esc는 메뉴 단축키로 사용하지 않는다.

사용자 요청에 따라 테스트·Play·컴파일·시청각 검사는 실행하지 않았다. 기존 씬의 사용자 튜닝은 보존한다. 런타임 기본값과 Resources 자동 로드로 현재 씬에서 별도 연결 없이 적용하도록 구성했다.


## 사용자 제공 BCSD 캐릭터 연결

2026-10-31 사용자 첨부 투명 PNG를 원본 그대로 Assets/GravityRelay/Art/BCSDCharacter.png에 복사했다. 검정 몸체와 보라색 고리를 보존하며 이미지 생성/배경 제거/표정 재그리기는 하지 않았다. 씬의 BCSD Character SpriteRenderer와 Presentation > BCSD Sprite에 직접 연결하고 원본 색을 표시하도록 Renderer 색상을 흰색으로 저장했다. 임시 눈 2개는 비활성화했다.

PPU=320, 캐릭터 자식 Transform Scale=1.15. 512×512 캔버스 안의 실제 캐릭터는 약 430×284px이므로 화면상 실제 크기는 약 1.55×1.02 units다. 기존 임시 원형 캐릭터 지름 0.835에 비해 가로 약 85%, 높이 약 22% 커진다. 공전 반경·가이드·행성 크기는 바꾸지 않는다. 방향 화살표는 실제 SpriteRenderer 경계 바깥에서 시작한다. 추가 조정은 BCSD Player > BCSD Character의 Transform Scale에서 한다.

Feedback Particle Sprite에는 기존 Disc를 직접 연결해 새 캐릭터 그림이 파티클 텍스처로 사용되지 않도록 했다. 콤보 숫자·고리·입자·사운드·늘어남/튕김 효과는 유지한다. 제공된 로고는 단일 표정 이미지이므로 원본 사용 중에는 임시 눈/입을 덧그리지 않으며, 다른 표정은 Presentation의 Launch/Landing/Combo/Failure Expression 슬롯에 같은 크기·피벗의 Sprite를 넣으면 표시한다. 기존 Placeholder WAV는 임시 사운드다.

기존 사용자 씬 튜닝을 보존하고 이번 캐릭터 연결 부분만 커밋한다. 테스트·Play·컴파일·Unity 화면 검사는 실행하지 않았다.

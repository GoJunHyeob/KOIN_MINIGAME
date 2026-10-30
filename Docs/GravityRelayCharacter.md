# BCSD 캐릭터 정리 버전

사용자가 제공한 두 번째 참고 이미지를 내장 image_gen 도구로 수정했다. 둥근 검정 몸체·보라색 고리·작은 흰 눈과 입을 유지하고, 손·팔·회색 발·다리·펜·수첩을 모두 제거한 투명 PNG다. 원본 BCSDCharacter.png는 보존하며 새 파일 Assets/GravityRelay/Art/BCSDCharacterClean.png를 씬 SpriteRenderer와 Presentation > BCSD Sprite에 연결했다. 새 씬 생성 기본 경로도 갱신했다.

캐릭터 자식 Scale=1.15를 유지하고 이미지의 실제 보이는 폭을 기준으로 PPU=753.806452를 적용해 약 1.55 units 폭으로 표시한다. 공전 경로·가이드·행성 크기·기존 사용자 씬 튜닝은 변경하지 않는다. Sprite 경계에 따른 방향 화살표 위치 보정과 기존 콤보·입자·사운드·크기 연출을 유지한다. 이번 이미지는 단일 기본 표정이며 선택적 표정 슬롯은 유지한다.

교체 위치: Relay Input and HUD > Relay Presentation > BCSD Sprite, BCSD Player > BCSD Character > SpriteRenderer. 추가 크기 조정은 캐릭터 자식 Transform > Scale.

## 비행·도착 연출

- 클릭/터치로 발사해도 BCSD 캐릭터 그림은 더 이상 진행 방향으로 회전하지 않는다. 짧은 반동 크기만 적용한다.
- BCSD는 기본 Sprite 표정을 그대로 사용하며, 별도의 집중 표정 오버레이는 표시하지 않는다. 발사 파티클만 출발 반응으로 유지한다.
- Trail Renderer는 `BCSD Player > BCSD Character`에 저장된 씬 컴포넌트다. 플레이 중 공전·비행 상태에서 켜지며, 포획·사망·재시작 때는 새 잔상이 만들어지지 않는다.
- 기본값은 `Time 0.85`, `Width Multiplier 0.34`이다. 폭 곡선은 처음부터 끝까지 `1`로 유지해 꼬리가 가늘어지지 않는 긴 사각 리본 형태이며, 캐릭터 쪽은 밝은 라벤더·뒤쪽은 투명한 진보라로 설정했다. Inspector에서 `Time`, `Width`, `Color`, `Width Curve`를 직접 조절하면 된다.
- `Relay Presentation > Character Display Scale` 기본값은 0.88로, 씬에 저장한 캐릭터 크기를 유지하면서 표시만 12% 줄인다.
- 공전 궤도에 도착하면 기존 도착 입자와 함께 두 겹의 파동이 나타난다. 하나는 바깥으로 퍼지고, 다른 하나는 바깥쪽에서 실제 공전 반경까지 수렴해 ‘궤도에 안착’하는 순간을 보여 준다.

실제 Unity Play·테스트·컴파일·게임 화면 검사는 사용자 요청에 따라 실행하지 않았다.

## 최종 생성 프롬프트

Edit the attached BCSD mascot reference into exactly one clean isolated transparent game sprite. Preserve its identity and minimalist flat 2D vector-like look: a soft round near-black charcoal body; a vivid purple Saturn-like elliptical ring wrapping diagonally around its middle; two small round white eyes positioned slightly to the viewer's right of the body center; a tiny short white mouth below/between the eyes; a friendly neutral expression. REMOVE BOTH HANDS, ALL ARMS, ALL LEGS AND ALL GRAY FEET. REMOVE the pen and notebook completely. Reconstruct the plain charcoal round body and uninterrupted purple ring behind the removed props and hands. The finished silhouette consists ONLY of the charcoal round body and purple ring, with small white facial features inside the body. No limbs, no gray shapes anywhere, no appendages, no accessories. Remove the white background and every cropped surrounding character or gray background mark. Exactly one complete centered mascot with modest equal padding on a square canvas, no cropping. Genuine transparent alpha background, no baked checkerboard, no ground or shadow, no text, no watermark. Smooth crisp solid-color shapes, no shading, no gradients, no 3D. Keep the reference's cute white eyes and small mouth, not black eyes. Intended for the BCSD player in a dark purple space arcade game.

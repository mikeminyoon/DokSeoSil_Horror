# Cubicle08 Fire Damage (Unity)

## 사용법
1. Unity에서 Assets > Import Package > Custom Package... 로 이 .unitypackage 를 가져옵니다.
2. `Assets/Cubicle08_FireDamage/Models` 의 FBX 를 씬에 드래그하면 텍스처가 입혀진 상태로 나옵니다.
3. 같은 변형(A/B/C)의 Desk/Shelf/Chair/Lamp/Floor 를 모두 위치 (0,0,0) 에 두면 서로 맞물립니다.

## 변형
- A: 심하게 탐 (패널 구멍, 녹은 의자, 탄 책)
- B: 그을음 위주 (형태는 멀쩡)
- C: 가벼운 그을음

## 렌더 파이프라인
- 머티리얼은 URP/Lit 로 미리 연결돼 있습니다.
- Built-in(기본) 또는 HDRP 프로젝트면 가져올 때 자동으로 해당 파이프라인용으로 다시 연결됩니다.
  핑크색으로 보이면 메뉴 Tools > Cubicle08 > Rebuild Materials 를 한 번 눌러주세요.
- HDRP 는 BaseColor + Normal 만 연결됩니다 (마스크맵 포맷이 달라서).

## 참고
- 1 unit = 1 m. 콜라이더는 없으니 필요하면 Box/Mesh Collider 를 추가하세요.
- 책등 텍스처에 실제 출판사 로고가 들어 있을 수 있으니 상업적으로 쓸 때는 교체하세요.

# CLAUDE.md — 독서실 경비 (DokSeoSil_Horror) 프로젝트 인수인계

> 이 문서는 웹 Claude와 준현(mikeminyoon)이 여러 세션에 걸쳐 만든 프로젝트를,
> Claude Code가 **맥락 손실 없이 이어받기 위한** 완전 인수인계서다.
> "이전 Claude를 그대로 옮긴다"는 목적으로 작성됨. 처음 읽고 이 프로젝트의
> 설계 철학·완성 시스템·코드 구조·함정·남은 작업·협업 방식을 전부 파악할 것.
>
> **마지막 갱신: 2026-11-24** (Claude Code 세션 — 오디오/카메라 에러, 리셋 패널, 비상 종 재정의, 밤 숫자 단일화).
> 이 세션의 설계 결정 원문은 **구현노트 §15**에 있다.

---

## 0. 가장 먼저 — 이 프로젝트가 뭔가

**FNAF(Five Nights at Freddy's) 스타일 호러 생존 게임.** 한국 독서실(불타 폐허가 된 5일 밤 경비)을 배경으로, 실제 친구들을 3D 스캔해 귀신으로 등장시킨 헌정 게임(친구들 허락받음). 플레이어는 관리실에 앉아 CCTV·창문·환풍구 셔터·오디오(종)·전력으로 밤을 버틴다.

- **엔진:** Unity URP (Unreal 대신 개발 속도/친숙도로 선택)
- **개발 환경:** MacBook Air M2, VS Code
- **작업 언어:** 반드시 **한국어**
- **레포:** https://github.com/mikeminyoon/DokSeoSil_Horror.git (private, git-lfs, 세션 끝마다 commit/push)
- **레퍼런스:** FNAF 1(문/전력), FNAF 2(퍼펫 — 오디오 방치 즉사 느낌), FNAF 3(스프링트랩=엔진A, 오디오 유인, 시스템 리셋, 카메라 에러), Sister Location(브레이커 룸)

### 핵심 개발 철학 (반드시 지킬 것)
1. **메커니즘 먼저, 그래픽은 맨 나중(Phase 8).** 회색 박스로 로직부터 검증. 애니메이션·조명·사운드·저해상도 필터·맵 디테일은 전부 Phase 8.
2. **설계 → 말로 알고리즘 확인 → 유저 이해 확인 → 그다음 코드.** 절대 코드부터 던지지 말 것.
3. **코드는 한글 주석 + 작게 쪼개서.** 통파일 교체를 선호(부분 수정은 유저가 헷갈려함).
4. **문서 우선.** 기획/귀신 행동 관련 제안은 **반드시 기획서·구현노트 먼저 확인 후** 말할 것. 추측으로 답하다 여러 번 틀렸고(창고런·준영 즉사 여부 등) 유저가 교정했다. 문서가 진실의 원천. **단 문서끼리, 문서와 코드가 어긋날 때가 있음**(예: 종 규칙 — 구현노트 §11 vs 코드). 어긋나면 추측하지 말고 유저에게 어느 쪽이 현행인지 물을 것.
5. **모듈화.** 한 파일에 다 넣지 말고 기능별 분리.
6. **최적화 고려.** (특히 Debug.Log 폭주 주의 — 아래 함정 참고)
7. **많은 부분을 수정해야 하면 반드시 유저에게 먼저 물을 것.** 요청이 불명확하면 실행 말고 이해부터 확인.
8. **한 일은 정확히 보고.** 커밋/푸시는 명령 결과를 확인한 뒤에만 "했다"고 말할 것(2026-11-24에 실행 안 한 커밋을 했다고 말한 사고 있었음).

---

## 1. 유저(준현)에 대해 — 협업 방식

- **강력한 독자적 설계 직관.** 시스템의 빈틈을 스스로 잘 찾아냄: 타이머 충돌, 시야 회전 사각, 방심 찌르기, "빼꼼은 벗어날 수 없다", "창문앞부터가 대치다", CCTV ON/OFF 상충이 겹침의 진짜 축, 비상 종 아이디어, "연호가 앞에 있는지 몰라야 짜릿하다" 등 — 전부 유저가 먼저 지적. **단순 검증자가 아니라 대등한 협업자로 대하고, 비판적으로 함께 사고할 것.**
- "간 줄 알았는데 사실 있었다" 류의 **심리전·불확실성을 매우 중시.** 플레이어 친화적 장치(예: 연호 도착 시 CCTV 강제 다운)는 "너무 친절하다"며 거절함.
- 직접 플레이해보고 수치를 조정함(예: 연호 창문앞 시간 6초 → 약 12초). 씬의 Inspector 값이 문서 값과 다르면 의도된 조정일 수 있으니 바로 되돌리지 말 것.
- 짧고 집중된 세션을 선호. 논리적 milestone에서 끝내고 git commit.
- **에디터 세팅(Inspector 값, 노드 배치, roomFeeds 순서 등)은 코드에 없어서 웹 Claude가 반복해서 되물었고 유저가 답답해했다.** → Claude Code는 `Assets/Scenes/Main.unity`(YAML)를 직접 grep해서 확인할 것. 참조 필드가 비었는지(`{fileID: 0}` 또는 줄 자체가 없음), 버튼 OnClick이 뭘 부르는지(`m_MethodName`) 다 씬 파일에 있음.

---

## 2. 맵 / 시야 / 방 구조 (씬 세팅 — 코드에 안 나옴, 중요)

### roomFeeds 배열 순서 (MonitorDisplay의 Texture[] — 유저가 확정)
```
[0] 공부방1   [1] 공부방2   [2] 가로복도   [3] 화장실
[4] 로비      [5] 세로복도   [6] 창고
```
CCTV 7개(CAM01~07)가 이 방들에 대응. **CAM07(창고)은 항상 스태틱**(화면 X, 소리만 — 소리는 Phase 8).

### 시야 4구역 (ViewController, zoneAngles ≈ {-60, 0, 60, 180})
- **왼쪽(zone 0):** 리셋 패널 (오디오/카메라 리셋, 전체 리셋 — 환기는 나중)
- **가운데(zone 1):** 컴퓨터(CCTV + 종 버튼) / CCTV 내리면 정면 **창문**(육안 실루엣 확인) + 창문 옆 벽 **비상 종**(World 버튼)
- **오른쪽(zone 2):** 환풍구 + 셔터 + 불빛 버튼 (현우 대응)
- **후면(zone 3):** 창고 통로 ("뒤돌아보기" 버튼, 정전 시에만 진입 — 미구현)
- **한 번에 한 구역만.**
- ⭐ **화면 전환의 정의 (ScreenTransitionDetector 코드 기준):** **CCTV 올림/내림 + 리셋 패널 열기/닫기**만 전환. **시야 회전은 전환 아님**(부드러운 연속). 환기 오류 등은 `ForceTransition()`으로 강제 전환. → 환풍구 보다가 고개 돌려 창문 보는 건 안전, 패널에서 나오는 건 위험.
- **구역별 미세추적(microYaw/microPitch) 값을 배열로 다르게** 줌: 왼12/가운데15/환풍구5/후면10. 환풍구를 낮춘 건 셔터 버튼 클릭 안정 위해. **가운데는 낮출 수 없음 — 현승 창문 응시 대응 때문.** (비상 종 World 버튼 클릭은 UIRayCamera로 해결함)

### Script Execution Order
ViewController 100 → CCTV 200 → Panel 300. (GameManager는 기본 순서지만 `Instance`를 Awake에서 세팅하므로 다른 스크립트 Start에서 안전하게 읽힘)

---

## 3. 귀신 로스터 & 위협 매트릭스 (7마리)

> ⚠️ **이름 스왑 이력:** 과거 문서에서 윤진↔현우의 역할 이름표가 뒤바뀌어, 3개 문서에 sed 스왑 적용함. **현재 확정:**
> - **현우 = 환풍구/셔터/3스택 정전** (코드 Hyunwoo.cs)
> - **윤진 = 창고/Ballora 청각 스텔스/창고런** (코드 없음)
> 준영·연호·현승·김욱·윤석은 이름 안 바뀜. 프로젝트 문서는 스왑본으로 교체 완료(유저 확인).

| 귀신 | 위치 | 대응 | 결과 | 등장 | 엔진 |
|---|---|---|---|---|---|
| **현승** | 창문 | 육안 응시 | 비즉사(오디오 고장)→방치 시 **즉사**(퍼펫). 1일차는 고장 없음 | 1일~ | B |
| **현우** | 환풍구 | 셔터+라이트 | 비즉사(스택)→3스택 **정전** | 2일~ | B |
| **연호** | 창문(정면) | **종**(CCTV) / 최후엔 비상 종 | **즉사** | 2일 맛보기/3일 정식/5일 AI16 | **A** |
| **김욱** | 환풍구 CAM | 카메라에서 벗어나기 | 비즉사(환기 오류) | 3일~ | - |
| **준영** | CAM(관리실 방치) | 카메라 켜기(김욱 반대) | 비즉사(환기 오류), 밤 1회 | 3일~ | - |
| **윤진** | 창고 | 청각 회피(Ballora) | 즉사(창고런) | 3일~ | - |
| **윤석** | 전기실 | 스파크·달래기 | 즉사(창고런) | 4일~ | - |

### ⭐ 겹침의 진짜 축 = CCTV ON/OFF 상충 (위치가 아니라)
- **켜야:** 연호 종, 준영 없애기(그 CAM 봐야)
- **꺼야:** 김욱 안전(보면 위험), 현승 창문 육안 응시, (CCTV 과사용 = 카메라 에러 확률)
- 이게 매 순간 "CCTV 켤까 말까" 판단 = 겹침의 핵심 저글링.
- **창고런(윤진·윤석)은 정전 freeze라 다른 위협과 격리** → 동시 위협 실질 5마리.
- **즉사는 연호 하나(5마리 중)** → 우선순위 단순(연호 최우선). "연호만 확실히 막으면 나머진 좀 당해도 생존."
- 5일 = 전원 풀가동(제한 안 함). 후반 카오스는 "연호 우선 + 나머지 관리"로 풀림 = 공정.

### 준영 vs 김욱 = CCTV 정반대 (동시 등장 시 딜레마)
준영=CAM 켜야 사라짐 / 김욱=CAM 벗어나야 안전. 둘 다 3일~. 동시 빈도는 AI 곡선으로 조절해 억울하지 않게.

---

## 4. 완성된 시스템 & 코드 (파일별)

> 모든 코드는 씬의 해당 오브젝트에 부착. 아래는 각 스크립트의 역할·핵심 로직·주의점.
> ⭐ **밤 숫자 규칙:** 밤 숫자는 `GameManager.currentNight` 한 곳에만 있다. 다른 스크립트는 **자기 currentNight 필드를 만들지 말고 `GameManager.Night`를 읽을 것.** 테스트할 밤은 GameManager Inspector에서만 바꿈.

### 4.1 현승 (Hyunsoong.cs) — COMPLETE, 엔진B
- 루트 R1/R2/R4/R1'(로비 잠복) + **허공 등장 노드**(fixedNodeCount=3, fixedNodeWaits 배열 [허공10~30, 착석10~20, 기립3~7]). 노드0(허공)은 스태틱 X, index>0만.
- 루트 해금은 `GameManager.Night`: 1밤 R1 / 2밤~ +R2·R4 / 3밤~ +R1'.
- 엔진B(cycleTime마다 rand<aiLevel 전진) + 행동 굴림(85/10/5) + 창문 워크패스 + GazeBox.
- **STRIKE1**(비즉사, 오디오 고장 — 1일차는 점프스케어만) → **STRIKE2**(즉사, 퍼펫 — AudioSystem이 방치 32초 후 호출). STRIKE2에서 `GameManager.GameOver("현승퍼펫")` + `jumpscare.PlayGameOver()`.
- **armed 발동 = 둘 중 먼저 오는 쪽** (§14 원안 "자동만"을 유저가 변경, 구현노트 §15.10): ① 화면 전환 시 즉시(HandleScreenTransition 구독 유지) ② 안 해도 `armedAutoDelay` 5초 뒤 자동. CCTV/패널 보고 있었으면 jumpscare.Play()가 강제로 닫고, 그 CCTV 강제 다운이 화면 전환이라 연호 캐스케이드가 생김.
- 창고 노드 도착 시 "와장창" 로그 — 소리는 Phase 8.

### 4.2 현우 (Hyunwoo.cs) — COMPLETE 완전체, 엔진B + 판정 A + 모델/애니
- 환풍구 루트 노드 [허공, 공부방2 앉음, 환풍구 매달림, 화장실, 관리실 입구]. 스태틱은 index 1·2·3에서만(화장실=사라지는 순간).
- **셔터 판정 A** (상태 enum: Moving → AtEntrance → Armed):
  - 입구 도달 → `infiltrationTimer`(침투, 셔터 열린 동안 참, limit 15s) / `holdTimer`(유지, 셔터 닫힌 동안, holdRequired 5s).
  - 셔터 닫고 5초 유지 → **격퇴(Repel)** / 초당 1/10 조기 이탈 굴림.
  - 셔터 열면 holdTimer 리셋 + 침투 재개. 침투 만료 → **BecomeArmed**.
  - armed: `armedRecognizeDelay`(1s) 후 시야가 ventZone(2) 벗어나면 즉시 당함 / `armedGrace`(6s) 초과 자동 당함 → GetHit(스택+1).
  - **격퇴와 armed가 똑같이 보임**(쿵쿵+빈 환풍구) = 심리전.
- **stackCount** 현우 내부 카운터. 지금은 **스택 숫자 + 빨간 잔상만** — 스택별 효과(1 조명깜빡 / 2 조명나감 / 3 정전) 미구현. 3스택 → 정전 로그만.
- 2스택 효과는 원래 "CCTV 노이즈"였으나 카메라 에러와 겹쳐서 **"조명 나감"으로 변경됨**(구현노트 §12.2).
- **Animator PoseNode(Integer)**: 1=Sit, 2=Hanging, 4=InVent(크롤링 변형). AnyState 전환, MoveToNode에서 `SetInteger("PoseNode", index)`.
- 모델: Meshy AI 생성 → 텍스처(URP/Lit, Base Map) → Mixamo 크롤링 포즈 변형(InVent). 캡슐 제거, 모델에 스크립트 직접 부착. **로직/실루엣 통합**.
- 참조: monitor, shutter(isShutterClosed 읽기), viewController(currentZone), jumpscare, animator.

### 4.3 셔터 (ShutterController.cs) — COMPLETE
- 오른쪽 구역(ventZone=2) UI 버튼 토글(closedPos/openPos Lerp, `isShutterClosed` public).
- 라이트 홀드(EventTrigger PointerDown→LightOn / Up→LightOff, ventLight Spot, 배터리 무관).
- **ForceLock()**: 점프스케어 시 버튼 숨김 + 라이트 끔.
- **shutterDrainRate**: 닫힌 동안 `BatteryManager.DrainPerSecond` (기본 2는 높음 → 1 권장).

### 4.4 카메라·패널·전환 스크립트
- **CCTVController.cs**: FNAF식 토글(마우스 하단 재진입으로 on/off). 가운데 구역(allowedZone=1)에서만 켜짐. `isCameraDown` public. **"현재 방" 개념 없음** — 위치/회전만 관리. 복귀는 **homeLocalPos**(Start에서 1회 고정). ForceCancel/CancelReturn 상호 처리.
- **PanelController.cs**: 동일하게 homeLocalPos 방식. 왼쪽 구역 가면 자동으로 패널 열림.
  - `ClosePanel()`(Exit 버튼): **오디오든 카메라든 리셋 중이면 못 나감**(손 묶임).
  - `ResetAll()`(전체 리셋 버튼): 오디오+카메라+환기 동시, `resetAllDuration` 8초.
  - 참조: audioSystem, cameraSystem, ventSystem — 비어 있으면 Start에서 자동으로 찾음(연결 누락으로 잠금·고장 화면이 조용히 안 된 적 있어서 바꿈).
- **ViewController.cs**: 4구역, 구역별 microYaw/microPitch 배열화, SuppressEdgeUntilRelease. `currentZone` public.
- **ScreenTransitionDetector.cs**: CCTV 올림/내림 + 패널 열기/닫기 → `OnScreenTransition` 이벤트. `ForceTransition()`(환기 오류용). 시야 회전은 전환 아님.

### 4.5 MonitorDisplay.cs — COMPLETE (현재 방 관리 = 여기)
- 상태 enum: Off / Switching / Active. **currentRoom**(지금 보는 방 인덱스, roomFeeds 기준).
- `SwitchRoom(idx)`(버튼 호출), `GhostMoveStatic()`(귀신 이동 시 스태틱), buttonGroup(CanvasGroup, 방 전환 버튼 묶음).
- `RingBellCurrentRoom()` → `yeonho.RingBell(currentRoom)`. 종 버튼 OnClick → 이 함수.
- **CAM07(`storageRoomIndex`=6)은 Active여도 영상 대신 전용 화면** — 거의 검은 바탕 + 드문드문 먼지 점(`storageBase*`/`storageSpeck*`/`storageInterval`로 조절). 전환 스태틱·카메라 고장 스태틱(밝은 TV 노이즈)과 일부러 다르게 함(유저 요청).
- **카메라 고장(`cameraSystem.IsCameraBroken()`) 시 방·상태 무관 전부 스태틱.**

### 4.6 JumpscareOverlay.cs — COMPLETE
- Play()(빨강 STRIKE1, 1.5s 후 Stop) / PlayGameOver()(보라, 영구 잠금).
- 잠금 방식 = 각 컨트롤러 `enabled=false` 직접(방식 A). **cctv/panel/viewController/shutter 등록 완료**.

### 4.7 GameManager.cs — COMPLETE 기초
- static Instance(Awake). **`public static int Night`** — 모든 스크립트가 밤 숫자를 여기서 읽음(GameManager 없으면 1).
- `currentNight`(수동 — ⭐나중 저장 시스템/자동화 PENDING). nightDuration 390s / firstNightDuration 240s.
- gameHour/gameMinute 계산(12시→6시). 6시 → NightClear. GameOver(cause)(isGameOver 시 Update 정지 = 타이머·시계 멈춤).
- 예전엔 GameManager가 귀신 필드에 밤을 넣어주는 방식이었으나 Start 실행 순서 문제로 **제거**(구현노트 §15.8). 귀신 참조 필드도 없음.
- **주의:** 게임오버 시 귀신들은 안 멈춤(점프스케어 오버레이가 화면 덮어서 무관, 씬 전환 예정).

### 4.8 Clock_Digital.cs — COMPLETE
- 에셋 시계 개조. 현실시간 → **GameManager.gameHour/gameMinute**. URP 전용(_BaseMap/_EmissionMap만). 3D 오브젝트 시계(월드), 분도 표시.

### 4.9 BatteryManager.cs — COMPLETE
- static Instance. battery 100/maxBattery. **naturalDrain**(0.6은 너무 빠름, 0.1). Drain(즉시)/DrainPerSecond(지속). Deplete → **BatteryBlackout.Begin()**(씬에 없으면 자동으로 붙임). 여기서 GameOver 안 함 — 밤 타이머가 흘러야 6시 생존 가능.
- **배터리 방전 = 암전 시퀀스**(4.15). ≠ 현우 배선 고장(창고런).
- 장치별 소모율은 **각 장치가 배터리에 요청**(셔터 shutterDrainRate, 종 bellDrain, 비상 종 batteryCost).

### 4.10 BatteryDisplay.cs — COMPLETE
- **World Space Canvas** 텍스트, 모니터에 부착. `cctv.isCameraDown`일 때만 표시.
- ⭐ **UI 철학 (유저가 스스로 도출):**
  - **World Space** = 3D 오브젝트에 붙은 정보/장치(배터리, CCTV 방 전환 버튼, 비상 종).
  - **Screen Overlay** = 플레이어 조작(셔터·라이트·패널 버튼).

### 4.11 ⭐ 연호 (Yeonho.cs) — 엔진A, 메인 빌런 (이 프로젝트의 핵심)

**엔진A = FNAF3 스프링트랩 로직 (웹검색으로 원본 정확히 확인).**
```
매초: move_counter += 1 (흉포면 2)
매초 임계값 = (10 - AI) - (흉포?1) + Random(1~15) - total_turns
move_counter > 임계값 → 행동 굴림
  굴림: 1=멈춤(total_turns+1) / 2=후퇴 / 3=전진 / 4=급습(원본 vent, 우리는 흉포 전용)
  이동 성공(2,3) → total_turns=0, move_counter 리셋
```
- **핵심:** Random(1~15)이 매초 임계값에 들어가 **예측 불가**. total_turns = 멈출수록 다음 행동 가속.
- **거점 = 공부방2**. 환풍구 못 씀(몸집). 1단계는 L1만: 노드 = [공부방2, 가로복도, 로비, 세로복도]. **nodeRoomIndex = [1,2,4,5]**.
- **등장:** 시간 로직(spawnDelay 10~25)으로 model SetActive 껐다 켜기. 엔진A와 완전 분리.
- **행동 가중치:** weightForward 60 / Retreat 20 / Stay 20.

**대치 단계 (상태 enum: Spawning → Moving → WindowFront → Peeking):**
- Moving(엔진A 방황) → 세로복도 끝에서 전진 → **WindowFront**(창문앞, 랜덤 후퇴 없음) → **Peeking**(빼꼼).
- **WindowFront → Peeking 조건 두 가지:** ① 대기 시간(`windowFrontTime`, 씬 값 **약 12초** — 유저가 6초는 너무 짧다고 늘림) 경과 ② **창문앞에서 화면 전환**(즉사 아님, 빼꼼으로 넘어감).
- **즉사는 빼꼼에서만:** 빼꼼에서 화면 전환 → Kill(). 가만있으면 당장 안 죽지만 환기 오류/현승이 화면 전환을 강제 → 그 틈에 즉사(기획서 캐스케이드).
- **연호가 창문앞에 와도 CCTV 강제 다운 없음** — "앞에 있는지 몰라야 짜릿하다"(유저).

**일반 종 (RingBell) — COMPLETE:**
- `RingBell(room)`: room = MonitorDisplay.currentRoom.
  - 오디오 고장 중이면 못 침. 쿨(bellCooldown 5.4s) 중 무시. 쿨·배터리(bellDrain 8)는 **헛방이어도 소모.**
  - `AudioSystem.TryUseBell()`로 밤당 횟수 소모 — 초과면 오디오 고장 + 이번 종은 안 먹힘.
  - **Moving:** CCTV로 **연호가 지금 있는 방**을 보며 쳐야 함(`nodeRoomIndex[currentNode] == room`). 무시 1/7. 다른 방 = 헛방.
  - **WindowFront:** **세로복도(마지막 노드의 방)**에 친 종만 먹힘 — 무시 없음, 2칸 후퇴. 다른 방 = 헛방. 플레이어는 연호가 창문앞인지 CCTV로 못 보니 **감으로 치는 종**.
  - **Peeking:** 효과 없음.
- ⚠️ 구현노트 §11의 "인접(뒤쪽) 방" 규칙은 **옛 규칙**(웹에서 바뀜, 문서 미반영이었음). 현행은 위와 같음 — §11 상단에 주석 달아둠.
- **종 반응 딜레이:** 종소리(`bellSoundDuration` 1.0) + 랜덤 텀(0.2~0.6) 후 후퇴. 반응 중 엔진 멈칫. DoRetreat에서 moveCounter/secondTimer 리셋(재전진 방지 — 완료).

**비상 종 (EmergencyBell.cs) — COMPLETE (2026-11-24 재정의, 구현노트 §15.6):**
- **역할:** 일반 종으로 못 막았을 때 쓰는 **진짜 최후 수단.** 창문 옆 벽 World 버튼(가운데 구역 볼 때만, UIRayCamera로 클릭 판정).
- **효과는 연호가 창문앞일 때만** → `Yeonho.EmergencyPush()` 완전 후퇴(Spawning으로, 재등장 대기). 등장 전·이동 중·빼꼼 = 헛방(false).
- 누르면 효과와 무관하게 **횟수·쿨·배터리 소모.**
- **하루 2번(usesPerNight), 쿨 60초.** (씬 값도 2/60으로 맞춰둠)
- **오디오 시스템과 별개:** 오디오 고장 중에도 작동, 소진해도 오디오 고장 안 남(그냥 안 눌림), 리셋으로 안 채워짐.

### 4.12 AudioSystem.cs — COMPLETE (오디오 고장 + 리셋 + 종 횟수)
- **고장 원인:** 현승 STRIKE1, 일반 종 밤당 횟수 초과. (비상 종은 무관)
- **1일차는 고장 자체가 없음**(`noBreakUntilNight`=1) → 1일차 STRIKE1은 점프스케어만, STRIKE2 불가.
- **방치 32초(`breakTimeLimit`) → 현승 STRIKE2 퍼펫 즉사.** 원인 무관(종 과사용 고장도). 의도됨 — FNAF2 퍼펫 느낌.
- **종 밤당 한도 `bellLimitByNight` = {0, 0, 5, 4, 3, 2}:** 인덱스 = 밤 숫자 그대로([0]은 안 쓰는 칸). **0 이하 = 무제한** → 1일 무제한 / 2일5 / 3일4 / 4일3 / 5일2. Inspector에선 Element 0부터 보여서 헷갈림 주의.
- **리셋:** `StartReset()`(버튼용, 5초) / `StartResetFor(초)`(전체 리셋용). **고장 없어도 예방적으로 가능**, 완료 시 `bellUses = 0`(예방적 리셋 전략, 구현노트 §8.3).
- 고장 알림이 아직 없음(로그뿐) → TODO(사이렌 검토).

### 4.13 CameraSystem.cs — COMPLETE (카메라 에러, 구현노트 §12·§15.2)
- CCTV 켜져 있는 동안만 **`rollInterval` 3.5초마다 `breakChance` 3%** 굴림 → 고장 시 CCTV 완전 스태틱. 끄면 주기 누적 초기화.
- 고장 중에도 종은 사용 가능(원작대로).
- 리셋: AudioSystem과 같은 구조(`StartReset()` / `StartResetFor(초)`, 예방적 가능).
- 참조: cctv. MonitorDisplay·PanelController가 cameraSystem을 참조함(비어 있으면 Start에서 자동으로 찾음).

### 4.14 VentilationSystem.cs — COMPLETE 1차 (환기 에러, 구현노트 §15.9)
- **원인(지금):** 셔터 공기 게이지 — 닫고 5초(`closedGrace`)까지 공짜, 이후 초당 +1, 20(`airLimit`)이면 고장. 열면 초당 -1. 닫힌 시간도 열면 연 만큼만 줄어듦(깜빡 꼼수 방지).
- **원인(나중):** 김욱·준영 → `BreakVent(이유)`.
- **효과:** 셔터 강제 개방 + 못 닫음(`ShutterController.SetForcedOpen`) / 8초 후부터 12초마다 흐려짐 → 가장 어두운 순간 `ForceTransition()` (연호·armed 현승 둘 다 반응).
- 흐려짐 검은 화면은 `blurOverlay` 비어 있으면 자동 생성(Overlay Canvas, sortingOrder 100 — 점프스케어를 잠깐 가릴 수 있음, Phase 8에서 정리).
- 리셋: `StartReset()` / `StartResetFor(초)`, 예방적 가능(게이지 초기화). 완료 시 셔터 잠금만 해제.
- 참조(shutter, transitionDetector)는 비어 있으면 자동으로 찾음.

### 4.15 BatteryBlackout.cs — COMPLETE 1차 (배터리 방전 암전, 구현노트 §15.11)
- ⚠️ **"방전" 전용.** 현우 3스택 "배선 고장"(기획서가 "정전"이라 부르는 것, 창고런으로 수리 가능)과 완전히 별개. 이름도 그래서 Outage(정전)가 아니라 Blackout(방전 암전).
- 흐름: **암전(조용한 시간 10~24초 랜덤)** → **오르골 8초**(창문앞에 연호 + 오르골 + 화면 깜빡) → **완전 어둠 3초** → 연호 점프스케어 → GameOver("배터리 방전 — 연호"). 오르골 이후는 고정 길이(유저 결정).
- **6시가 먼저 오면 생존**(`GameManager.isNightOver` 체크). 밤 타이머는 계속 흐르고 시계만 꺼짐.
- **암전 중 시야 회전만 가능.** 잠그는 방식:
  - 입력 잠금 플래그(`inputLocked`): CCTVController, PanelController, ShutterController, EmergencyBell — **컴포넌트를 끄지 않는 이유:** CCTV·패널을 보던 중이면 카메라 복귀 이동을 그 컴포넌트 Update가 해야 해서.
  - 컴포넌트 끄기(enabled=false): 현승·현우·연호, AudioSystem·CameraSystem·VentilationSystem, ScreenTransitionDetector(**먼저 끔** — 안 그러면 CCTV 강제 다운이 화면 전환으로 잡혀 연호 즉사 등 발생).
  - `PowerOff()`: MonitorDisplay(검은 화면), Clock_Digital(숫자 검게), ShutterController(열림+불빛 끔).
  - 패널은 리셋 중이어도 `ForceCloseIgnoringReset()`으로 닫음.
- 연호는 AI 꺼진 채 `AppearAtWindowForBlackout()`로 모습만 드러냄.
- 화면: 검은 덮개(`ScreenOverlayFactory`, sortingOrder 110) — 점프스케어 직전에 치움.
- 오르골 소리: `musicBoxClip` 비우면 코드가 임시 멜로디 생성. 진짜 소리는 Phase 8.

---

## 5. ⚠️ 함정 / 하지 말 것 (실제로 겪은 것들)

- **버튼 OnClick용 함수에 매개변수(기본값 포함)를 붙이지 말 것.** `StartReset()`을 `StartReset(float x = -1f)`로 바꿨더니 Unity 버튼이 함수를 못 찾아 아무 반응이 없었음(C# 기본값 매개변수는 Unity엔 그냥 "float 받는 함수"). 시간 지정이 필요하면 `StartResetFor(float)`처럼 **별도 함수**로.
- **새 참조 필드를 추가하면 씬에서 연결해야 함.** 안 하면 null이라 `if (x != null && ...)` 가드가 조용히 통과 → 기능이 그냥 안 되는 것처럼 보임. 씬 YAML에서 필드 줄이 있는지 확인.
- **씬에 저장된 Inspector 값이 코드 기본값보다 우선.** 코드 기본값을 바꿔도 이미 씬에 있는 컴포넌트엔 반영 안 됨 → 유저에게 Inspector 수정 요청하거나 씬 값 확인.
- **밤 숫자 필드를 스크립트마다 만들지 말 것.** `GameManager.Night`만 읽기(4번 머리말 참고).
- **Debug.Log 폭주 = 렉.** 매 틱 로그 빼고 ★ 상태 전환 로그만.
- **카메라 복귀 버그(해결됨):** 어중간한 위치를 home으로 저장하면 이상한 데로 복귀. → homeLocalPos를 Start에서 1회 고정.
- **World Space Canvas 스케일:** 처음 만들면 거대함. Scale 0.001부터.
- **World UI 클릭 안 될 때:** GraphicRaycaster IgnoreReversedGraphics, 전체화면 RawImage의 Raycast Target, 저해상도 RT(4:3)와 화면(16:9) 종횡비 불일치 — UIRayCamera로 해결함(커밋 9f84e4e).
- **URP 셰이더:** 외부 모델이 분홍/하양이면 URP/Lit로 바꾸고 Base Map 연결.
- **엔진A 5개 임계값은 문서 오해였음.** 원본은 임계값 하나 + Random(1~15).
- **테스트값 되돌리기:** aiLevel↑, naturalDrain↑, spawnDelay↓, bellLimitByNight 등으로 테스트함. 커밋 전 원래값 복구. 단 유저가 플레이해보고 의도적으로 바꾼 값(예: windowFrontTime 12초)은 되돌리지 말 것 — 애매하면 물어보기.
- **`.gitignore`:** `.DS_Store` 무시 추가함. `.claude/`(로컬 설정)는 커밋 안 함.

---

## 6. 남은 작업 (우선순위 순)

### 바로 할 수 있는 것
1. ~~배터리 방전 → 연호 점프스케어~~ → 완료(4.15 방전 암전 시퀀스).
2. ~~현승 §14 자동 발동~~ → 완료(화면 전환 즉시 OR 5초 자동, 구현노트 §15.10). 알려진 모서리: 패널 리셋 중 STRIKE1이면 패널이 안 닫힌 채 점프스케어 종료 — 테스트 때 확인.
3. **고장 알림 설계 (오디오/카메라)** — 지금 로그뿐이라 플레이어가 고장을 모름(오디오는 32초 안에 못 고치면 죽는데!). 유저 아이디어: **FNAF3처럼 오디오 고장 시 경비실에 사이렌/경고.** 사운드+시각 경고 방식 설계 필요. 최소한 패널 임시 상태 표시("AUDIO: OK/ERROR/RESETTING")도 후보.

### 밸런스
4. **밤 곡선 잡기** (유저가 시작 신호 줄 것) — 연호 aiLevel(2일1→5일16), windowFrontTime(약 12s 확인), 종 한도, 비상 종(2회/60s), 현승 fixedNodeWaits, 카메라 에러 확률 등.

### 귀신 마무리
5. **현우 스택별 효과** — 1 조명깜빡+시야흐림 / 2 조명 나감(불빛 버튼 확인 방해) / 3 정전.
6. **현승 R3(환풍구)**, **STRIKE1 캐스케이드**(행동상세 §1.4: STRIKE1 시 ~10초 CCTV/패널 잠금 + 연호 빠른 전진).
7. **연호 5단계 흉포 토글** — 현승·김욱·준영에게 당함/정전/4~5시/관리실 10초+ 방치 → move_counter 2배 + 급습(굴림4). 매 15초 rand(0~4)<AI 자동. **다른 귀신 구현 후.**

### 시스템 (등뼈)
8. **정전 / 창고런** — 현우 3스택 → 배선 고장 → 창고런 5박자([결정]→[창고 기어가기 윤진]→[전기실 브레이커 윤석]→[탈출]→[복귀]). freeze 3단계. 5시 이후 창고런 없음.
9. ~~환기 에러~~ → 1차 완료(4.14). 남은 것: 김욱·준영 원인 연결, 연호 흉포 연결.

### 남은 귀신 (4마리)
10. **김욱**(카메라 벗어나야 안전, 환기 오류), **준영**(창문 왼쪽 밖 실루엣, CCTV 켜면 사라짐, 무시하면 armed → 정면 복귀 시 점프 → 환기 오류, 구현노트 §13), **윤진**(창고 청각/창고런), **윤석**(전기실 브레이커).

### 밤 진행 / 저장
11. **currentNight 자동화 + 저장(PlayerPrefs)** — 유저: "기능 다 만들고 마지막에."

### Phase 8 (그래픽·사운드 — 전부 맨 나중)
12. **환기 흐려짐 연출 교체** — 지금은 `VentilationSystem`이 검은 화면을 자동 생성(`blurOverlay` 칸은 **일부러 비워둔 것** — 유저가 칸을 숨기지 말고 남겨두라고 함). Phase 8에서: 직접 만든 연출(Canvas + CanvasGroup — 노이즈/비네트 등 여러 장 얹어도 alpha 하나로 같이 페이드)을 그 칸에 연결 / 이름 blur → fade 정리(실제론 어두워짐) / sortingOrder 100이라 점프스케어를 잠깐 가리는 문제 정리.
13. **방전 암전 연출 교체** — 진짜 오르골 소리(`BatteryBlackout.musicBoxClip`, 창문 쪽 3D 소리로), 덮개 깜빡임 대신 진짜 조명 끄기·창문 쪽 불빛 깜빡임, 연호 방전 암전 전용 점프스케어 애니.
14. 조명·저해상도 필터·3D 점프스케어 애니(죽음마다 고유 애니·사운드·카메라워크)·종소리/비상 종소리/거부음·CAM07 창고 소리(현승 와장창 등)·환풍구 앰비언스·ProBuilder 맵·에러 패널 UI(FNAF3 시스템 리셋 메뉴, 에러 항목 빨간 점멸)·리셋 진행 바·JumpscareManager(즉사>비즉사 우선순위)·개발 로그 정리.

---

## 7. 문서 위치 (`Assets/Mds/`)
- 기획서: `독서실_경비_기획서_이름스왑.md` (스왑본)
- 귀신 행동상세: `귀신_행동상세_이름스왑.md`
- 구현노트: `구현_노트_이름스왑.md` (§8~14: 리셋 패널, 점프스케어, 맵 방식, 연호 종, 카메라 에러, 준영 재설계, 현승 자동 트리거 / **§15: 2026-11-24 결정 정리**)
- **기획/귀신 관련 판단은 반드시 이 문서들 먼저 확인 후.** 문서 간 충돌 시 더 최근 결정(구현노트 > 기획서, §15 > 이전 절)이 우선이지만, 애매하면 유저에게 확인.
- 비상 종은 기획서에 없고 구현노트 §15.6에만 있음.

---

## 8. 한 줄 요약 (스타일)
한국어로, 따뜻하고 격려하며, 설계 규율과 좋은 발견을 칭찬하되, **대등한 협업자로서 비판적으로 함께 사고**한다. 알고리즘을 말로 먼저 확인 → 유저 이해 확인 → 한글 주석 코드(작게 쪼개거나 통파일 교체). 클릭 단위 Unity 안내. 문서 없이 추측 금지. 메커니즘 먼저, 그래픽은 Phase 8. 한 일은 확인한 것만 했다고 말한다.

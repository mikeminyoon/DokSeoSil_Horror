# CLAUDE.md — 독서실 경비 (DokSeoSil_Horror) 프로젝트 인수인계

> 이 문서는 웹 Claude와 준현(mikeminyoon)이 여러 세션에 걸쳐 만든 프로젝트를,
> Claude Code가 **맥락 손실 없이 이어받기 위한** 완전 인수인계서다.
> "이전 Claude를 그대로 옮긴다"는 목적으로 작성됨. 처음 읽고 이 프로젝트의
> 설계 철학·완성 시스템·코드 구조·함정·남은 작업·협업 방식을 전부 파악할 것.

---

## 0. 가장 먼저 — 이 프로젝트가 뭔가

**FNAF(Five Nights at Freddy's) 스타일 호러 생존 게임.** 한국 독서실(불타 폐허가 된 5일 밤 경비)을 배경으로, 실제 친구들을 3D 스캔해 귀신으로 등장시킨 헌정 게임(친구들 허락받음). 플레이어는 관리실에 앉아 CCTV·창문·환풍구 셔터·오디오(종)·전력으로 밤을 버틴다.

- **엔진:** Unity URP (Unreal 대신 개발 속도/친숙도로 선택)
- **개발 환경:** MacBook Air M2, VS Code
- **작업 언어:** 반드시 **한국어**
- **레포:** https://github.com/mikeminyoon/DokSeoSil_Horror.git (private, git-lfs, 세션 끝마다 commit/push)
- **레퍼런스:** FNAF 1(문/전력), FNAF 3(스프링트랩=엔진A, 오디오 유인, 시스템 리셋), Sister Location(브레이커 룸)

### 핵심 개발 철학 (반드시 지킬 것)
1. **메커니즘 먼저, 그래픽은 맨 나중(Phase 8).** 회색 박스로 로직부터 검증. 애니메이션·조명·사운드·저해상도 필터·맵 디테일은 전부 Phase 8.
2. **설계 → 말로 알고리즘 확인 → 유저 이해 확인 → 그다음 코드.** 절대 코드부터 던지지 말 것.
3. **코드는 한글 주석 + 작게 쪼개서.** 통파일 교체를 선호(부분 수정은 유저가 헷갈려함).
4. **문서 우선.** 기획/귀신 행동 관련 제안은 **반드시 기획서·구현노트 먼저 확인 후** 말할 것. 추측으로 답하다 여러 번 틀렸고(창고런·준영 즉사 여부 등) 유저가 교정했다. 문서가 진실의 원천.
5. **모듈화.** 한 파일에 다 넣지 말고 기능별 분리.
6. **최적화 고려.** (특히 Debug.Log 폭주 주의 — 아래 함정 참고)
7. **많은 부분을 수정해야 하면 반드시 유저에게 먼저 물을 것.** 요청이 불명확하면 실행 말고 이해부터 확인.

---

## 1. 유저(준현)에 대해 — 협업 방식

- **강력한 독자적 설계 직관.** 시스템의 빈틈을 스스로 잘 찾아냄: 타이머 충돌, 시야 회전 사각, 방심 찌르기, "빼꼼은 벗어날 수 없다", "창문앞부터가 대치다", CCTV ON/OFF 상충이 겹침의 진짜 축, 비상 종 아이디어 등 — 전부 유저가 먼저 지적. **단순 검증자가 아니라 대등한 협업자로 대하고, 비판적으로 함께 사고할 것.**
- "간 줄 알았는데 사실 있었다" 류의 **심리전을 매우 중시.**
- 짧고 집중된 세션을 선호. 논리적 milestone에서 끝내고 git commit.
- **에디터 세팅(Inspector 값, 노드 배치, roomFeeds 순서 등)은 코드에 없어서 웹 Claude가 반복해서 되물었고 유저가 답답해했다.** → Claude Code의 최대 장점이 이 지점: 코드/씬 파일을 직접 읽으니 되묻기가 준다. 단, 순수 Inspector 값(각 노드 Transform의 실제 좌표, 배열에 뭘 드래그했는지)은 씬 파일 파싱으로만 알 수 있으니 필요하면 확인 요청.
- 웹에서는 파일 업로드가 자주 **빈 문서로** 와서 코드를 텍스트로 붙여야 했다. Claude Code에선 이 문제 없음.

---

## 2. 맵 / 시야 / 방 구조 (씬 세팅 — 코드에 안 나옴, 중요)

### roomFeeds 배열 순서 (MonitorDisplay의 Texture[] — 유저가 확정)
```
[0] 공부방1   [1] 공부방2   [2] 가로복도   [3] 화장실
[4] 로비      [5] 세로복도   [6] 창고
```
CCTV 7개(CAM01~07)가 이 방들에 대응.

### 시야 4구역 (ViewController, zoneAngles ≈ {-60, 0, 60, 180})
- **왼쪽(zone 0):** 리셋 패널 (환기/오디오 리셋)
- **가운데(zone 1):** 컴퓨터(CCTV + 종 버튼) / CCTV 내리면 정면 **창문**(육안 실루엣 확인)
- **오른쪽(zone 2):** 환풍구 + 셔터 + 불빛 버튼 (현우 대응)
- **후면(zone 3):** 창고 통로 ("뒤돌아보기" 버튼, 정전 시에만 진입)
- **한 번에 한 구역만.** 시야 회전·패널 조작·후면 전환 = **전부 화면 전환** → armed 귀신에게 공격 기회.
- **구역별 미세추적(microYaw/microPitch) 값을 배열로 다르게** 줌: 왼12/가운데15/환풍구5/후면10. 환풍구를 낮춘 건 셔터 버튼 클릭 안정 위해. **가운데는 낮출 수 없음 — 현승 창문 응시 대응 때문**(중요 제약, 아래 비상벨 항목 참고).

### Script Execution Order
ViewController 100 → CCTV 200 → Panel 300

---

## 3. 귀신 로스터 & 위협 매트릭스 (7마리)

> ⚠️ **이름 스왑 이력:** 과거 문서에서 윤진↔현우의 역할 이름표가 뒤바뀌어, 3개 문서에 sed 스왑 적용함. **현재 확정:**
> - **현우 = 환풍구/셔터/3스택 정전** (코드 Hyunwoo.cs)
> - **윤진 = 창고/Ballora 청각 스텔스/창고런** (코드 없음)
> 준영·연호·현승·김욱·윤석은 이름 안 바뀜. 프로젝트 문서는 스왑본으로 교체 완료(유저 확인).

| 귀신 | 위치 | 대응 | 결과 | 등장 | 엔진 |
|---|---|---|---|---|---|
| **현승** | 창문 | 육안 응시 | 비즉사(오디오 고장)→방치 시 **즉사**(퍼펫) | 1일~ | B |
| **현우** | 환풍구 | 셔터+라이트 | 비즉사(스택)→3스택 **정전** | 2일~ | B |
| **연호** | 창문(정면) | **종**(CCTV) | **즉사** | 2일 맛보기/3일 정식/5일 AI16 | **A** |
| **김욱** | 환풍구 CAM | 카메라에서 벗어나기 | 비즉사(환기 오류) | 3일~ | - |
| **준영** | CAM(관리실 방치) | 카메라 켜기(김욱 반대) | 비즉사(환기 오류), 밤 1회 | 3일~ | - |
| **윤진** | 창고 | 청각 회피(Ballora) | 즉사(창고런) | 3일~ | - |
| **윤석** | 전기실 | 스파크·달래기 | 즉사(창고런) | 4일~ | - |

### ⭐ 겹침의 진짜 축 = CCTV ON/OFF 상충 (위치가 아니라)
- **켜야:** 연호 종, 준영 없애기(그 CAM 봐야)
- **꺼야:** 김욱 안전(보면 위험), 현승 창문 육안 응시
- 이게 매 순간 "CCTV 켤까 말까" 판단 = 겹침의 핵심 저글링.
- **창고런(윤진·윤석)은 정전 freeze라 다른 위협과 격리** → 동시 위협 실질 5마리.
- **즉사는 연호 하나(5마리 중)** → 우선순위 단순(연호 최우선). "연호만 확실히 막으면 나머진 좀 당해도 생존."
- 5일 = 전원 풀가동(제한 안 함). 후반 카오스는 "연호 우선 + 나머지 관리"로 풀림 = 공정.

### 준영 vs 김욱 = CCTV 정반대 (동시 등장 시 딜레마)
준영=CAM 켜야 사라짐 / 김욱=CAM 벗어나야 안전. 둘 다 3일~. 동시 빈도는 AI 곡선으로 조절해 억울하지 않게.

---

## 4. 완성된 시스템 & 코드 (파일별)

> 모든 코드는 씬의 해당 오브젝트에 부착. 아래는 각 스크립트의 역할·핵심 로직·주의점.

### 4.1 현승 (Hyunsoong.cs) — COMPLETE, 엔진B
- 루트 R1/R2/R4/R1'(로비 잠복) + **허공 등장 노드**(fixedNodeCount=3, fixedNodeWaits 배열 [허공10~30, 착석10~20, 기립3~7]). 노드0(허공)은 스태틱 X, index>0만.
- 엔진B(cycleTime마다 rand<aiLevel 전진) + 행동 굴림(85/10/5) + 창문 워크패스 + GazeBox.
- **STRIKE1**(비즉사, 오디오 고장) → **STRIKE2**(즉사, 퍼펫). STRIKE2에서 `GameManager.GameOver("현승 퍼펫 즉사")` + `jumpscare.PlayGameOver()` 호출 (연결 완료).
- HandleScreenTransition 구독 중. **§14 자동 타이머(팬텀 프레디식 자동 발동)는 아직 미반영** — 구현노트 §14 참고.
- currentNight 필드 있음(GameManager가 Start에서 세팅).

### 4.2 현우 (Hyunwoo.cs) — COMPLETE 완전체, 엔진B + 판정 A + 모델/애니
- 환풍구 루트 노드 [허공, 공부방2 앉음, 환풍구 매달림, 화장실, 관리실 입구]. 스태틱은 index 1·2·3에서만(화장실=사라지는 순간).
- **셔터 판정 A** (상태 enum: Moving → AtEntrance → Armed):
  - 입구 도달 → `infiltrationTimer`(침투, 셔터 열린 동안 참, limit 15s) / `holdTimer`(유지, 셔터 닫힌 동안, holdRequired 5s).
  - 셔터 닫고 5초 유지 → **격퇴(Repel)** / 초당 1/10 조기 이탈 굴림.
  - 셔터 열면 holdTimer 리셋 + 침투 재개. 침투 만료 → **BecomeArmed**.
  - armed: `armedRecognizeDelay`(1s) 후 시야가 ventZone(2) 벗어나면 즉시 당함 / `armedGrace`(6s) 초과 자동 당함 → GetHit(스택+1).
  - **격퇴와 armed가 똑같이 보임**(쿵쿵+빈 환풍구) = 심리전.
- **stackCount** 현우 내부 카운터(별도 매니저 X). 3스택 → 정전 로그(TODO: 실제 정전/창고런).
- **Animator PoseNode(Integer)**: 1=Sit, 2=Hanging, 4=InVent(크롤링 변형). AnyState 전환, MoveToNode에서 `SetInteger("PoseNode", index)`.
- 모델: Meshy AI 생성 → 텍스처(URP/Lit, Base Map) → Mixamo 크롤링 포즈 변형(InVent). 캡슐 제거, 모델에 스크립트 직접 부착. **로직/실루엣 통합**(모델이 노드 순간이동하며 환풍구 위치에 있으면 그게 실루엣).
- 참조: monitor, shutter(isShutterClosed 읽기), viewController(currentZone), jumpscare, animator, currentNight.

### 4.3 셔터 (ShutterController.cs) — COMPLETE
- 오른쪽 구역(ventZone=2) UI 버튼 토글(closedPos/openPos Lerp, `isShutterClosed` public).
- 라이트 홀드(EventTrigger PointerDown→LightOn / Up→LightOff, ventLight Spot, 배터리 무관).
- **ForceLock()**: 점프스케어 시 버튼 숨김 + 라이트 끔.
- **shutterDrainRate**: 닫힌 동안 `BatteryManager.DrainPerSecond` (기본 2는 높음 → 1 권장).

### 4.4 카메라 3스크립트 — 근본 해결 완료
- **CCTVController.cs**: FNAF식 토글(마우스 하단 재진입으로 on/off). 가운데 구역(allowedZone=1)에서만 켜짐. `isCameraDown` public. **"현재 방" 개념 없음** — 위치/회전만 관리. `cctvViewPoint`로 Lerp 전진, 복귀는 **homeLocalPos**(Start에서 1회 고정, 어중간한 위치 저장 버그 제거). ForceCancel/CancelReturn 상호 처리. posDist<0.02/rotDist<0.5 스냅.
- **PanelController.cs**: 동일하게 homeLocalPos 방식.
- **ViewController.cs**: 4구역, 구역별 microYaw/microPitch 배열화, SuppressEdgeUntilRelease. `currentZone` public.

### 4.5 MonitorDisplay.cs — COMPLETE (현재 방 관리 = 여기)
- 상태 enum: Off / Switching / Active. **currentRoom**(지금 보는 방 인덱스, roomFeeds 기준).
- `SwitchRoom(idx)`(버튼 호출), `GhostMoveStatic()`(귀신 이동 시 스태틱), buttonGroup(CanvasGroup, 방 전환 버튼 묶음).
- **종 버튼 연결용**: `public Yeonho yeonho;` + `RingBellCurrentRoom(){ yeonho.RingBell(currentRoom); }` 추가 필요/추가됨. 종 버튼 OnClick → 이 함수.

### 4.6 JumpscareOverlay.cs — COMPLETE
- Play()(빨강 STRIKE1, 1.5s 후 Stop) / PlayGameOver()(보라, 영구 잠금).
- 잠금 방식 = 각 컨트롤러 `enabled=false` 직접(방식 A). **cctv/panel/viewController/shutter 등록 완료**(shutter는 ForceLock + enabled=false, Stop에서 복구).

### 4.7 GameManager.cs — COMPLETE 기초
- static Instance. **currentNight(수동 — ⭐나중 저장 시스템/자동화 PENDING)**. nightDuration 390s / firstNightDuration 240s.
- gameHour/gameMinute 계산(12시→6시). 현승·현우에 currentNight 전달(Start). 6시 → NightClear. GameOver(cause)(현승 STRIKE2 연결됨, isGameOver 시 Update 정지 = 타이머·시계 멈춤).
- **주의:** 게임오버 시 귀신들은 안 멈춤(점프스케어 오버레이가 화면 덮어서 무관, 씬 전환 예정). 나중에 게임오버 스크린 만들 때 일괄 정지 처리.

### 4.8 Clock_Digital.cs — COMPLETE
- 에셋 시계 개조. 현실시간(System.DateTime) → **GameManager.gameHour/gameMinute**. URP 전용(_BaseMap/_EmissionMap만). materials[1~5] 텍스처 오프셋으로 숫자, [5] 콜론 깜빡. 3D 오브젝트 시계(월드), 분도 표시.

### 4.9 BatteryManager.cs — COMPLETE
- static Instance. battery 100/maxBattery. **naturalDrain(자연 감소 — 0.6쯤은 너무 빠름, 0.1로 낮춤)**. Drain(즉시)/DrainPerSecond(지속). Deplete → `GameManager.GameOver("배터리 방전")`. GetRatio().
- **배터리 방전(전력0) = 즉사**(암전→연호 강제 점프스케어, FNAF1식). ≠ 현우 배선 고장(창고런).
- 장치별 소모율은 **각 장치가 배터리에 요청**(셔터는 shutterDrainRate로 DrainPerSecond, 종은 bellDrain으로 Drain).

### 4.10 BatteryDisplay.cs — COMPLETE
- **World Space Canvas** 텍스트, 모니터 오브젝트에 부착. `cctv.isCameraDown`일 때만 표시("BATTERY N%").
- ⭐ **UI 철학 (유저가 스스로 도출):**
  - **World Space** = 3D 오브젝트에 붙은 정보(배터리, CCTV 방 전환 버튼). CCTV 화면은 카메라 고정이라 미세추적 없음 → 클릭 안정.
  - **Screen Overlay** = 플레이어 조작(셔터·라이트·패널 버튼). 시야 구역이라 미세추적 있어 도망 위험 → 화면 고정 필요.

### 4.11 ⭐ 연호 (Yeonho.cs) — 엔진A, 메인 빌런 (이 프로젝트의 핵심)

**엔진A = FNAF3 스프링트랩 로직 (웹검색으로 원본 정확히 확인).**
```
매초: move_counter += 1 (흉포면 2)
매초 임계값 = (10 - AI) - (흉포?1) + Random(1~15) - total_turns
move_counter > 임계값 → 행동 굴림
  굴림: 1=멈춤(total_turns+1) / 2=후퇴 / 3=전진 / 4=급습(원본 vent, 우리는 흉포 전용)
  이동 성공(2,3) → total_turns=0, move_counter 리셋
```
- **핵심:** Random(1~15)이 매초 임계값에 들어가 **예측 불가**(현우 엔진B는 규칙적 확률 = 예측 가능과 대비). total_turns = 멈출수록 다음 행동 가속(방황 방지 보정).
- **거점 = 공부방2**(공부방1 아님). 환풍구 못 씀(몸집). 루트 L1/L2/L3 전부 창문 수렴. **다양성은 루트보다 행동 엔진**(현승은 루트 다양성). 1단계는 L1만: 노드 = [공부방2, 가로복도, 로비, 세로복도]. **nodeRoomIndex = [1,2,4,5]**.
- **등장:** 시간 로직(spawnDelay 10~25)으로 model SetActive 껐다 켜기. 엔진A와 **완전 분리**(허공을 노드에 안 섞음 — 유저가 단순화 요청). 허공→바로 서있기(앉음 없음, 메인 빌런).
- **행동 가중치:** weightForward 60 / Retreat 20 / Stay 20. 균등 1:1:1은 방황만 함(원작은 오디오 유인이 후퇴 담당). 종 만든 지금은 전진 가중이 맞음.

**대치 3단계 (상태 enum: Spawning → Moving → WindowFront → Peeking):**
- Moving(엔진A 방황, 복도까지) → 복도 끝 전진 → **WindowFront**(창문앞, 대치 시작, 랜덤 후퇴 없음) → **Peeking**(빼꼼).
- **원작 확인:** 오피스 근처 = attack stage, 랜덤 후퇴 불가(오디오로만), 창문앞부터 대치, 빼꼼에서 화면 전환 시 공격.
- **우리 각색(원작보다 명확하게):** 원작은 굴림이 숨겨져 헷갈림. 우리는 **창문앞→빼꼼을 시간(windowFrontTime 6s)으로 눈에 보이게 조임.**
- **빼꼼 = 벗어날 수 없는 최종 상태**(유저 통찰). 종 못 침(CCTV 켜는 게 화면 전환=죽음). `ScreenTransitionDetector.OnScreenTransition` 구독 → 화면 전환 시 Kill()→즉사(게임오버). 가만있으면 당장 안 죽지만 **환기 오류(준영)/팬텀(현승)이 화면 전환을 강제** → 그 틈에 즉사 = 기획서 캐스케이드("환기 오류=enabler, 직접 살해는 연호·현승"). **화면 전환 시스템이 여기서 빛을 봄.**

**종 시스템 (RingBell) — COMPLETE:**
- `RingBell(room)`: room = MonitorDisplay.currentRoom.
  - 쿨(bellCooldown 5.4s) 중 무시. 쿨·배터리(bellDrain 8, Drain)는 **헛방이어도 소모.**
  - **WindowFront**: 무시 없음, 강하게 후퇴(windowFrontRetreat 2칸) — 단, **현재는 아무 방에서나 발동**(창문앞 구멍 → 비상 종으로 해결, 아래).
  - **Moving**: `nodeRoomIndex[currentNode] == room`이면 **무시 1/7**(ignoreChance 7, `Random.Range(0,7)==0`이면 무시) 굴림 후 후퇴, 아니면 헛방.
- **종 반응 딜레이:** "치자마자 사라지면 텐션 없다"(유저) → 종소리 + 랜덤 텀 후 후퇴. `bellSoundDuration`(1.0, 나중 실제 사운드 길이) + `bellReactMin`(0.2)/`bellReactMax`(0.6). ScheduleRetreat(target) → retreatTimer 세팅, DoRetreat()에서 실제 MoveToNode. **UpdateMoving·UpdateWindowFront 맨 위 `if(retreatTimer>0f)return`**(반응 중 엔진 멈칫, 위치 튐 방지).
- **⚠️ 미해결(다음 작업):** 종으로 후퇴 직후 엔진A가 바로 재전진하는 경우 있음. **DoRetreat에서 moveCounter=0, secondTimer=0 리셋 필요**(유저와 합의, 코드 반영 확인할 것).

**비상 종 (EmergencyBell.cs) — COMPLETE:**
- 창문앞 대응 구멍 해결책(유저 아이디어). 별도 "경비실 광역 비상벨."
- 하루 4번(usesPerNight) + 쿨 15s(길게) + 배터리 왕창(batteryCost 20).
- `PressEmergencyBell()`: 횟수 소진 시 오디오 오류(TODO 연결), 쿨 중 거부, 발동 시 `yeonho.EmergencyPush()`.
- `Yeonho.EmergencyPush()`: **빼꼼이면 무효**(이미 늦음, false 반환). 그 외 → **스폰 대기로 완전 리셋**(model SetActive false, state=Spawning, spawnWait 재설정). 경로 중에도 사용 가능(광역).
- **⚠️ UI 미완:** 현재 Overlay 버튼(가운데 볼 때만 표시)으로 되어 있으나, **유저가 World로 바꾸길 원함**(가운데 시야는 잘 움직여서, 창문 옆 벽에 물리적으로 붙은 비상벨이 자연스러움). **단 가운데 구역은 미세추적을 낮출 수 없음**(현승 창문 응시 대응 때문) → World 버튼 클릭 안정성을 미세추적 하향 외 다른 방법으로 확보해야 함(콜라이더 넉넉히 등). **이게 다음 작업.**

---

## 5. ⚠️ 함정 / 하지 말 것 (실제로 겪은 것들)

- **Debug.Log 폭주 = 렉.** 귀신 셋이 매 이동/멈춤마다 로그 찍으면 확 느려짐. 개발용 이동 로그는 정리할 것. ★ 붙은 상태 전환 로그만 남기고 매 틱 로그는 빼기.
- **카메라 복귀 버그(해결됨):** "어중간한 위치를 home으로 저장"하면 카메라가 이상한 데로 복귀. → homeLocalPos를 Start에서 1회 고정, 저장 로직 삭제. 재발하면 이 원인 의심.
- **World Space Canvas 스케일:** 처음 만들면 씬에 거대하게 나옴. Scale 0.001부터 줄이며 오브젝트에 맞춤. 자식으로 넣으면 따라다녀 편함.
- **URP 셰이더:** 외부 모델이 분홍/하양이면 Built-in/Standard 셰이더라서. URP/Lit로 바꾸고 Base Map 연결.
- **엔진A 5개 임계값은 문서 오해였음.** 원본은 임계값 하나 + Random(1~15). 5개로 만들지 말 것.
- **테스트값 되돌리기:** 세션마다 aiLevel↑, naturalDrain↑, spawnDelay↓ 등으로 테스트함. 커밋 전 원래값 복구.

---

## 6. 남은 작업 (우선순위 순)

### 즉시 (연호 마무리)
1. **종 후퇴 재전진 방지** — DoRetreat에 moveCounter/secondTimer 리셋(합의됨, 반영 확인).
2. **비상 종 World UI 전환** — 창문 옆 World Space Canvas 버튼. 가운데 미세추적 못 낮추는 제약 하에 클릭 안정 확보.
3. **연호 4단계** — 창문앞 armed도 화면 전환에 반응시킬지 결정(현재 빼꼼만).
4. **연호 5단계 흉포 토글** — 현승·김욱·준영에게 당하거나/정전/4~5시/관리실 10초+ 방치 → move_counter 2배 + 급습(굴림4). 매 15초 rand(0~4)<AI 자동. **다른 귀신들 연동 필요라 그들 구현 후.**

### 시스템 (등뼈)
5. **오디오 고장 시스템** — 일반 종 횟수 초과 + 비상 종 소진 + 현승 피격 통합 "오디오 다운" → 리셋 필요(구현노트 §8.3, 예방적 리셋 = 종 횟수 초기화). 비상 종의 TODO 연결.
6. **정전 / 창고런** — 현우 3스택 → 배선 고장(≠배터리 방전) → 정전 → 창고런 5박자([결정]→[창고 기어가기 윤진]→[전기실 브레이커 윤석]→[탈출]→[복귀]). freeze 3단계. 5시 이후 창고런 안 생김·윤진 퇴장.
7. **카메라 에러** — CCTV 과사용 처벌(구현노트 §12).
8. **환기 시스템** — 셔터 장시간 폐쇄 → 환기 오류(유저 추가 아이디어). 김욱과 엮임(환기 공통).

### 남은 귀신 (5마리)
9. **윤진**(창고/Ballora 청각/창고런), **윤석**(전기실 브레이커), **김욱**(벌룬보이 환기 마비, 카메라 벗어나야 안전), **준영**(팬텀 포시, 관리실 방치→환기 오류, 카메라 켜야 안전, 밤 1회 — 구현노트 §13).

### 밤 진행 / 저장
10. **currentNight 자동화 + 저장(PlayerPrefs)** — 지금 수동. 클리어 시 다음 밤. (유저: "밤 넘기기는 밸런스라 기능 다 만들고 마지막에" — 우선순위 낮춤.)
11. 밤별 값 곡선(fixedNodeWaits, aiLevel, 연호 AI 2일1→5일16, 종 횟수 2일5/3일4/4일3/5일2).

### 현승 마무리
12. 현승 R3(환풍구), §14 자동 타이머(팬텀 프레디식), 스택 시각효과(1 조명깜빡/2 조명나감/3 정전).

### Phase 8 (그래픽 — 전부 맨 나중)
13. 조명·저해상도 필터·3D 점프스케어 애니(현승 팬텀 잔상/퍼펫 즉사, 연호 창문 급습, 윤진 창고런 등 **죽음마다 고유 애니·사운드·카메라워크**)·종소리 등 사운드(상태별: 정상/쿨 거부음/소진 거부음/오류)·환풍구 앰비언스·ProBuilder 맵·에러 패널 UI(FNAF3 시스템 리셋 메뉴)·리셋 진행 바·JumpscareManager(즉사>비즉사 우선순위 통합)·개발 로그 정리.

---

## 7. 문서 위치
- 기획서: `독서실_경비_기획서_이름스왑.md` (스왑본, 최신)
- 귀신 행동상세: `귀신_행동상세_이름스왑.md`
- 구현노트: `구현_노트_이름스왑.md` (§8~14: 리셋 패널, 점프스케어, 맵 방식, 연호 종, 카메라 에러, 준영 재설계, 현승 자동 트리거)
- **기획/귀신 관련 판단은 반드시 이 문서들 먼저 확인 후.**

---

## 8. 한 줄 요약 (스타일)
한국어로, 따뜻하고 격려하며, 설계 규율과 좋은 발견을 칭찬하되, **대등한 협업자로서 비판적으로 함께 사고**한다. 알고리즘을 말로 먼저 확인 → 유저 이해 확인 → 한글 주석 코드(작게 쪼개거나 통파일 교체). 클릭 단위 Unity 안내. 문서 없이 추측 금지. 메커니즘 먼저, 그래픽은 Phase 8.

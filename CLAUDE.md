# CLAUDE.md — 독서실 경비 (DokSeoSil_Horror) 프로젝트 인수인계

> 이 문서는 웹 Claude와 준현(mikeminyoon)이 여러 세션에 걸쳐 만든 프로젝트를,
> Claude Code가 **맥락 손실 없이 이어받기 위한** 완전 인수인계서다.
> "이전 Claude를 그대로 옮긴다"는 목적으로 작성됨. 처음 읽고 이 프로젝트의
> 설계 철학·완성 시스템·코드 구조·함정·남은 작업·협업 방식을 전부 파악할 것.
>
> **마지막 갱신: 2026-11-27** (Claude Code 세션 — 현우 스택 후폭풍 + 배선 고장 임시판, 환풍구 불빛에만 보임 + "등 뒤로" 갑툭튀, CCTV UI World 캔버스, 환풍구 자리 관리 + 현승 R3, 현우 1스택 시계 분 고장, 윤진 삭제·창고런 = 전기실만, 5시 배선 컷오프, ⭐연호 원작식 갈아엎기 — 종 = 옆방 유인 + 방 지도 + 창문/빼꼼/관리실 안, 창문앞 복도 조명 깜빡 + 실루엣).
> 설계 결정 원문은 **구현노트 §15**(11-24: §15.1~11 / 11-25: §15.12~17 / 11-26: §15.18~24 / 11-27: §15.25)에 있다.

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

## 3. 귀신 로스터 & 위협 매트릭스 (6마리 — 윤진 삭제, 2026-11-26)

> ⚠️ **이름 스왑 이력:** 과거 문서에서 윤진↔현우의 역할 이름표가 뒤바뀌어, 3개 문서에 sed 스왑 적용함. **현재 확정:**
> - **현우 = 환풍구/셔터/3스택 정전** (코드 Hyunwoo.cs)
> - **윤진 = 창고/Ballora 청각 스텔스/창고런** (코드 없음) → ⚠️ **2026-11-26 유저 결정으로 게임에서 완전 삭제**(구현노트 §15.23). 창고런은 전기실(윤석)만.
> 준영·연호·현승·김욱·윤석은 이름 안 바뀜. 프로젝트 문서는 스왑본으로 교체 완료(유저 확인).

| 귀신 | 위치 | 대응 | 결과 | 등장 | 엔진 |
|---|---|---|---|---|---|
| **현승** | 창문 | 육안 응시 | 비즉사(오디오 고장)→방치 시 **즉사**(퍼펫). 1일차는 고장 없음 | 1일~ | B |
| **현우** | 환풍구 | 셔터+라이트 | 비즉사(스택)→3스택 **정전** | 2일~ | B |
| **연호** | 창문(정면) | **종 = 옆방 유인**(CCTV, 원작 오디오) / 최후엔 비상 종(어디서든 → 처음으로, 빼꼼 전까지) | **즉사**(빼꼼에서 올렸다 못 막음 / 관리실 안에서 내림 / 시야 상실) | 2일 맛보기/3일 정식/5일 AI16 | **A** |
| **김욱** | 환풍구 CAM | 카메라에서 벗어나기 | 비즉사(환기 오류) | 3일~ | - |
| **준영** | 관리실 구석 | 카메라 켜기(김욱 반대) | 비즉사(환기 오류), 밤 1회 | 3일~ | - |
| **윤석** | 전기실 | 스파크·달래기 | 즉사(창고런 — 유저: 죽을 수 있게) | 4일~ (3일차 첫 창고런은 브레이커만 + 윤석은 뒤에 서 있기만, 자세한 건 창고런 때) | - |

### ⭐ 겹침의 진짜 축 = CCTV ON/OFF 상충 (위치가 아니라)
- **켜야:** 연호 종, 준영 없애기(어느 CAM이든 CCTV를 다 켜면 됨)
- **꺼야:** 김욱 안전(보면 위험), 현승 창문 육안 응시, (CCTV 과사용 = 카메라 에러 확률)
- 이게 매 순간 "CCTV 켤까 말까" 판단 = 겹침의 핵심 저글링.
- **창고런(윤석)은 정전 freeze라 다른 위협과 격리** → 동시 위협 실질 5마리.
- **즉사는 연호 하나(5마리 중)** → 우선순위 단순(연호 최우선). "연호만 확실히 막으면 나머진 좀 당해도 생존."
- 5일 = 전원 풀가동(제한 안 함). 후반 카오스는 "연호 우선 + 나머지 관리"로 풀림 = 공정.

### 준영 vs 김욱 = CCTV 정반대 (동시 등장 시 딜레마)
준영=CAM 켜야 사라짐 / 김욱=CAM 벗어나야 안전. 둘 다 3일~. 동시 빈도는 AI 곡선으로 조절해 억울하지 않게.

---

## 4. 완성된 시스템 & 코드 (파일별)

> 모든 코드는 씬의 해당 오브젝트에 부착. 아래는 각 스크립트의 역할·핵심 로직·주의점.
> ⭐ **밤 숫자 규칙:** 밤 숫자는 `GameManager.currentNight` 한 곳에만 있다. 다른 스크립트는 **자기 currentNight 필드를 만들지 말고 `GameManager.Night`를 읽을 것.** 테스트할 밤은 GameManager Inspector에서만 바꿈.
> ⭐ **밤별 값 규칙 (구현노트 §15.15):** 밤별 값은 `[0]` 안 쓰는 칸 + `[1]~[5]` 배열, 꺼낼 땐 **`GameManager.ByNight(배열)`만**, 시간대는 **`GameManager.Hour`(0~6, 12시=0)**만. **AI 0 = 비활성**(등장 날짜 칸을 따로 두지 않음). 귀신마다 `testAiOverride`(-1 = 끔)로 밤 상관없이 테스트. 새 귀신·시스템도 이 규칙을 따를 것.

### 4.1 현승 (Hyunsoong.cs) — COMPLETE, 엔진B
- 루트 R1/R2/R4/R1'(로비 잠복)/R3(환풍구) + **허공 등장 노드**(fixedNodeCount=3, fixedNodeWaits 배열 [허공10~30, 착석10~20, 기립3~7]). 노드0(허공)은 스태틱 X, index>0만. `fixedNodeCount` = 앞에서 몇 노드를 고정 랜덤 시간으로 기다릴지(그 뒤는 엔진B, 후퇴도 이 구간으론 안 감). ⚠️ 씬 값이 2로 남아 있었음(허공 노드 추가 때 안 바뀐 흔적, §15.21) — 3으로 맞추라고 안내함.
- 루트 해금은 `GameManager.Night`: 1밤 R1 / 2밤~ +R2·R4 / 3밤~ +R1'.
- **R3 환풍구 루트 (구현노트 §15.21):** 루트 고를 때 `r3ChanceByNight` {0,0,0,0.15,…} 주사위 먼저(3밤~ 15%). 노드 [허공, 착석, 기립, 공부방2, 환풍구(CAM), 화장실 = **R1과 같은 `Hs_Bathroom_Inside`**, 관리실 환풍구]. 마지막 노드 = 관리실 환풍구 → **현우처럼 셔터 판정 A**(숫자는 `vent*` 필드로 따로) → 못 막으면 창문과 같은 armed → STRIKE1. 불빛에만 보임(`VentLightReveal` 자동). 환풍구가 막혔으면(`VentOccupancy`) **기다리지 않고 창문으로 돌아감**(R1 뒷부분으로 갈아탐). 테스트 `testR3ChanceOverride`.
- **STRIKE1 캐스케이드 = 완료**(유저 확인, §15.21): 잔상식, 따로 잠그지 않음 — 행동상세 §1.4의 "~10초 잠금"은 옛 내용.
- 밤별: `aiByNight` {0,3,5,8,11,14}, `cycleTimeByNight` {0,8,7,6,5,4}(기획서), `walkPathTimeByNight` 8초(유저, 밤별 차이는 미정). **1일차 확정 출현:** 3시까지 창문에 안 왔으면 창문으로 보냄(`guaranteeNight/Hour`).
- 엔진B(cycleTime마다 rand<aiLevel 전진) + 행동 굴림(85/10/5) + 창문 워크패스 + GazeBox.
- **STRIKE1**(비즉사, 오디오 고장 — 1일차는 점프스케어만) → **STRIKE2**(즉사, 퍼펫 — AudioSystem이 방치 32초 후 호출). STRIKE2에서 `GameManager.GameOver("현승퍼펫")` + `jumpscare.PlayGameOver()`.
- **armed 발동 = 둘 중 먼저 오는 쪽** (§14 원안 "자동만"을 유저가 변경, 구현노트 §15.10): ① 화면 전환 시 즉시(HandleScreenTransition 구독 유지) ② 안 해도 `armedAutoDelay` 5초 뒤 자동. CCTV/패널 보고 있었으면 jumpscare.Play()가 강제로 닫음. **연호 캐스케이드(§15.24 이후):** 잔상 = 시야 상실 → 연호가 빼꼼·관리실 안이면 잔상 끝나고 이어서 즉사(창문앞이면 상관없음 — 예전엔 빼꼼으로 넘어갔음).
- 창고 노드 도착 시 "와장창" 로그 — 소리는 Phase 8.

### 4.2 현우 (Hyunwoo.cs) — COMPLETE 완전체, 엔진B + 판정 A + 모델/애니
- 환풍구 루트 노드 [허공, 공부방2 앉음, 환풍구 매달림, 화장실, 관리실 입구]. 스태틱은 index 1·2·3에서만(화장실=사라지는 순간).
- **관리실 환풍구(노드 4, `roomVentNode`)는 불빛에만 보임**(`VentLightReveal`, 자동으로 붙음) — 셔터가 닫혀 가리면(`ShutterController.IsCoveringVent`) 불빛 켜도 안 보임. 도착은 플레이어가 불빛으로 보고 있으면 **끄는 순간**으로 미룸. 5시 퇴장도 같음. 화장실 → 환풍구 이동에도 CCTV 지직. (구현노트 §15.19)
- **환풍구 자리 관리 `VentOccupancy.cs` (구현노트 §15.21, 유저 제안):** 관리실 환풍구는 **한 번에 한 귀신**(`IsVentUsing`). 쓰는 중 = 들어오기로 한 순간(불빛 대기 포함) ~ 사건 끝(격퇴/STRIKE1/갑툭튀/퇴장), **현우 "등 뒤로"도 쓰는 중**. 막히면 현우 = 화장실에서 대기 / 현승 = 창문으로 돌아감. 우선순위 `ventPriority` 현우 2 > 현승 1 — **현우 먼저**(유저): 현승이 쓰는 중에 현우가 오면 현승이 **armed 전까지는 비켜주고 창문으로**(`ShouldYield`, 환풍구 안이면 플레이어가 못 볼 때만). armed면 STRIKE1 그대로. **비운 뒤 `freeGap` 7초**(유저 — 쿵쿵 소리 자리). `TryEnter(귀신, 우선순위, 기다릴지)` / `Release(귀신)`(아무 때나 불러도 안전). 씬에 없으면 자동 생성. 새 환풍구 귀신도 이걸 쓸 것.
- **허공 대기 밤별 배율 `voidWaitScaleByNight`** {0,1,1,0.7,0.45,0.3} × 씬 `fixedNodeWaits[0]`(30~150초, 유저 조정 — 예전 101~164) — 후반일수록 자주 옴.
- **셔터 판정 A** (상태 enum: Moving → AtEntrance → Armed → Behind):
  - **관리실 환풍구 노드 도착 = 즉시 판정 시작**(2026-11-26 수정 — 예전엔 도착 후 주사위 한 번 더 통과해야 시작돼서 그동안 셔터가 무의미했음) → `infiltrationTimer`(침투, 셔터 열린 동안 참, limit 15s) / `holdTimer`(유지, 셔터 닫힌 동안, holdRequired 5s).
  - 셔터 닫고 5초 유지 → **격퇴(Repel)** / 초당 1/10 조기 이탈 굴림.
  - 셔터 열면 holdTimer 리셋 + 침투 재개. 침투 만료 → **BecomeArmed**.
  - **armed:** 아직 환풍구에 있음(셔터 소용없음, 고개 돌려도 안 당함). **화면 전환 또는 `comeBehindTime` 6초**(옛 armedGrace, FormerlySerializedAs) → **Behind(등 뒤로 = 환풍구에서 사라짐)**.
  - **Behind:** 시야 구역이 **환풍구 말고 다른 곳으로 바뀌는 순간** 갑툭튀 → GetHit(스택+1). 환풍구 쪽으로 돌리는 건 안전. `behindAttackTime`(0 = 끔, 유저 설계대로) 켜면 고개 안 돌려도 N초 뒤 갑툭튀.
  - **격퇴와 등 뒤로가 똑같이 보임**(빈 환풍구) = 심리전 — 유저 설계.
- 밤별 `aiByNight` {0,0,5,6,10,13} (1일 비활성). **5시 퇴장**(`leaveHour`, 이동 중일 때 허공으로). **2일차는 스택 대신 배터리 -15**(`stackFromNight` 3, `earlyNightBatteryCost`).
- **stackCount 후폭풍 (3일차~, 구현노트 §15.18):** 1 = 관리실 조명 가끔 깜빡(기획서의 "잠깐 시야 흐림"은 유저가 플레이 후 뺌 — 환기 오류 흐려짐이랑 겹침) / 2 = 조명 나감 / 3 = **배선 고장**(`WiringFailure`, 밤당 1회). 조명은 **단계식**(2가 1을 덮어씀). **+ 1스택부터 시계 분 고장**(쌓이는 효과 — 2스택에도 남음, 배선 복구 때 수리, §15.22). 배선 고장 낸 뒤 또 당하면 배터리 -15(`stacksSpent`). **후폭풍을 잔상(jumpscare.Play)보다 먼저** 부름 — 순서 바꾸면 잔상 중 시야 회전 잠금이 풀림.
- 2스택 효과는 원래 "CCTV 노이즈"였으나 카메라 에러와 겹쳐서 **"조명 나감"으로 변경됨**(구현노트 §12.2). **환풍구 불빛은 배선 고장 때 말고는 안 건드림**(유저 결정 — §12.2의 "불빛 확인 방해"는 안 함).
- 테스트: 플레이 중 Inspector에서 `stackCount`를 0/1/2로 놓고 한 번 당하면 다음 단계 효과가 나옴.
- **Animator PoseNode(Integer)**: 1=Sit, 2=Hanging, 4=InVent(크롤링 변형). AnyState 전환, MoveToNode에서 `SetInteger("PoseNode", index)`.
- 모델: Meshy AI 생성 → 텍스처(URP/Lit, Base Map) → Mixamo 크롤링 포즈 변형(InVent). 캡슐 제거, 모델에 스크립트 직접 부착. **로직/실루엣 통합**.
- 참조: monitor, shutter(isShutterClosed 읽기), viewController(currentZone), jumpscare, animator.

### 4.3 셔터 (ShutterController.cs) — COMPLETE
- 오른쪽 구역(ventZone=2) UI 버튼 토글(closedPos/openPos Lerp, `isShutterClosed` public).
- 라이트 홀드(EventTrigger PointerDown→LightOn / Up→LightOff, ventLight Spot, **배터리 무관 — 유저 결정, 소모 넣지 말 것**). **환풍구 구역 벗어나면 자동으로 꺼짐** → "불빛 켜짐 = 환풍구 보고 있음"(현우가 이걸 믿고 도착·퇴장 판정). 환풍구 불빛은 배선 고장 때 말고는 다른 시스템이 건드리지 않음.
- **ForceLock()**: 점프스케어 시 버튼 숨김 + 라이트 끔.
- **shutterDrainRate**: 닫힌 동안 `BatteryManager.DrainPerSecond` (기본 2는 높음 → 1 권장).

### 4.4 카메라·패널·전환 스크립트
- **CCTVController.cs**: FNAF식 토글(마우스 하단 재진입으로 on/off). 가운데 구역(allowedZone=1)에서만 켜짐. `isCameraDown` public. **`isFullyIn`**: 카메라가 모니터 앞에 다 도착했나(isCameraDown은 켜는 순간 true라 따로 만듦, 준영이 씀). **`PlayerCamera`**: 플레이어 시점 카메라. **"현재 방" 개념 없음** — 위치/회전만 관리. 복귀는 **homeLocalPos**(Start에서 1회 고정). ForceCancel/CancelReturn 상호 처리.
- **PanelController.cs**: 동일하게 homeLocalPos 방식. 왼쪽 구역 가면 자동으로 패널 열림.
  - `ClosePanel()`(Exit 버튼): **오디오든 카메라든 리셋 중이면 못 나감**(손 묶임).
  - `ResetAll()`(전체 리셋 버튼): 오디오+카메라+환기 동시, `resetAllDuration` 8초.
  - **리셋은 한 번에 하나만**(원작 규칙): `IsBusy` = 아무 시스템이나 리셋 중. 진행 중이면 각 시스템 `StartReset()`·`ResetAll()`이 거절. `IsResettingAll` = 전체 리셋으로 시작한 리셋 진행 중(메뉴가 reboot all 옆에만 rebooting 표시).
  - 참조: audioSystem, cameraSystem, ventSystem — 비어 있으면 Start에서 자동으로 찾음(연결 누락으로 잠금·고장 화면이 조용히 안 된 적 있어서 바꿈).
- **ViewController.cs**: 4구역, 구역별 microYaw/microPitch 배열화, SuppressEdgeUntilRelease. `currentZone` public.
- **ScreenTransitionDetector.cs**: CCTV 올림/내림 + 패널 열기/닫기 → `OnScreenTransition` 이벤트(현승·현우 armed가 씀). `ForceTransition()`(환기 오류용 — `OnVisionLost`도 같이). 시야 회전은 전환 아님. **연호용(§15.24):** `PlayerIsUp`(CCTV 켬 또는 패널 엶) / `OnLowered`(올려진 상태 → 내려진 순간, 강제 포함) / `OnVisionLost`(강제 전환 = 시야 상실).

### 4.5 MonitorDisplay.cs — COMPLETE (현재 방 관리 = 여기)
- 상태 enum: Off / Switching / Active. **currentRoom**(지금 보는 방 인덱스, roomFeeds 기준).
- `SwitchRoom(idx)`(버튼 호출), `GhostMoveStatic()`(귀신 이동 시 스태틱), buttonGroup(CanvasGroup, 방 전환 버튼 묶음).
- `RingBellCurrentRoom()` → `yeonho.RingBell(currentRoom)`. 종 버튼 OnClick → 이 함수.
- `IsShowingRoom(room)`: 지금 CCTV로 그 방을 보고(듣고) 있나 — CCTV 켜짐 + Active + 그 방 + 카메라 고장 아님 + 전기 들어옴. 연호 창고 부스럭 판정용.
- **CAM07(`storageRoomIndex`=6)은 Active여도 영상 대신 전용 화면** — 거의 검은 바탕 + 드문드문 먼지 점(`storageBase*`/`storageSpeck*`/`storageInterval`로 조절). 전환 스태틱·카메라 고장 스태틱(밝은 TV 노이즈)과 일부러 다르게 함(유저 요청).
- **카메라 고장(`cameraSystem.IsCameraBroken()`) 시 방·상태 무관 전부 스태틱.** 단 상태(버튼 켜짐/꺼짐)는 고장 중에도 CCTV 따라 갱신 — 예전엔 건너뛰어서 고장 중 CCTV 꺼도 버튼이 눌렸음(§15.21).

### 4.6 JumpscareOverlay.cs — COMPLETE
- Play()(빨강 STRIKE1, 1.5s 후 Stop) / PlayGameOver()(보라, 영구 잠금).
- 잠금 방식 = 각 컨트롤러 `enabled=false` 직접(방식 A). **cctv/panel/viewController/shutter 등록 완료**.
- `IsPlaying`(잔상 재생 중) — 연호가 읽음: 잔상 시작 = 시야 상실(빼꼼·관리실 안이면 즉사), 연호 즉사는 잔상이 끝난 뒤 이어서(연속으로 당하기).

### 4.7 GameManager.cs — COMPLETE 기초
- static Instance(Awake). **`public static int Night`** — 모든 스크립트가 밤 숫자를 여기서 읽음(GameManager 없으면 1).
- `currentNight`(수동 — ⭐나중 저장 시스템/자동화 PENDING). 씬 값 nightDuration **500s** / firstNightDuration **380s**(유저 조정 — 코드 기본 390/240). `isNightOver`/`isGameOver`는 Awake에서 항상 false로 초기화(체크된 채 씬에 저장된 적 있음 → 시작부터 6시 판정 위험).
- gameHour/gameMinute 계산(12시→6시). 6시 → NightClear. GameOver(cause)(isGameOver 시 Update 정지 = 타이머·시계 멈춤).
- 예전엔 GameManager가 귀신 필드에 밤을 넣어주는 방식이었으나 Start 실행 순서 문제로 **제거**(구현노트 §15.8). 귀신 참조 필드도 없음.
- **주의:** 게임오버 시 귀신들은 안 멈춤(점프스케어 오버레이가 화면 덮어서 무관, 씬 전환 예정).

### 4.8 Clock_Digital.cs — COMPLETE
- 에셋 시계 개조. 현실시간 → **GameManager.gameHour/gameMinute**. URP 전용(_BaseMap/_EmissionMap만). 3D 오브젝트 시계(월드), 분도 표시.
- ⚠️ **파일이 CRLF + BOM** (다른 스크립트와 다름) — 고칠 때 줄바꿈 유지.
- **분 고장 (현우 1스택~, 구현노트 §15.22 — 유저 설계):** `FreezeMinute()` / `UnfreezeMinute()` / static `FreezeMinuteAll()`. 당한 순간의 **분만 멈춤, 시는 계속 감**(2:37에 당하면 3시엔 3:37) + 콜론은 계속 깜빡. 2스택에 또 당해도 처음 분 그대로. **배선 고장 복구 때 수리**(WiringFailure). 표시만 고장 — 시간 판정은 전부 GameManager를 읽음(시계 표시를 읽는 코드 없음).
- `PowerOff()`/`PowerOn()`(방전·배선 고장)은 색만 바꿈 — 분 고장과 별개.

### 4.9 BatteryManager.cs — COMPLETE
- static Instance. battery 100/maxBattery. **naturalDrain**(0.6은 너무 빠름, 0.1). Drain(즉시)/DrainPerSecond(지속). Deplete → **BatteryBlackout.Begin()**(씬에 없으면 자동으로 붙임). 여기서 GameOver 안 함 — 밤 타이머가 흘러야 6시 생존 가능.
- **배터리 방전 = 암전 시퀀스**(4.15). ≠ 현우 배선 고장(창고런).
- 장치별 소모율은 **각 장치가 배터리에 요청**(셔터 shutterDrainRate, 종 bellDrain, 비상 종 batteryCost).

### 4.10 BatteryDisplay.cs — COMPLETE
- **World Space Canvas** 텍스트, 모니터에 부착. `cctv.isCameraDown`일 때만 표시.
- ⭐ **UI 철학 (유저가 스스로 도출):**
  - **World Space** = 3D 오브젝트에 붙은 정보/장치(배터리, 비상 종).
  - **Screen Overlay** = 플레이어 조작(셔터·라이트 버튼). 패널·CCTV 버튼은 World로 옮김(아래).
  - 씬 구조: 3D 장면은 저해상도 RT → Overlay `Canvas`의 전체화면 `RawImage`로 표시. 같은 `Canvas`에 Vent 버튼 묶음과 JumpscareOverlay가 있음. World 캔버스는 `CCTVWorldCanvas`(CCTV 버튼 + 배터리, CCTV_Screen 자식) / `ResetPanelWorldCanvas` / `EmergencyBellWorldCanvas` — **이벤트 카메라는 전부 `UIRayCamera`**.
  - **CCTV 버튼 (구현노트 §15.20):** `CCTVWorldCanvas`(옛 `WorldCanvas` 재활용, PanelUI 레이어 → 고해상도 글자). `MonitorDisplay.buttonGroup` = `CCTV Swtichs` Canvas Group 그대로. 나중에 UI 이미지는 버튼 `Image`에 스프라이트만 넣으면 됨.
  - **리셋 패널 (구현노트 §15.16):** `ResetPanelWorldCanvas`(World, 이벤트 카메라 UIRayCamera, Canvas Group = PanelController.panelButtons) + **패널 글자만 고해상도** 전용 카메라 `PanelUI_Camera`(PanelUI 레이어만 → `PanelUIRT` 1600×1200 → `RawImageHIgh`, 항상 켜둠, Near 0.1, HDR·MSAA 끔은 씬 파일에서) + `ResetPanelMenu`(>>> 커서 / error·rebooting 깜빡, 이름으로 자동 연결). 태블릿 모델은 에셋 스토어 무료 패키지(스크립트는 불필요, 사용 중단 API 경고 남).

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
- ⭐ **2026-11-26 원작식으로 갈아엎음 (구현노트 §15.23~24)** — 고정 루트·"연호 있는 방에 종" 폐기. 유저 소감: "밀어내기 원툴", "루트 정형화", "온지도 모르고 죽음".
- **지도 = `rooms[]` (방 번호 = CCTV 방 번호 = roomFeeds 인덱스).** 각 방 = `standPoint`(서 있을 자리) + `neighbors`(붙은 방, 한쪽만 적어도 양쪽). 유저 맵 그림 + **창고 뒷문**(유저 OK):
  - 세로복도(5) — 창문앞·가로복도·화장실·로비·창고 / 가로복도(2) — 세로복도·공부방1·공부방2·창고 / 창고(6) — 가로복도·세로복도(문 2개)
  - 화장실(3)·로비(4) = 세로복도만, 공부방1(0)·공부방2(1) = 가로복도만 (공부방1↔2 문은 "해보고 결정" — 유저).
  - 창문까지 거리(Start에서 BFS): 세로복도 1 / 가로복도·화장실·로비·창고 2 / 공부방 3. `spawnRoom` 1(공부방2), `windowRoom` 5(세로복도).
  - **자리가 빈 방은 지도에서 빠짐**(못 감, 경고 로그). 옛 `nodes`/`nodeRoomIndex`([HideInInspector])가 빈 자리를 자동으로 채움 → 공부방2·가로복도·로비·세로복도는 옛 자리 그대로. **공부방1·화장실·창고 자리는 유저가 씬에 만들어 연결해야 함.**
  - 시작 로그 `연호 지도: 공부방1 3칸 / ...`로 확인.
- **이동(엔진A):** 전진 = 창문에 더 가까운 옆방(세로복도에서 전진 = 창문앞) / 후퇴 = 더 먼 옆방 랜덤 → 없으면 같은 거리 옆방 → 없으면 멈춤으로 침(막다른 방). 굴림 가중치 씬 값 **전진 65 / 후퇴 15 / 멈춤 20**(유저 조정, 코드 기본 60/15/25).
- **등장:** 시간 로직(spawnDelay 10~25)으로 model SetActive. **밤별 AI** `aiByNight` {0,0,2,5,9,16} (1일 비활성).
- **2일차 맛보기:** 4시(`tasteHour`)부터 등장 대기 → 창문앞·빼꼼에서 종으로 끌어내거나 비상 종 쓰면 그날 퇴장.
- **맵의 의미(유저와 정리):** 공부방 = 주차 자리(3칸, 출구 하나) / 화장실·로비 = 위험 구역(일반 종으로 더 못 밀어냄) / 창고 = 도박(안 보임 + 뒷문으로 세로복도에 불쑥). 세로복도에서 유인 정답 = 가로복도.

**창문 쪽 3단계 (상태 enum: Spawning → Moving → WindowFront → Peeking → Inside) — 원작 창문/문간/사무실 안:**
- **창문앞(WindowFront):** 도착 순간 임시 숨소리(`YeonhoSounds`). CCTV엔 안 보이고 창문 너머로만. **화면 전환 상관없음**(내려서 확인해도 안전 — 예전엔 빼꼼으로 넘어갔음). `windowFrontTime`(씬 값 **약 12.4초**) 지나면 빼꼼. 랜덤 후퇴 없음.
- **빼꼼(Peeking, 문간):** 가만히 있으면 안 죽음. **CCTV·패널을 올리면 `doorEntryTime`(2.5초) 안에 세로복도 종이 먹혀야 함 → 못 하면 관리실 안.** 내리면 카운트 초기화(유저 OK). CCTV 보는 중에 빼꼼이 되면 그 순간부터 카운트(원작 "모니터에 너무 오래 숨기"). 패널 리셋에 묶여 있으면 그대로 들어옴(원작 "재부팅 틈").
- **관리실 안(Inside):** 모델 숨김. **CCTV 내림·패널 닫음(강제 포함) 순간 즉사.** 6시까지 안 내리면 생존.
- **시야 상실 = 빼꼼·관리실 안 즉사:** 환기 흐려짐(`ForceTransition` → `OnVisionLost`) / **다른 귀신 잔상**(`JumpscareOverlay.IsPlaying` 시작). 잔상이면 **그 잔상이 끝난 뒤 연호 점프스케어**(유저: "연속으로 당하기" — 원작도 팬텀 점프스케어 때 덮침).
- **연호가 창문앞에 와도 CCTV 강제 다운 없음** — "앞에 있는지 몰라야 짜릿하다"(유저). 대신 임시 숨소리.
- **창문 쪽(창문앞·빼꼼·떠나는 반응 텀)에 있는 동안 = 창문앞 복도 조명 깜빡 + 실루엣**(구현노트 §15.25, 유저 요청 11-27). `WindowCorridorLight`(평소 켜짐 → 불규칙 깜빡, 켜고 끄기만, 배선 고장·방전과 아직 안 엮음, 자동 생성됨). **조명 연결 = 연호 Inspector `Corridor Lights` 칸에 Light를 넣음** — 유저가 만든 `Lights/Window Corridor Light`(Point, Realtime). 비우면 연호 뒤 복도 쪽에 임시 역광 조명 자동 생성. ⚠️ 예전 `Window Light` 칸은 스크립트 타입이라 Light가 안 들어갔음(11-27 유저 발견) → Light 칸으로 바꿈 + `Silhouette`(모델 재질을 검은 무광으로 바꿨다 되돌림, 재사용 가능 — 연호 모델에 자동으로 붙음). 떠나면(유인·비상 종·관리실 안·6시·방전) 원래대로. 관리실 안이면 조명이 멀쩡해짐.
- 6시(`isNightOver`)·게임오버면 연호 멈춤.

**흉포 토글 (YeonhoAggression.cs) — COMPLETE 1차 (구현노트 §15.14):**
- 원인: 오디오 고장 중 / 환기 오류 중 / **정전(배선 고장) 중** / 흉포 시간대(`furyHourStart~End` = **5~6시**, 마지막 한 시간 — 문서 §2.5의 4~5시에서 유저가 변경) / 방치(`idleLimit` 씬 값 20초) / 확률(`rollInterval` 15초마다 `rand(0~19) < aiLevel`, 원안 /5는 3일부터 100%라 바꿈).
- **유지:** 원인이 생기면 즉시 ON, **원인이 사라진 뒤 `furyMinDuration` 후 OFF**(코드 기본 8초, 씬 값 10초 — 유저 조정. 확률이면 딱 그만큼). 처음엔 "최소 15초"였는데 유저가 빡세다고 줄임 — 유지 시간과 주사위 간격을 분리함.
- 방치 = **조작 없는 시간**: 시야 구역 변경(+ `roomSwitchCounts` 켜면 CCTV 방 전환, 씬에서 켜져 있음). CCTV·패널 올림/내림은 조작 아님.
- 효과(Yeonho.TickSecond): 카운터 +2/초, 임계값 -1. **급습(굴림 4) 없음**(유저가 뺌).
- 연호가 없으면 자동으로 붙임. 방전 암전 때 정지.

**일반 종 (RingBell) = 유인 (원작 FNAF3 오디오):**
- `RingBell(room)`: room = MonitorDisplay.currentRoom(지금 보는 CCTV 방). 종 버튼 OnClick은 그대로 `MonitorDisplay.RingBellCurrentRoom`.
  - 오디오 고장 중이면 못 침. 쿨(bellCooldown 5.4s) 중 무시. 쿨·배터리(bellDrain, 씬 값 **2**)는 **헛방이어도 소모.** `AudioSystem.TryUseBell()` 횟수 — 초과면 오디오 고장 + 이번 종은 안 먹힘.
  - **돌아다닐 때:** 연호가 친 방의 **옆방**에 있으면 종소리 + 반응 텀 뒤 **그 방으로 걸어옴**(보고 있는 화면으로 들어옴). 같은 방·안 붙은 방 = 헛방. **잘못 치면 창문 쪽으로 끌어옴**(예: 로비에 있는데 세로복도에 침).
  - **창문앞·빼꼼:** 세로복도에 쳐야 세로복도로 돌아옴. 받아들여지는 순간 떠나는 중(빼꼼 카운트·즉사 판정 멈춤).
  - **관리실 안:** 헛방(이미 늦음).
  - **무시 1/7은 어디서나**(창문앞·빼꼼 포함 — 비상 종이 100%라서, 유저).
- **종 반응 딜레이:** 종소리(`bellSoundDuration` 1.0) + 랜덤 텀(씬 값 **0.5~3초**) 뒤 이동. 반응 중 엔진 멈칫. 끌려온 뒤 moveCounter/secondTimer 리셋(바로 재이동 방지).
- ⚠️ 옛 규칙들(구현노트 §11 "인접 뒤쪽 방", §15.5 "연호 있는 방에 종 / 창문앞은 세로복도 감 종 2칸 후퇴")은 **폐기**. 현행은 위.
- 종 횟수(`bellLimitByNight` 2~5회/리셋)는 유인이 연달아 필요해 빠듯할 수 있음 — 플레이 후 조정 예정.

**비상 종 (EmergencyBell.cs) — 2026-11-26 재정의 (구현노트 §15.24):**
- **역할:** 진짜 최후 수단. 창문 옆 벽 World 버튼(가운데 구역 볼 때만, UIRayCamera로 클릭 판정).
- **효과: 돌아다니는 중(유인 반응 중 포함)이든 창문앞이든 100% 스폰 전으로**(`Yeonho.EmergencyPush()`). **등장 전·빼꼼·관리실 안 = 헛방**(유저: "A안 + 창문 상황 아니어도 쓰면 처음으로"). 화장실·로비(위험 구역)에서 쓸 이유가 생김.
- 누르면 효과와 무관하게 **횟수·쿨·배터리 소모.** **하루 2번(usesPerNight), 쿨 60초.**
- **오디오 시스템과 별개:** 오디오 고장 중에도 작동, 소진해도 오디오 고장 안 남(그냥 안 눌림), 리셋으로 안 채워짐.

**창고 소리 (YeonhoSounds.cs — 임시, 구현노트 §15.24):** CAM07은 화면이 없어서 소리가 정보의 전부.
- 들어갈 때 **우당탕**(어디서든 들림) / 안에 있는 동안 **CAM07을 보면 부스럭·숨소리**(`MonitorDisplay.IsShowingRoom`, 반복) / **나갈 땐 조용** → 나갔는지 확인하러 CAM07을 봐야 함.
- 창문앞 도착 **숨소리**도 여기. 각 칸(`windowArriveClip`/`storageEnterClip`/`storageInsideClip`)에 진짜 소리 넣으면 그걸 씀(비우면 코드가 임시 소리 생성). 연호가 없으면 자동으로 붙음. 연호가 꺼지면(방전 암전) 소리 정지.

### 4.12 AudioSystem.cs — COMPLETE (오디오 고장 + 리셋 + 종 횟수)
- **고장 원인:** 현승 STRIKE1, 일반 종 밤당 횟수 초과. (비상 종은 무관)
- **1일차는 고장 자체가 없음**(`noBreakUntilNight`=1) → 1일차 STRIKE1은 점프스케어만, STRIKE2 불가.
- **방치 32초(`breakTimeLimitByNight`, 전부 32 — 기획서 10→5초는 너무 짧아서 유저가 늘림) → 현승 STRIKE2 퍼펫 즉사.** 원인 무관(종 과사용 고장도). 의도됨 — FNAF2 퍼펫 느낌.
- **종 밤당 한도 `bellLimitByNight` = {0, 0, 5, 4, 3, 2}:** 인덱스 = 밤 숫자 그대로([0]은 안 쓰는 칸). **0 이하 = 무제한** → 1일 무제한 / 2일5 / 3일4 / 4일3 / 5일2. Inspector에선 Element 0부터 보여서 헷갈림 주의.
- **리셋:** `StartReset()`(버튼용, 5초) / `StartResetFor(초)`(전체 리셋용). **고장 없어도 예방적으로 가능**, 완료 시 `bellUses = 0`(예방적 리셋 전략, 구현노트 §8.3).
- 고장 알림 = 패널 error 표시만(경보·사이렌은 환기에만 — 원작대로, §15.17).

### 4.13 CameraSystem.cs — COMPLETE (카메라 에러, 구현노트 §12·§15.2)
- CCTV 켜져 있는 동안만 **`rollInterval` 3.5초마다 `breakChance`**(씬 값 2% — 유저 조정, 코드 기본 3%) 굴림 → 고장 시 CCTV 완전 스태틱. 끄면 주기 누적 초기화.
- 고장 중에도 종은 사용 가능(원작대로).
- 리셋: AudioSystem과 같은 구조(`StartReset()` / `StartResetFor(초)`, 예방적 가능).
- 참조: cctv. MonitorDisplay·PanelController가 cameraSystem을 참조함(비어 있으면 Start에서 자동으로 찾음).

### 4.14 VentilationSystem.cs — COMPLETE 1차 (환기 에러, 구현노트 §15.9)
- **원인(지금):** 셔터 공기 게이지 — 닫고 5초(`closedGrace`)까지 공짜, 이후 초당 +1, 20(`airLimit`)이면 고장. 열면 초당 -1. 닫힌 시간도 열면 연 만큼만 줄어듦(깜빡 꼼수 방지).
- **원인(나중):** 김욱·준영 → `BreakVent(이유)`.
- **효과:** 셔터 강제 개방 + 못 닫음(`ShutterController.SetForcedOpen`) / 8초 후부터 12초마다 흐려짐 → 가장 어두운 순간 `ForceTransition()` (연호·armed 현승 둘 다 반응).
- 흐려짐 검은 화면은 `blurOverlay` 비어 있으면 자동 생성(Overlay Canvas, sortingOrder 100 — 점프스케어를 잠깐 가릴 수 있음, Phase 8에서 정리).
- **경보(`VentAlarm.cs`, §15.17):** 환기 오류 중 빨간 경고등 깜빡 + 임시 사이렌(환기에만 — 원작대로). 씬에선 `VentSystem`에 붙어 있고 `Lights/Siren` 라이트 연결됨. 깜빡 주기 씬 값 **1.1초**(유저 조정). 배선 고장 중엔 꺼짐.
- 흐려짐 씬 값: `blurInterval` **8초**, `fadeTime` **1.4초**(유저 조정 — 코드 기본값 12/0.5와 다름).
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

### 4.16 김욱 (KimWook.cs) — 1차 구현 (팬텀 벌룬보이, 구현노트 §15.12)
- **보면 위험** (기획서 §3 — 행동상세 §3의 "응시하면 사라짐"은 옛 내용).
- **원작식 기습형**(봉쇄형은 깜빡 꼼수·만날 일 없음 허점으로 폐기): 20초 고정 간격 주사위 `rand(0~9) < aiLevel`(기본 3) → 등장 + 지직. CCTV 켜져 있으면 `currentCamChance` 50%로 **지금 보는 카메라에 끼어듦**, 아니면 CAM01~06 랜덤. CAM07 제외.
- 그 화면을 `exposureLimit` 2초 보면 → jumpscare.Play()(CCTV 강제 다운 → 연쇄) + `vent.BreakVent("김욱")`.
- **한 번 본 뒤 직접 넘기거나 CCTV 끄면 그 자리에서 사라짐**(`seen` 플래그). 다른 귀신 스태틱·고장 중엔 피한 걸로 안 침(시간만 멈춤). 사라질 때 지직 없음(유저 결정 — "아직 있나?" 불안).
- **카메라 렌즈 앞 실제 모델** 방식(CCTV가 실시간 렌더 텍스처라 CRT·조명을 같이 받음). 스크립트는 KimWook 모델에 직접 붙임 — 숨길 땐 렌더러만 끔.
- 위치: 씬 `MovePoints/KW_MovePoint` 자식 6개. **이름 속 CAM 번호로 자동 정렬**(`camNodes` 비우면) — Hierarchy 순서(Quridor_D가 Bathroom보다 위)와 roomFeeds 순서가 달라서 이렇게 함.
- 밤별 `aiByNight` {0,0,0,3,5,7}(3일~, 숫자 임의), `exposureLimitByNight` 2초(미정). 방전 암전 정지 목록에 추가됨.

### 4.17 준영 (JoonYoung.cs) — 1차 구현 (팬텀 폭시, 구현노트 §15.13)
- **창문 가리기 뺌** → **관리실 구석**(정면 시야 가장자리)에 진짜 모델(`JoonYoung`, 애니메이터 포함). 스크립트는 모델에 직접 붙임, 숨길 땐 렌더러만 끔, 모델은 제자리.
- **CCTV 올릴 때마다 주사위** `chanceByNight` {0,0,0.001,0.02,0.04,0.1}(인덱스 = 밤 숫자). 테스트용 `testChanceOverride`(0~1, 평소 -1).
- **생기고 사라지는 순간이 절대 안 보이게:** `cctv.isFullyIn` + **플레이어 화면에 안 보임**일 때만 렌더러 켜고 끔. 보임 판정 = 몸 안쪽 7개 지점 중 하나라도 "뷰포트 안 + 카메라→점 Raycast가 다른 충돌체에 안 막힘". ⚠️ 시야 범위만 보는 방식(TestPlanesAABB)은 **실패했음** — 플레이어 카메라 FOV 83°(4:3 RT라 좌우 약 99°)라 CCTV 안에서도 범위가 구석까지 덮어서 모니터에 가려진 것도 '보임'이 됨. 가리는 건 `CCTV_Screen`의 BoxCollider. 충돌체 없는 물체(CrtMonitor 몸체 등)는 통과 = '보임'으로 침(안전한 쪽). 보이면 "화면에 보여서 대기 중" 로그.
- 상태: Absent → Pending(주사위 성공) → Hidden(몰래 자리잡음) → Revealed(CCTV 내림, 유예 `graceTime` 5초) → Leaving(피함, `restAfterDodge` 20~30초) / Armed(정면에서 고개 돌림) → Done(공격: jumpscare.Play + BreakVent("준영"), 그날 퇴장).
- CCTV 올린 동안 유예 멈춤, **다 들어가야** 피한 걸로 침. 정면 보는 채로 유예 끝 또는 armed로 정면 복귀 → 공격.
- 방전 암전 정지 목록에 추가됨.

### 4.18 관리실 조명 (RoomLights.cs) + 배선 고장 (WiringFailure.cs) — 1차 (구현노트 §15.18)
- **RoomLights:** 단계 0 정상 / 1 깜빡(8~20초마다) / 2 나감. `SetPowerCut`(배선 고장) / `RestoreAfterRepair`(정상으로) / `PowerOff`(방전, 영구).
  - ⭐ **켜고 끄기(enabled)만** — 밝기·범위·색·위치 안 건드림. **라이트 Mode는 Realtime/Mixed**(Baked는 안 꺼짐). `lights` 비우면 임시 천장등 자동 생성 — 지금은 유저가 만든 `Lights/Real Light`가 연결돼 있음. 환풍구 불빛·Siren은 넣지 말 것(Siren은 오브젝트 켜둔 채 Light 컴포넌트만 꺼둠 — 오브젝트를 끄면 VentAlarm 경고등이 안 나옴).
- **WiringFailure:** `Begin(이유)` → 관리실 전기 나감(조명·환풍구 불빛·CCTV·모니터·패널·시계·환기 경보 꺼짐, 셔터 열린 채 멈춤, **비상 종만 살아있음**, 귀신 계속 움직임, 연호 흉포). 밤당 1회.
  - **지금은 임시로 `autoRepair` 20초 뒤 `Repair()`** → 조명 정상 + 시계 분 고장 수리. ⭐ 창고런 만들면 autoRepair 끄고 브레이커 수리 완료에서 `Repair()`만 호출.
  - **5시 컷오프:** `cutoffHour` 5 이후엔 안 남(`CanBegin`) — 현우 3스택이 5시 넘어 나오면 배터리 -15로 대신(§15.23).
  - 복구 시점에 배터리 방전이면 복구 안 함(방전 암전이 우선). 방전 암전이 WiringFailure를 끔.
- 둘 다 static `Instance` + `GetOrCreate()` — 씬에 없으면 현우 Start가 만듦.
- 셔터·모니터·시계에 `PowerOn()` 있음(배선 복구 전용 — 방전 땐 안 부름). `PowerOff()`는 둘이 같이 씀.

---

## 5. ⚠️ 함정 / 하지 말 것 (실제로 겪은 것들)

- **버튼 OnClick용 함수에 매개변수(기본값 포함)를 붙이지 말 것.** `StartReset()`을 `StartReset(float x = -1f)`로 바꿨더니 Unity 버튼이 함수를 못 찾아 아무 반응이 없었음(C# 기본값 매개변수는 Unity엔 그냥 "float 받는 함수"). 시간 지정이 필요하면 `StartResetFor(float)`처럼 **별도 함수**로.
- **새 참조 필드를 추가하면 씬에서 연결해야 함.** 안 하면 null이라 `if (x != null && ...)` 가드가 조용히 통과 → 기능이 그냥 안 되는 것처럼 보임. 씬 YAML에서 필드 줄이 있는지 확인.
- **씬에 저장된 Inspector 값이 코드 기본값보다 우선.** 코드 기본값을 바꿔도 이미 씬에 있는 컴포넌트엔 반영 안 됨 → 유저에게 Inspector 수정 요청하거나 씬 값 확인.
- **밤 숫자 필드를 스크립트마다 만들지 말 것.** `GameManager.Night`만 읽기(4번 머리말 참고).
- **테스트할 밤은 플레이 누르기 전에 바꿀 것.** 플레이 중에 `currentNight`를 바꾸면 밤 길이(Start에서 1회)·현승 첫 루트·현우 첫 허공 대기가 이전 밤 기준으로 남음 → 방문 횟수가 적게 나옴(2026-11-26 "3일차인데 현승·현우가 한 번씩만" 원인, §15.22). 로그 `=== 밤 N 시작` 줄로 확인.
- **Debug.Log 폭주 = 렉.** 매 틱 로그 빼고 ★ 상태 전환 로그만.
- **카메라 복귀 버그(해결됨):** 어중간한 위치를 home으로 저장하면 이상한 데로 복귀. → homeLocalPos를 Start에서 1회 고정.
- **World Space Canvas 스케일:** 처음 만들면 거대함. Scale 0.001부터.
- **World UI 클릭 안 될 때:** GraphicRaycaster IgnoreReversedGraphics, 전체화면 RawImage의 Raycast Target, 저해상도 RT(4:3)와 화면(16:9) 종횡비 불일치 — UIRayCamera로 해결함(커밋 9f84e4e).
- **World 캔버스 Event Camera는 항상 `UIRayCamera`.** `PanelUI_Camera`(RT에 그리는 카메라)를 넣으면 마우스 좌표가 RT 픽셀 기준이 돼서 클릭이 어긋남. 또 **카메라에서 Near보다 가까운 버튼은 클릭 판정에서 무시됨**(IgnoreReversedGraphics) — CCTV 볼 때 모니터까지 약 30cm라 `UIRayCamera`·`PanelUI_Camera` Near 0.1로 해둠(§15.20).
- **TMP 글자는 가로·세로 크기가 같아야 안 깨짐.** 늘린 큐브(`CCTV_Screen` 등 비균일 스케일)의 자식이거나 Scale X≠Y면 세로획이 깨짐(TMP는 선명도를 세로 크기로만 계산).
- **URP 셰이더:** 외부 모델이 분홍/하양이면 URP/Lit로 바꾸고 Base Map 연결.
- **엔진A 5개 임계값은 문서 오해였음.** 원본은 임계값 하나 + Random(1~15).
- **테스트값 되돌리기:** aiLevel↑, naturalDrain↑, spawnDelay↓, bellLimitByNight 등으로 테스트함. 커밋 전 원래값 복구. 단 유저가 플레이해보고 의도적으로 바꾼 값(예: windowFrontTime 12초)은 되돌리지 말 것 — 애매하면 물어보기.
- **`.gitignore`:** `.DS_Store` 무시 추가함. `.claude/`(로컬 설정)는 커밋 안 함.

---

## 6. 남은 작업 (우선순위 순)

### 바로 할 수 있는 것
1. ~~배터리 방전 → 연호 점프스케어~~ → 완료(4.15 방전 암전 시퀀스).
2. ~~현승 §14 자동 발동~~ → 완료(화면 전환 즉시 OR 5초 자동, 구현노트 §15.10). 알려진 모서리: 패널 리셋 중 STRIKE1이면 패널이 안 닫힌 채 점프스케어 종료 — 테스트 때 확인.
3. ~~고장 알림~~ → 완료: 패널 FNAF3식 메뉴(§15.16) + **환기 오류 경보**(`VentAlarm` 빨간 경고등 + 임시 사이렌, 환기에만 — §15.17).
3-1. ~~CCTV UI도 World 캔버스로~~ → 완료(구현노트 §15.20). 모니터 World 캔버스 + PanelUI 고해상도 글자. UI 이미지 교체는 Phase 8(버튼 Image에 스프라이트만).

### 밸런스
4. **밤 곡선 튜닝** — 구조(밤별 배열)와 기획서 AI는 넣음(4·§15.15). 남은 것: 숫자 미정인 배열 채우기(현승 워크패스, 김욱 노출, 준영 유예, 오디오 퍼펫 시간), windowFrontTime(약 12s), 비상 종(2회/60s), 현승 fixedNodeWaits, 카메라 에러 확률 — 직접 플레이하며 조정. 1일차 "12~1시 정적·4~6시 잔잔" 시퀀스도 아직.

### 귀신 마무리
5. ~~현우 스택별 효과~~ → 1차 완료(4.2·4.18, §15.18). 3스택 배선 고장은 **임시 20초 자동 복구** — 창고런 만들 때 `WiringFailure.Repair()` 연결. 관리실 진짜 조명 세팅(Realtime)은 유저가.
6. ~~현승 R3(환풍구)~~ → 1차 완료(4.1·4.2, §15.21 — 환풍구 자리 관리 포함). 씬 `routeR3` 노드 연결은 유저가. ~~STRIKE1 캐스케이드~~ → 완료(잔상식 = 현행 코드, 유저 확인).
7. ~~연호 흉포 토글~~ → 완료(4.11 흉포 토글, 정전 조건까지 연결).

### 시스템 (등뼈)
8. **창고런 = 전기실(윤석)만** (구현노트 §15.23 — 윤진·창고 기어가기·3막 탈출 삭제). 배선 고장 자체는 임시판 있음(4.18). ~~5시 이후 3스택~~ → 완료: `WiringFailure.cutoffHour` 5 이후엔 배선 고장 대신 배터리 -15. 남은 것: [결정] → 통로 진입(freeze) → 전기실 윤석(브레이커, SL Breaker Room식 미니게임 — 유저 제안 4~5개) → 복귀(페이드). 밤 시계 정지 + 복귀 시 30초 점프. 3일차 = 브레이커만 + 윤석 뒤에 서 있기. **자세한 건 창고런 시작할 때 같이 설계.**
9. ~~환기 에러~~ → 완료(4.14). 김욱·준영 원인, 연호 흉포, 경보까지 연결됨.

### 남은 귀신 (4마리)
10. ~~김욱~~ → 1차 완료(4.16). ~~준영~~ → 1차 완료(4.17). ~~윤진~~ → 삭제(§15.23). **윤석**(전기실 브레이커).
11-1. ~~연호 원작식 재설계~~ → **코드 완료, 유저 씬 작업·플레이 테스트 대기**(4.11, §15.24). 씬: 공부방1·화장실·창고 서 있을 자리 3개 만들어 `rooms[0/3/6].standPoint`에 연결. 볼 것: 빼꼼 2.5초가 손에 맞는지, 종 횟수, 임시 소리 볼륨, 공부방1↔2 문.

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

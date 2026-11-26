using UnityEngine;

// 현승: 노드 이동 → 창문 워크패스 → 응시 대응
// R3(환풍구 루트, 3밤~ 가끔): 창문 대신 관리실 환풍구로 옴 → 현우처럼 셔터 판정 A (구현노트 §15.21)
//  - 관리실 환풍구는 한 번에 한 귀신 (VentOccupancy). 막혔으면 기다리지 않고 창문으로 돌아감 (R1 뒷부분으로 갈아탐).
//  - 현우 우선: 현승이 쓰는 중에 현우가 오면 armed 전까지는 비켜주고 창문으로 (환풍구 안이면 플레이어가 못 볼 때만).
//  - 관리실 환풍구에선 불빛에만 보임 (VentLightReveal 자동으로 붙음). 불빛으로 보고 있으면 끄는 순간 도착.
//  - 못 막으면 창문과 같은 armed → 화면 전환 또는 armedAutoDelay → STRIKE1
public class Hyunsoong : MonoBehaviour
{
    [Header("루트 (각각 노드 배열)")]
    public Transform[] routeR1;    // [허공,착석,기립,가로복도,화장실,화장실안,세로복도,창문]
    public Transform[] routeR2;    // [허공,착석,기립,공부방2,가로복도,화장실,화장실안,창문]
    [Tooltip("R3 환풍구: [허공, 착석, 기립, 공부방2, 환풍구(CAM), 화장실, 관리실 환풍구] — 마지막 = 관리실 환풍구 (창문 대신).\n" +
             "⚠️ 화장실은 R1과 같은 노드(Hs_Bathroom_Inside)를 넣어야 환풍구가 막혔을 때 창문으로 돌아갈 수 있음")]
    public Transform[] routeR3;
    public Transform[] routeR4;
    public Transform[] routeR1p;    // ← 로비 잠복

    [Header("로비 잠복")]
    public Transform lobbyNode;         // Node_로비 연결
    public float lobbyLurkMin = 8f;     // 잠복 최소 시간
    public float lobbyLurkMax = 15f;    // 잠복 최대 시간

    private float lurkTimer = 0f;       // 잠복 경과
    private bool isLurking = false;     // 잠복 중?
    private float lurkDuration = 0f;

    // 날짜는 GameManager.Night에서 직접 읽음 (자기 필드 없음). 1밤=R1만 / 2밤~=R1·R2·R4 / 3밤~=+R1' (+R3는 확률)
    private Transform[] nodes;

    [Header("창고 노드 (소리 판별용)")]
    public Transform storageNode;  // ← Node_창고 연결

    [Header("등장 연출 (허공·착석·기립) 대기시간")]
    [Tooltip("앞에서부터 몇 개 노드를 '정해진 랜덤 시간'으로 기다릴지 (그 뒤는 엔진B 주사위). 후퇴도 이 구간으로는 안 돌아감")]
    public int fixedNodeCount = 3;   // 허공/착석/기립
    public Vector2[] fixedNodeWaits = {
        new Vector2(10f, 30f),   // 노드0: 허공 (등장 대기)
        new Vector2(10f, 20f),   // 노드1: 착석
        new Vector2(3f, 7f),     // 노드2: 기립
    };

    [Header("창문 워크패스")]
    public Transform windowStart;      // 창문 왼쪽 끝
    public Transform windowEnd;        // 창문 오른쪽 끝
    [Tooltip("밤별 워크패스 총 시간(초) = 응시 대응 시간. [0]은 안 쓰는 칸. 기획서: 1일 김 → 5일 가장 짧음 (밤별 숫자 미정 — 지금은 유저가 정한 8초로 통일)")]
    public float[] walkPathTimeByNight = { 0f, 8f, 8f, 8f, 8f, 8f };

    [Header("1일차 확정 출현 (기획서 §7 — 모든 플레이어가 응시를 한 번은 배움)")]
    public int guaranteeNight = 1;     // 이 밤엔
    public int guaranteeHour = 3;      // 이 시까지 창문에 한 번도 안 왔으면 → 창문으로 바로 보냄

    [Header("응시 게이지")]
    public float gazeFillSpeed = 1f;   // 응시 중 초당 채움
    public float gazeDrainSpeed = 2f;  // 놓쳤을 때 초당 감소(빠르게)
    public float gazeRequired = 4f;    // 필요 누적량

    [Header("armed 발동 (§14 변형 — 화면 전환 즉시 OR 자동)")]
    public float armedAutoDelay = 5f;  // armed 후 아무것도 안 해도 이 시간 지나면 자동 STRIKE1

    [Header("R3 환풍구 루트 (기획서 §2 — 가끔만 오는 변칙)")]
    [Tooltip("밤별 R3 확률 (인덱스 = 밤 숫자, [0]은 안 쓰는 칸). 루트 고를 때 이 확률로 R3, 아니면 나머지 루트 중 랜덤. 0 = 안 옴")]
    public float[] r3ChanceByNight = { 0f, 0f, 0f, 0.15f, 0.15f, 0.15f };
    [Tooltip("테스트용: 0~1이면 밤 상관없이 이 확률로 R3 (1 = 항상). -1 = 끔(평소)")]
    public float testR3ChanceOverride = -1f;
    [Tooltip("환풍구 자리 우선순위 — 현우(2)보다 낮게. 막히면 기다리지 않고 창문으로 돌아감")]
    public int ventPriority = 1;

    [Header("R3 셔터 판정 A (현우와 같은 방식, 숫자는 따로 조절)")]
    [Tooltip("셔터 열린 동안 이만큼 지나면 침투(armed)")]
    public float ventInfiltrationLimit = 15f;
    [Tooltip("셔터 닫고 이만큼 유지하면 격퇴")]
    public float ventHoldRequired = 5f;
    [Tooltip("셔터 닫힌 동안 초당 조기 이탈 확률 (FNAF2식 1/10)")]
    public float ventEarlyLeaveChance = 0.1f;

    [Header("엔진B 확률 이동 — 밤별 (인덱스 = 밤 숫자, [0]은 안 쓰는 칸)")]
    [Tooltip("기획서 §8: 1일 3 / 2일 5 / 3일 8 / 4일 11 / 5일 14. rand(0~19) < AI 이면 이동. 0 = 비활성")]
    public int[] aiByNight = { 0, 3, 5, 8, 11, 14 };
    [Tooltip("기획서 §8 이동판정 간격: 1일 8초 → 5일 4초")]
    public float[] cycleTimeByNight = { 0f, 8f, 7f, 6f, 5f, 4f };
    [Tooltip("테스트용: 0 이상이면 밤 상관없이 이 AI 사용. -1 = 끔(평소)")]
    public int testAiOverride = -1;

    public int AI => (testAiOverride >= 0) ? testAiOverride : GameManager.ByNight(aiByNight);
    float CycleTime => GameManager.ByNight(cycleTimeByNight);
    float WalkPathTime => GameManager.ByNight(walkPathTimeByNight);
    float R3Chance => (testR3ChanceOverride >= 0f) ? testR3ChanceOverride : GameManager.ByNight(r3ChanceByNight);

    [Header("행동 굴림 가중치")]
    public int weightForward = 85;   // 전진
    public int weightStay = 10;      // 제자리
    public int weightRetreat = 5;    // 후퇴

    [Header("참조")]
    public CCTVController cctv;         // CCTV 내림 여부 확인용
    public MonitorDisplay monitor;   // 스태틱 요청용
    public ScreenTransitionDetector transitionDetector;   // armed 중 화면 전환 = 즉시 STRIKE1
    public JumpscareOverlay jumpscare;
    public AudioSystem audioSystem;
    public ShutterController shutter;   // R3: 셔터 닫힘·불빛 상태 읽기 (비워두면 자동)

    [Header("상태 플래그")]
    public bool isAudioBroken = false;

    // 상태
    private enum State { Moving, WalkPath, VentJudge, Armed, Gone }
    private State state = State.Moving;

    private float currentWait = 0f;   // 이번 노드에서 기다릴 시간 (랜덤 뽑은 값)
    private int currentNode = 0;
    private float timer = 0f;
    private float walkTimer = 0f;       // 워크패스 경과
    private float gazeGauge = 0f;       // 응시 누적
    private bool beingGazed = false;    // 지금 GazeBox가 닿아있나 (Trigger가 갱신)
    private float armedTimer = 0f;      // armed 후 경과
    private bool walkedTonight = false; // 오늘 밤 창문 워크패스 한 번이라도 했나 (1일차 확정 출현용)

    // R3 환풍구
    private VentOccupancy vent;             // 환풍구 자리 (씬에 없으면 만듦)
    private VentLightReveal reveal;         // 관리실 환풍구에선 불빛에만 보이게
    private bool pendingVent = false;       // 자리는 잡았는데 불빛 켜져 있어서 도착 대기 중
    private bool loggedLightWait = false;   // "불빛 때문에 대기" 로그 한 번만
    private float ventInfiltrationTimer = 0f;   // 침투까지 (셔터 열린 동안 참)
    private float ventHoldTimer = 0f;           // 셔터 유지 (닫힌 동안 참)
    private float ventEarlyLeaveTimer = 0f;     // 조기 이탈 굴림용 (초당)

    // 지금 R3(환풍구 루트)인가 — 창문으로 돌아가면(R1으로 갈아타면) false
    bool OnVentRoute => nodes != null && nodes == routeR3 && routeR3.Length > 0;
    int VentNode => nodes.Length - 1;   // R3의 마지막 노드 = 관리실 환풍구

    void Start()
    {
        if (cctv == null) cctv = FindAnyObjectByType<CCTVController>();
        if (monitor == null) monitor = FindAnyObjectByType<MonitorDisplay>();
        if (audioSystem == null) audioSystem = FindAnyObjectByType<AudioSystem>();
        if (transitionDetector == null) transitionDetector = FindAnyObjectByType<ScreenTransitionDetector>();
        if (jumpscare == null) jumpscare = FindAnyObjectByType<JumpscareOverlay>();
        if (shutter == null) shutter = FindAnyObjectByType<ShutterController>();
        vent = VentOccupancy.GetOrCreate();

        // 관리실 환풍구에선 불빛에만 보임 (현우와 같은 방식) — 없으면 자동으로 붙임
        reveal = GetComponent<VentLightReveal>();
        if (reveal == null) reveal = gameObject.AddComponent<VentLightReveal>();
        reveal.shutter = shutter;

        SelectRoute();   // 루트 선택
        if (nodes.Length > 0) MoveToNode(0);

        // 화면 전환 방송 구독 — armed 중이면 즉시 STRIKE1
        if (transitionDetector != null)
            transitionDetector.OnScreenTransition += HandleScreenTransition;
    }

    void OnDestroy()
    {
        if (transitionDetector != null)
            transitionDetector.OnScreenTransition -= HandleScreenTransition;
    }

    void Update()
    {
        if (AI <= 0) return;   // AI 0 = 비활성 (오늘 밤 안 나옴)

        CheckGuarantee();

        switch (state)
        {
            case State.Moving: UpdateMoving(); break;
            case State.WalkPath: UpdateWalkPath(); break;
            case State.VentJudge: UpdateVentJudge(); break;
            case State.Armed: UpdateArmed(); break;
        }
    }

    // --- 노드 이동 ---
    void UpdateMoving()
    {
        // R3: 관리실 환풍구 자리는 잡았고, 플레이어가 불빛 끄는 순간 도착
        if (pendingVent)
        {
            // 아직 환풍구 밖(화장실)이라 안 보임 → 현우가 오면 바로 비켜줌
            if (vent != null && vent.ShouldYield(this))
            {
                YieldVent();
                return;
            }
            UpdatePendingVent();
            return;
        }

        if (currentNode >= nodes.Length - 1)
        {
            // (안전장치) R3 마지막 노드는 MoveToNode에서 판정을 시작하므로 여기 올 일 없음
            if (OnVentRoute) StartVentJudge();
            else StartWalkPath();
            return;
        }

        timer += Time.deltaTime;

        // 허공·착석·기립 = 랜덤 고정 시간
        if (currentNode < fixedNodeCount)
        {
            if (timer >= currentWait)
            {
                timer = 0f;
                Forward();
            }
        }
        // 그 이후 = 엔진B 확률 이동
        else
        {
            // 로비 잠복 중이면 타이머 다 될 때까지 안 움직임
            if (isLurking)
            {
                lurkTimer += Time.deltaTime;
                if (lurkTimer >= lurkDuration)
                {
                    isLurking = false;
                    Debug.Log("★ 현승 로비 잠복 종료 → 이동 재개");
                }
                return;
            }

            if (timer >= CycleTime)
            {
                timer = 0f;
                int roll = Random.Range(0, 20);
                if (roll < AI)
                    DoActionRoll();
            }
        }
    }

    // 한 칸 전진 — R3에서 다음이 관리실 환풍구면 바로 안 가고 자리부터 확인
    void Forward()
    {
        int next = currentNode + 1;
        if (OnVentRoute && next == VentNode)
        {
            TryEnterVent();
            return;
        }
        MoveToNode(next);
    }

    // 1일차 확정 출현: 정해진 시까지 창문에 한 번도 안 왔으면 창문 바로 앞(마지막 노드)으로 보냄 (스태틱 → 곧 워크패스)
    void CheckGuarantee()
    {
        if (walkedTonight || state != State.Moving) return;
        if (GameManager.Night != guaranteeNight || GameManager.Hour < guaranteeHour) return;
        if (nodes == null || nodes.Length == 0) return;

        Debug.Log($"★ 현승 1일차 확정 출현 ({guaranteeHour}시까지 창문에 안 와서 창문으로 보냄)");
        isLurking = false;

        // R3(환풍구)였으면 창문 루트(R1)로 갈아탐 — 확정 출현은 "창문 응시"를 배우는 것 (테스트로 1일차에 R3를 켰을 때 대비)
        if (OnVentRoute)
        {
            pendingVent = false;
            loggedLightWait = false;
            if (vent != null) vent.Release(this);
            nodes = routeR1;
        }

        MoveToNode(nodes.Length - 1);   // 다음 프레임 UpdateMoving이 워크패스 시작
    }

    // 그날 허용된 루트 중 하나를 랜덤 선택
    void SelectRoute()
    {
        // R3(환풍구)는 "가끔만" — 먼저 밤별 확률 주사위, 실패하면 나머지 루트 중 랜덤 (기획서 §2)
        if (routeR3 != null && routeR3.Length >= 2 && Random.value < R3Chance)
        {
            nodes = routeR3;
            Debug.Log("현승 루트 선택: R3 (환풍구)");
            return;
        }

        int night = GameManager.Night;
        var allowed = new System.Collections.Generic.List<Transform[]>();
        allowed.Add(routeR1);                                  // R1은 항상
        if (night >= 2 && routeR2 != null && routeR2.Length > 0)
            allowed.Add(routeR2);                              // 2밤부터 R2
        if (night >= 2 && routeR4 != null && routeR4.Length > 0)
            allowed.Add(routeR4);                              // 2밤부터 R4
        if (night >= 3 && routeR1p != null && routeR1p.Length > 0)
            allowed.Add(routeR1p);      // ← 3밤부터 로비 잠복

        nodes = allowed[Random.Range(0, allowed.Count)];

        // string routeName = nodes == routeR1 ? "R1"
        //                  : nodes == routeR2 ? "R2"
        //                  : nodes == routeR4 ? "R4(창고)"
        //                  : "R1'(로비잠복)";
        // Debug.Log($"현승 루트 선택: {routeName} (밤 {night})");
    }

    void MoveToNode(int index)
    {
        currentNode = index;
        transform.position = nodes[index].position;
        transform.rotation = nodes[index].rotation;

        bool atRoomVent = OnVentRoute && index == VentNode;
        if (reveal != null) reveal.inRoomVent = atRoomVent;   // 관리실 환풍구에선 불빛에만 보임
        Debug.Log($"현승 이동 → 노드 {index} ({nodes[index].name})");   // 이동할 때만 (매 프레임 아님)

        // 허공(0)은 안 보이니 스태틱 X. 노드1(착석)부터 = 등장 지직. (관리실 환풍구 도착 = 화장실에서 사라지는 순간도 지직 — 현우와 같음)
        if (index > 0)
        {
            if (monitor != null) monitor.GhostMoveStatic();
        }

        // 창고 노드면 와당탕 소리 (CAM07 소리만)
        if (storageNode != null && nodes[index] == storageNode)
        {
            Debug.Log("와장창소리");
            // TODO: phase 8 소리 재생 시스템
        }

        // 로비 도착 → 잠복 시작
        if (lobbyNode != null && nodes[index] == lobbyNode)
        {
            isLurking = true;
            lurkTimer = 0f;
            lurkDuration = Random.Range(lobbyLurkMin, lobbyLurkMax);
            Debug.Log($"★ 현승 로비 잠복 시작 ({lurkDuration:F1}초)");
        }

        // 고정 노드(허공/착석/기립)면 배열에서 랜덤 대기시간
        if (index < fixedNodeCount && index < fixedNodeWaits.Length)
            currentWait = Random.Range(fixedNodeWaits[index].x, fixedNodeWaits[index].y);

        // 관리실 환풍구 도착 = 바로 셔터 판정 시작
        if (atRoomVent) StartVentJudge();

        // TODO: 이동 사운드 / 착석·기립 애니
    }

    // --- R3: 관리실 환풍구 들어가기 ---
    // 자리(VentOccupancy)부터 확인 → 막혔으면 창문으로 돌아감 / 비었으면 자리 잡고 불빛 확인
    void TryEnterVent()
    {
        if (vent != null && !vent.TryEnter(this, ventPriority, false))   // false = 기다리지 않음
        {
            string why = (vent.Occupant != null) ? $"환풍구 막힘 ({vent.Occupant.name} 사용 중)"
                                                 : "환풍구 막힘 (비운 뒤 간격 / 현우 대기 중)";
            DetourToWindow(why);   // 돌아갈 길 없으면 이번엔 제자리 (다음 굴림에 다시 시도)
            return;
        }

        pendingVent = true;
        UpdatePendingVent();   // 바로 한 번 확인 (불빛 안 보고 있으면 이번 프레임에 도착)
    }

    // 자리는 잡았음 — 플레이어가 불빛으로 보고 있으면 끄는 순간 도착 ("눈앞에서 나타나면 안 됨", 현우와 같음)
    void UpdatePendingVent()
    {
        if (VentLightReveal.PlayerSeesVent(shutter))
        {
            if (!loggedLightWait)
            {
                loggedLightWait = true;
                Debug.Log("현승: 불빛 켜져 있어서 환풍구 도착 대기 (끄는 순간 도착)");
            }
            return;
        }

        pendingVent = false;
        loggedLightWait = false;
        MoveToNode(VentNode);
    }

    // 창문으로 돌아감: R3의 화장실 노드(관리실 환풍구 바로 앞)를 R1에서 찾아서 R1 뒷부분으로 갈아탐
    // (화장실 → 세로복도 → 창문). R3 화장실 노드가 R1에 없으면 돌아갈 길이 없음 → false
    bool DetourToWindow(string why)
    {
        int bathIndex = VentNode - 1;   // ⚠️ nodes가 아직 R3일 때 계산
        Transform bath = (bathIndex >= 0) ? nodes[bathIndex] : null;
        int idx = (routeR1 != null && bath != null) ? System.Array.IndexOf(routeR1, bath) : -1;
        if (idx < 0 || idx >= routeR1.Length - 1)
        {
            Debug.Log($"현승: {why} — 그런데 돌아갈 길 없음 (R3 화장실 노드가 R1에 없음)");
            return false;
        }

        Debug.Log($"★ 현승: {why} → 창문으로 돌아감");
        nodes = routeR1;
        MoveToNode(idx + 1);
        return true;
    }

    // 현우가 와서 환풍구를 비켜줌 → 창문으로 (현우 우선, 유저 요청)
    // 부르는 곳: 화장실에서 불빛 대기 중(안 보임) / 환풍구 판정 중 + 플레이어가 못 봄(불빛 꺼짐 또는 셔터 닫힘 — 격퇴랑 똑같이 보임)
    // armed면 안 부름 — 침투 성공한 벌(STRIKE1)은 그대로 받음 (현우가 와도 공짜로 빠져나갈 수 없게)
    void YieldVent()
    {
        pendingVent = false;
        loggedLightWait = false;
        state = State.Moving;
        timer = 0f;

        bool moved = DetourToWindow("현우가 와서 환풍구 비켜줌");   // nodes가 R3일 때 먼저 (화장실 노드 찾기)
        if (vent != null) vent.Release(this);                       // 자리 넘김 (비운 뒤 간격 7초 뒤 현우 들어옴)
        if (!moved) ResetToStart();                                 // 돌아갈 길 없으면 처음으로
    }

    // --- R3: 셔터 판정 A (현우와 같은 방식) ---
    void StartVentJudge()
    {
        state = State.VentJudge;
        ventInfiltrationTimer = 0f;
        ventHoldTimer = 0f;
        ventEarlyLeaveTimer = 0f;
        Debug.Log("★ 현승 관리실 환풍구 도착! 셔터 판정 시작 (R3)");
    }

    void UpdateVentJudge()
    {
        // 현우 우선: 현우가 오면 비켜줌 — 단 플레이어가 환풍구를 보고 있으면(불빛 켜짐 + 셔터 열림) 끌 때까지 그대로
        if (vent != null && vent.ShouldYield(this) && !VentLightReveal.PlayerSeesVent(shutter))
        {
            YieldVent();
            return;
        }

        bool shutterClosed = (shutter != null) && shutter.isShutterClosed;

        if (shutterClosed)
        {
            // 셔터 닫힘: 침투타이머 정지(그대로), 유지타이머 참
            ventHoldTimer += Time.deltaTime;

            // 조기 이탈 굴림 (초당 1회)
            ventEarlyLeaveTimer += Time.deltaTime;
            if (ventEarlyLeaveTimer >= 1f)
            {
                ventEarlyLeaveTimer = 0f;
                if (Random.value < ventEarlyLeaveChance)
                {
                    VentRepel("조기 이탈(운)");
                    return;
                }
            }

            // 유지 성공 → 격퇴
            if (ventHoldTimer >= ventHoldRequired)
            {
                VentRepel($"{ventHoldRequired:F0}초 유지 성공");
                return;
            }
        }
        else
        {
            // 셔터 열림: 유지타이머 리셋, 침투타이머 참
            ventHoldTimer = 0f;
            ventEarlyLeaveTimer = 0f;

            ventInfiltrationTimer += Time.deltaTime;
            if (ventInfiltrationTimer >= ventInfiltrationLimit)
            {
                // 창문과 같은 armed — 화면 전환 즉시 OR armedAutoDelay 뒤 자동 STRIKE1
                state = State.Armed;
                armedTimer = 0f;
                Debug.Log($"현승 ARMED (환풍구 침투) — 화면 전환 시 즉시 / 안 해도 {armedAutoDelay:F0}초 뒤 STRIKE1");
            }
        }
    }

    // 격퇴: 셔터 닫혀 있어서 사라지는 게 안 보임 → 처음으로 (루트 다시 뽑음)
    void VentRepel(string reason)
    {
        Debug.Log($"★ 현승 환풍구 격퇴! 쿵쿵 ({reason})");
        // TODO: 쿵쿵 사운드 (Phase 8 — 비운 뒤 간격 7초가 이 소리 자리)
        ResetToStart();
    }

    // --- 창문 워크패스 ---
    void StartWalkPath()
    {
        state = State.WalkPath;
        walkTimer = 0f;
        gazeGauge = 0f;
        walkedTonight = true;
    }

    void UpdateWalkPath()
    {
        walkTimer += Time.deltaTime;
        float t = walkTimer / WalkPathTime;
        if (windowStart != null && windowEnd != null)
            transform.position = Vector3.Lerp(windowStart.position, windowEnd.position, t);

        bool cctvDown = (cctv == null) || !cctv.isCameraDown;
        bool gazingNow = beingGazed && cctvDown;

        if (gazingNow)
            gazeGauge += gazeFillSpeed * Time.deltaTime;
        else
            gazeGauge -= gazeDrainSpeed * Time.deltaTime;
        gazeGauge = Mathf.Clamp(gazeGauge, 0f, gazeRequired);

        if (gazeGauge >= gazeRequired)
        {
            Disappear();
        }
        else if (walkTimer >= WalkPathTime)
        {
            state = State.Armed;
            armedTimer = 0f;
            Debug.Log($"현승 ARMED (화면 전환 시 즉시 / 안 해도 {armedAutoDelay:F0}초 뒤 STRIKE1)");
        }
    }

    // --- armed: 둘 중 먼저 오는 쪽으로 STRIKE1 (창문·환풍구 공용) ---
    //  ① 화면 전환(CCTV·패널 올림/내림, 환기 흐려짐) → 즉시 (HandleScreenTransition)
    //  ② 아무것도 안 해도 armedAutoDelay 지나면 자동 → "가만히 버티면 안 터지는" 구멍 없음
    // CCTV/패널 보고 있었으면 jumpscare.Play()가 강제로 정면 복귀시킴.
    void UpdateArmed()
    {
        armedTimer += Time.deltaTime;
        if (armedTimer >= armedAutoDelay)
        {
            Debug.Log("현승 armed 시간 초과 → 자동 STRIKE1");
            Strike1();
        }
    }

    void HandleScreenTransition()
    {
        if (!enabled) return;
        if (state == State.Armed)
            Strike1();
    }

    void Disappear()
    {
        Debug.Log("현승 응시 성공 → 공부방1 복귀");
        ResetToStart();
    }

    void ResetToStart()
    {
        state = State.Moving;
        currentNode = 0;
        timer = 0f;
        walkTimer = 0f;
        gazeGauge = 0f;

        isLurking = false;
        lurkTimer = 0f;

        // R3: 사건 끝 → 환풍구 자리 비움 (환풍구에 없었어도 안전)
        pendingVent = false;
        loggedLightWait = false;
        if (vent != null) vent.Release(this);

        SelectRoute();
        MoveToNode(0);
    }

    void Strike1()
    {
        Debug.Log("★★ 현승 STRIKE1! 점프스케어 → 오디오 고장");
        if (jumpscare != null) jumpscare.Play();
        if (audioSystem != null) audioSystem.BreakAudio();
        ResetToStart();
    }

    public void Strike2()
    {
        Debug.Log("☠☠☠ 현승 STRIKE2 - 퍼펫 즉사! 게임오버");
        if (jumpscare != null)
            jumpscare.PlayGameOver();

        if (GameManager.Instance != null)
            GameManager.Instance.GameOver("현승퍼펫");
    }

    void DoActionRoll()
    {
        int total = weightForward + weightStay + weightRetreat;
        int r = Random.Range(0, total);

        if (r < weightForward)
        {
            Forward();                                    // 전진 (R3 환풍구 앞이면 자리부터 확인)
        }
        else if (r < weightForward + weightStay)
        {
            // 멈춤
        }
        else
        {
            // 후퇴 (허공/착석/기립 구간으로는 안 돌아감)
            if (currentNode > fixedNodeCount)
                MoveToNode(currentNode - 1);
        }
    }

    public void SetGazed(bool value)
    {
        beingGazed = value;
    }
}

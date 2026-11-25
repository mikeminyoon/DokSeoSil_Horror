using UnityEngine;

// 현승: 노드 이동 → 창문 워크패스 → 응시 대응
public class Hyunsoong : MonoBehaviour
{
    [Header("루트 (각각 노드 배열)")]
    public Transform[] routeR1;    // [허공,착석,기립,가로복도,세로복도상단,화장실,창문]
    public Transform[] routeR2;    // [허공,착석,기립,공부방2,가로복도,세로복도상단,화장실,창문]
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

    // 날짜는 GameManager.Night에서 직접 읽음 (자기 필드 없음). 1밤=R1만 / 2밤~=R1·R2·R4 / 3밤~=+R1'
    private Transform[] nodes;

    [Header("창고 노드 (소리 판별용)")]
    public Transform storageNode;  // ← Node_창고 연결

    [Header("등장 연출 (허공·착석·기립) 대기시간")]
    public int fixedNodeCount = 3;   // 허공/착석/기립
    public Vector2[] fixedNodeWaits = {
        new Vector2(10f, 30f),   // 노드0: 허공 (등장 대기)
        new Vector2(10f, 20f),   // 노드1: 착석
        new Vector2(3f, 7f),     // 노드2: 기립
    };

    [Header("창문 워크패스")]
    public Transform windowStart;      // 창문 왼쪽 끝
    public Transform windowEnd;        // 창문 오른쪽 끝
    [Tooltip("밤별 워크패스 총 시간(초) = 응시 대응 시간. [0]은 안 쓰는 칸. 기획서: 1일 김 → 5일 가장 짧음 (숫자 미정이라 지금 값 6초로 채움)")]
    public float[] walkPathTimeByNight = { 0f, 6f, 6f, 6f, 6f, 6f };

    [Header("1일차 확정 출현 (기획서 §7 — 모든 플레이어가 응시를 한 번은 배움)")]
    public int guaranteeNight = 1;     // 이 밤엔
    public int guaranteeHour = 3;      // 이 시까지 창문에 한 번도 안 왔으면 → 창문으로 바로 보냄

    [Header("응시 게이지")]
    public float gazeFillSpeed = 1f;   // 응시 중 초당 채움
    public float gazeDrainSpeed = 2f;  // 놓쳤을 때 초당 감소(빠르게)
    public float gazeRequired = 4f;    // 필요 누적량

    [Header("armed 발동 (§14 변형 — 화면 전환 즉시 OR 자동)")]
    public float armedAutoDelay = 5f;  // armed 후 아무것도 안 해도 이 시간 지나면 자동 STRIKE1

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

    [Header("상태 플래그")]
    public bool isAudioBroken = false;

    // 상태
    private enum State { Moving, WalkPath, Armed, Gone }
    private State state = State.Moving;

    private float currentWait = 0f;   // 이번 노드에서 기다릴 시간 (랜덤 뽑은 값)
    private int currentNode = 0;
    private float timer = 0f;
    private float walkTimer = 0f;       // 워크패스 경과
    private float gazeGauge = 0f;       // 응시 누적
    private bool beingGazed = false;    // 지금 GazeBox가 닿아있나 (Trigger가 갱신)
    private float armedTimer = 0f;      // armed 후 경과
    private bool walkedTonight = false; // 오늘 밤 창문 워크패스 한 번이라도 했나 (1일차 확정 출현용)

    void Start()
    {
        SelectRoute();   // 루트 선택
        if (nodes.Length > 0) MoveToNode(0);
        if (cctv == null) cctv = FindAnyObjectByType<CCTVController>();
        if (monitor == null) monitor = FindAnyObjectByType<MonitorDisplay>();
        if (audioSystem == null) audioSystem = FindAnyObjectByType<AudioSystem>();
        if (transitionDetector == null) transitionDetector = FindAnyObjectByType<ScreenTransitionDetector>();
        if (jumpscare == null) jumpscare = FindAnyObjectByType<JumpscareOverlay>();

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
            case State.Armed: UpdateArmed(); break;
        }
    }

    // --- 노드 이동 ---
    void UpdateMoving()
    {
        if (currentNode >= nodes.Length - 1)
        {
            StartWalkPath();
            return;
        }

        timer += Time.deltaTime;

        // 허공·착석·기립 = 랜덤 고정 시간
        if (currentNode < fixedNodeCount)
        {
            if (timer >= currentWait)
            {
                timer = 0f;
                MoveToNode(currentNode + 1);
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

    // 1일차 확정 출현: 정해진 시까지 창문에 한 번도 안 왔으면 창문 바로 앞(마지막 노드)으로 보냄 (스태틱 → 곧 워크패스)
    void CheckGuarantee()
    {
        if (walkedTonight || state != State.Moving) return;
        if (GameManager.Night != guaranteeNight || GameManager.Hour < guaranteeHour) return;
        if (nodes == null || nodes.Length == 0) return;

        Debug.Log($"★ 현승 1일차 확정 출현 ({guaranteeHour}시까지 창문에 안 와서 창문으로 보냄)");
        isLurking = false;
        MoveToNode(nodes.Length - 1);   // 다음 프레임 UpdateMoving이 워크패스 시작
    }

    // 그날 허용된 루트 중 하나를 랜덤 선택
    void SelectRoute()
    {
        int night = GameManager.Night;
        var allowed = new System.Collections.Generic.List<Transform[]>();
        allowed.Add(routeR1);                                  // R1은 항상
        if (night >= 2 && routeR2 != null && routeR2.Length > 0)
            allowed.Add(routeR2);                              // 2밤부터 R2
        if (night >= 2 && routeR4 != null && routeR4.Length > 0)
            allowed.Add(routeR4);                              // 2밤부터 R4
        if (night >= 3 && routeR1p != null && routeR1p.Length > 0)
            allowed.Add(routeR1p);      // ← 3밤부터 로비 잠복
        // TODO: 3밤~ R3(환풍구)

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

        // 허공(0)은 안 보이니 스태틱 X. 노드1(착석)부터 = 등장 지직.
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

        // TODO: 이동 사운드 / 착석·기립 애니
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

    // --- armed: 둘 중 먼저 오는 쪽으로 STRIKE1 ---
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
            MoveToNode(currentNode + 1);                  // 전진
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
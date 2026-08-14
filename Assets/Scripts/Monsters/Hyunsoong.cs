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

    [Header("날짜 (나중에 GameManager가 세팅)")]
    public int currentNight = 1;   // 1밤=R1만 / 2밤~=R1·R2
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
    public float walkPathTime = 6f;    // 워크패스 총 시간(빡세게 6초)

    [Header("응시 게이지")]
    public float gazeFillSpeed = 1f;   // 응시 중 초당 채움
    public float gazeDrainSpeed = 2f;  // 놓쳤을 때 초당 감소(빠르게)
    public float gazeRequired = 4f;    // 필요 누적량

    [Header("엔진B 확률 이동")]
    public float cycleTime = 5f;    // 판정 주기(초). 2배 느리게 하려면 늘려
    public int aiLevel = 3;         // AI 레벨 (1밤=3). rand(0~19) < aiLevel 이면 이동

    [Header("행동 굴림 가중치")]
    public int weightForward = 85;   // 전진
    public int weightStay = 10;      // 제자리
    public int weightRetreat = 5;    // 후퇴

    [Header("참조")]
    public CCTVController cctv;         // CCTV 내림 여부 확인용
    public MonitorDisplay monitor;   // 스태틱 요청용
    public ScreenTransitionDetector transitionDetector;
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

    void Start()
    {
        SelectRoute();   // 루트 선택
        if (nodes.Length > 0) MoveToNode(0);
        if (cctv == null) cctv = FindAnyObjectByType<CCTVController>();
        if (monitor == null) monitor = FindAnyObjectByType<MonitorDisplay>();
        if (audioSystem == null) audioSystem = FindAnyObjectByType<AudioSystem>();
        if (transitionDetector == null) transitionDetector = FindAnyObjectByType<ScreenTransitionDetector>();
        if (jumpscare == null) jumpscare = FindAnyObjectByType<JumpscareOverlay>();

        // 화면 전환 방송 구독 (§0.5)
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
        switch (state)
        {
            case State.Moving: UpdateMoving(); break;
            case State.WalkPath: UpdateWalkPath(); break;
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

            if (timer >= cycleTime)
            {
                timer = 0f;
                int roll = Random.Range(0, 20);
                if (roll < aiLevel)
                    DoActionRoll();
            }
        }
    }

    // 그날 허용된 루트 중 하나를 랜덤 선택
    void SelectRoute()
    {
        var allowed = new System.Collections.Generic.List<Transform[]>();
        allowed.Add(routeR1);                                  // R1은 항상
        if (currentNight >= 2 && routeR2 != null && routeR2.Length > 0)
            allowed.Add(routeR2);                              // 2밤부터 R2
        if (currentNight >= 2 && routeR4 != null && routeR4.Length > 0)
            allowed.Add(routeR4);                              // 2밤부터 R4
        if (currentNight >= 3 && routeR1p != null && routeR1p.Length > 0)
            allowed.Add(routeR1p);      // ← 3밤부터 로비 잠복
        // TODO: 3밤~ R3(환풍구)

        nodes = allowed[Random.Range(0, allowed.Count)];

        string routeName = nodes == routeR1 ? "R1"
                         : nodes == routeR2 ? "R2"
                         : nodes == routeR4 ? "R4(창고)"
                         : "R1'(로비잠복)";
        Debug.Log($"현승 루트 선택: {routeName} (밤 {currentNight})");
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
    }

    void UpdateWalkPath()
    {
        walkTimer += Time.deltaTime;
        float t = walkTimer / walkPathTime;
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
        else if (walkTimer >= walkPathTime)
        {
            state = State.Armed;
            Debug.Log("현승 ARMED (다음 화면 전환 시 STRIKE1)");
        }
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

    void HandleScreenTransition()
    {
        if (state == State.Armed)
            Strike1();
    }
}
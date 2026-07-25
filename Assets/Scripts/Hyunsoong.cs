using UnityEngine;

// 현승: 노드 이동 → 창문 워크패스 → 응시 대응
public class Hyunsoong : MonoBehaviour
{
    [Header("루트 (각각 노드 배열)")]
    public Transform[] routeR1;    // [착석,기립,가로복도,세로복도상단,화장실,창문]
    public Transform[] routeR2;    // [착석,기립,공부방2,가로복도,세로복도상단,화장실,창문]
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
    private Transform[] nodes;          // [착석,기립,가로복도,세로복도상단,화장실,창문]

    [Header("창고 노드 (소리 판별용)")]
    public Transform storageNode;  // ← Node_창고 연

    [Header("등장 연출 (착석·기립) 대기시간 범위")]
    public int fixedNodeCount = 2;
    public Vector2 sitTimeRange = new Vector2(10f, 20f);    // 착석: 최소~최대
    public Vector2 standTimeRange = new Vector2(3f, 7f);    // 기립: 최소~최대

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
    public int weightRetreat = 5;   // 후퇴

    [Header("참조")]
    public CCTVController cctv;         // CCTV 내림 여부 확인용
    public MonitorDisplay monitor;   // 스태틱 요청용
    public ScreenTransitionDetector transitionDetector;   
    public JumpscareOverlay jumpscare; //ㄱㅏㅂㅌㅜㄱㅌㅜㅣ
    public AudioSystem audioSystem; //예아

    [Header("상태 플래그")]
    public bool isAudioBroken = false; //(오디오 고장, 지금은 플래그만)

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

    // 오브젝트 사라질 때 구독 해제 (필수! 안 하면 에러)
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
                // Armed, Gone은 나중(점프스케어/복귀)에서 처리
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

        // 착석(0)·기립(1) = 랜덤 고정 시간
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
                return;   // 잠복 중엔 확률 이동 안 함
            }

            //잠복 아니면 다시 엔진대로 이동
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
        // 허용 루트 목록 만들기 (날짜별)
        var allowed = new System.Collections.Generic.List<Transform[]>();
        allowed.Add(routeR1);                                  // R1은 항상
        if (currentNight >= 2 && routeR2 != null && routeR2.Length > 0)
            allowed.Add(routeR2);                              // 2밤부터 R2
        if(currentNight >= 2 && routeR4 != null && routeR4.Length > 0)
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

        // 이동 시 CCTV 스태틱 (FNAF식 - 뭔가 움직였다는 신호)
        if (monitor != null) monitor.GhostMoveStatic();

        // 이번 노드의 대기시간을 랜덤으로 뽑음 (착석·기립만) + Cam7이면 와당탕 소리?
        if (storageNode != null && nodes[index] == storageNode)
        {
            Debug.Log("와장창소리");
            //todo: phase 8 소리 재생 시스템
        }

        // 로비 도착 → 잠복 시작
        if (lobbyNode != null && nodes[index] == lobbyNode)
        {
            isLurking = true;
            lurkTimer = 0f;
            lurkDuration = Random.Range(lobbyLurkMin, lobbyLurkMax);
            Debug.Log($"★ 현승 로비 잠복 시작 ({lurkDuration:F1}초)");
        }

        if (index == 0)
            currentWait = Random.Range(sitTimeRange.x, sitTimeRange.y);      // 착석
        else if (index == 1)
            currentWait = Random.Range(standTimeRange.x, standTimeRange.y);  // 기립

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
        // 1) 캡슐이 창문 앞을 가로질러 이동 (start→end)
        walkTimer += Time.deltaTime;
        float t = walkTimer / walkPathTime;   // 0~1 진행도
        if (windowStart != null && windowEnd != null)
            transform.position = Vector3.Lerp(windowStart.position, windowEnd.position, t);

        // 2) 응시 판정: GazeBox 닿음(beingGazed) + CCTV 내린 상태
        bool cctvDown = (cctv == null) || !cctv.isCameraDown; // CCTV 안 올림 = 창문 보임
        bool gazingNow = beingGazed && cctvDown;
        //Debug.Log($"워크패스 | beingGazed:{beingGazed} | cctvDown:{cctvDown} | 게이지:{gazeGauge:F1}");

        if (gazingNow)
            gazeGauge += gazeFillSpeed * Time.deltaTime;       // 채움
        else
            gazeGauge -= gazeDrainSpeed * Time.deltaTime;      // 빠르게 감소
        gazeGauge = Mathf.Clamp(gazeGauge, 0f, gazeRequired);

        // 3) 결과
        if (gazeGauge >= gazeRequired)
        {
            Disappear();          // 응시 성공 → 사라짐
        }
        else if (walkTimer >= walkPathTime)
        {
            state = State.Armed;  // 워크패스 끝났는데 못 채움 → armed
            // TODO: 다음 화면 전환 시 STRIKE1 (다음 조각에서)
            Debug.Log("현승 ARMED (다음 화면 전환 시 STRIKE1)");
        }
    }

    // 응시 성공 → 물러남
    void Disappear()
    {
        Debug.Log("현승 응시 성공 → 공부방1 복귀");
        ResetToStart();
    }

    // 공부방1로 리셋 (응시 성공/STRIKE 공용)
    void ResetToStart()
    {
        state = State.Moving;
        currentNode = 0;
        timer = 0f;
        walkTimer = 0f;
        gazeGauge = 0f;

        isLurking = false;      // ← 로비잠복중인가?
        lurkTimer = 0f;         // ← 로비잠복

        SelectRoute();   // 루트 재선택
        MoveToNode(0);
    }

    // STRIKE1: 팬텀 잔상 + 오디오 고장 + 복귀 (비즉사)
    void Strike1()
    {
        Debug.Log("★★ 현승 STRIKE1! 점프스케어 → 오디오 고장");
        if (jumpscare != null) jumpscare.Play();

        // 오디오 고장
        if (audioSystem != null) audioSystem.BreakAudio();

        ResetToStart();
    }

    // STRIKE2: 퍼펫 즉사 (게임오버). 오디오 고장 방치 시.
    public void Strike2()
    {
        Debug.Log("☠☠☠ 현승 STRIKE2 - 퍼펫 즉사! 게임오버");

        // 퍼펫 즉사 점프스케어 (임시: JumpscareOverlay를 게임오버 모드로)
        if (jumpscare != null)
            jumpscare.PlayGameOver();

        // TODO: GameManager.GameOver() — 지금은 오버레이가 조작 영구 잠금
    }

    // 행동 굴림: 전진 / 멈춤 / 후퇴
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
            // 멈춤 — 그 자리 유지
        }
        else
        {
            // 후퇴 (단, 착석·기립 구간으로는 안 돌아감)
            if (currentNode > fixedNodeCount)
                MoveToNode(currentNode - 1);
            // 이미 최소 노드면 멈춤 취급
        }
    }
    
    // --- GazeBox가 호출 (Trigger 감지) ---
    public void SetGazed(bool value)
    {
        beingGazed = value;
    }

    // 화면 전환 방송을 받았을 때 (§0.5)
    void HandleScreenTransition()
    {
        // armed 상태일 때만 반응 → STRIKE1
        if (state == State.Armed)
            Strike1();
    }
}
using UnityEngine;

// 연호: 메인 빌런 (엔진A). 등장 → 경로(엔진A) → 창문앞 → 빼꼼(화면전환 즉사).
// 종 대응: CCTV 현재 방에 연호 있으면 종으로 후퇴 (무시 1/7, 쿨, 배터리, 반응 딜레이).
public class Yeonho : MonoBehaviour
{
    [Header("경로 노드 (공부방2→가로복도→로비→세로복도)")]
    public Transform[] nodes;

    [Header("각 노드의 방 번호 (roomFeeds 인덱스)")]
    public int[] nodeRoomIndex;   // 예: [1,2,4,5]

    [Header("대치 위치")]
    public Transform windowFrontPos;
    public Transform peekPos;

    [Header("등장")]
    public GameObject model;
    public float spawnDelayMin = 10f;
    public float spawnDelayMax = 25f;

    [Header("엔진A")]
    public int aiLevel = 1;
    public int currentNight = 1;

    [Header("행동 굴림 가중치")]
    public int weightForward = 60;
    public int weightRetreat = 20;
    public int weightStay = 20;

    [Header("창문 앞 머무르는 시간")]
    public float windowFrontTime = 6f;

    [Header("종 (연호 대응)")]
    public float bellCooldown = 5.4f;
    public float bellDrain = 8f;
    public int ignoreChance = 7;
    public int windowFrontRetreat = 2;

    [Header("종 반응 (종소리 + 랜덤 텀 후 후퇴)")]
    public float bellSoundDuration = 1.0f;   // 종소리 재생 시간 (나중에 실제 사운드 길이)
    public float bellReactMin = 0.2f;        // 추가 랜덤 텀 (최소)
    public float bellReactMax = 0.6f;        // (최대)

    [Header("참조")]
    public MonitorDisplay monitor;
    public ScreenTransitionDetector transitionDetector;
    public JumpscareOverlay jumpscare;

    // 상태
    private enum State { Spawning, Moving, WindowFront, Peeking }
    private State state = State.Spawning;

    private float spawnTimer = 0f;
    private float spawnWait = 0f;

    private int currentNode = 0;
    private float moveCounter = 0f;
    private int totalTurns = 0;
    private float secondTimer = 0f;
    private float confrontTimer = 0f;

    private float bellCooldownTimer = 0f;

    // 종 반응 예약
    private float retreatTimer = -1f;
    private int retreatTarget = -1;

    void Start()
    {
        spawnWait = Random.Range(spawnDelayMin, spawnDelayMax);
        if (model != null) model.SetActive(false);
        if (monitor == null) monitor = FindAnyObjectByType<MonitorDisplay>();
        if (transitionDetector == null) transitionDetector = FindAnyObjectByType<ScreenTransitionDetector>();
        if (jumpscare == null) jumpscare = FindAnyObjectByType<JumpscareOverlay>();

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
        // 종 쿨 감소
        if (bellCooldownTimer > 0f) bellCooldownTimer -= Time.deltaTime;

        // 종 후퇴 예약 처리 (종소리 + 텀 후 후퇴)
        if (retreatTimer > 0f)
        {
            retreatTimer -= Time.deltaTime;
            if (retreatTimer <= 0f) DoRetreat();
        }

        switch (state)
        {
            case State.Spawning:    UpdateSpawning();    break;
            case State.Moving:      UpdateMoving();      break;
            case State.WindowFront: UpdateWindowFront(); break;
        }
    }

    // === 등장 ===
    void UpdateSpawning()
    {
        spawnTimer += Time.deltaTime;
        if (spawnTimer >= spawnWait)
        {
            state = State.Moving;
            if (model != null) model.SetActive(true);
            MoveToNode(0);
            Debug.Log("★ 연호 등장 (공부방2)");
        }
    }

    // === 경로 이동 (엔진A) ===
    void UpdateMoving()
    {
        // 종 반응 중이면 엔진 멈춤 (멈칫)
        if (retreatTimer > 0f) return;

        secondTimer += Time.deltaTime;
        if (secondTimer >= 1f)
        {
            secondTimer -= 1f;
            TickSecond();
        }
    }

    void TickSecond()
    {
        moveCounter += 1f;
        int threshold = (10 - aiLevel) + Random.Range(1, 16) - totalTurns;
        if (moveCounter > threshold)
        {
            moveCounter = 0f;
            DoActionRoll();
        }
    }

    void DoActionRoll()
    {
        int total = weightForward + weightRetreat + weightStay;
        int r = Random.Range(0, total);

        if (r < weightStay)
            totalTurns += 1;
        else if (r < weightStay + weightRetreat)
        {
            totalTurns = 0;
            if (currentNode > 0) MoveToNode(currentNode - 1);
        }
        else
        {
            totalTurns = 0;
            MoveToNode(currentNode + 1);
        }
    }

    void MoveToNode(int index)
    {
        if (index >= nodes.Length)
        {
            EnterWindowFront();
            return;
        }
        if (index < 0) return;

        currentNode = index;
        transform.position = nodes[index].position;
        transform.rotation = nodes[index].rotation;
        if (monitor != null) monitor.GhostMoveStatic();
    }

    // === 창문앞 (대치) ===
    void EnterWindowFront()
    {
        state = State.WindowFront;
        confrontTimer = 0f;
        if (windowFrontPos != null)
        {
            transform.position = windowFrontPos.position;
            transform.rotation = windowFrontPos.rotation;
        }
        Debug.Log("★★ 연호 창문앞! 종 칠 마지막 기회");
    }

    void UpdateWindowFront()
    {
        // 종 반응 중이면 조임 정지
        if (retreatTimer > 0f) return;

        confrontTimer += Time.deltaTime;
        if (confrontTimer >= windowFrontTime)
            EnterPeeking();
    }

    // === 빼꼼 (화면전환 시 즉사) ===
    void EnterPeeking()
    {
        state = State.Peeking;
        if (peekPos != null)
        {
            transform.position = peekPos.position;
            transform.rotation = peekPos.rotation;
        }
        Debug.Log("★★★ 연호 빼꼼! (화면 전환 시 즉사)");
    }

    void HandleScreenTransition()
    {
        if (state == State.Peeking)
            Kill();
    }

    void Kill()
    {
        Debug.Log("☠☠☠ 연호 즉사! 게임오버");
        if (jumpscare != null) jumpscare.PlayGameOver();
        if (GameManager.Instance != null)
            GameManager.Instance.GameOver("연호");
    }

    // === 종 (버튼이 호출) ===
    public void RingBell(int room)
    {
        // 쿨 중이면 무시
        if (bellCooldownTimer > 0f)
        {
            Debug.Log("종 쿨타임 중");
            return;
        }

        // 쿨·배터리는 헛방이어도 소모
        bellCooldownTimer = bellCooldown;
        if (BatteryManager.Instance != null) BatteryManager.Instance.Drain(bellDrain);

        // 창문앞: 무시 없음, 강하게 후퇴 (반응 딜레이 후)
        if (state == State.WindowFront)
        {
            Debug.Log("★ 종! 연호 창문앞에서 반응 중...");
            state = State.Moving;
            int back = Mathf.Max(0, nodes.Length - windowFrontRetreat);
            ScheduleRetreat(back);
            return;
        }

        if (state == State.Peeking) return;

        // 경로 중 (Moving): 연호가 지금 보는 방에 있나?
        if (state == State.Moving)
        {
            int myRoom = (currentNode < nodeRoomIndex.Length) ? nodeRoomIndex[currentNode] : -1;

            if (myRoom == room)
            {
                // 무시 1/7 굴림
                if (Random.Range(0, ignoreChance) == 0)
                {
                    Debug.Log("종 무시됨! (1/7) — 안 밀림");
                    return;
                }
                Debug.Log($"★ 종! 연호 반응 중... (방 {room})");
                ScheduleRetreat(currentNode - 1);
            }
            else
            {
                Debug.Log($"종 헛방 (방 {room}, 연호는 방 {myRoom})");
            }
        }
    }

    public bool EmergencyPush()
    {
        //빼곰상태인지 확인
        if (state == State.Peeking) return false;

        //종 적용되면 스폰대기상태로
        Debug.Log("★★ 비상 종! 연호 완전 후퇴 (재등장 대기)");
        model?.SetActive(false);
        state = State.Spawning;
        spawnTimer = 0f;
        spawnWait = Random.Range(spawnDelayMin, spawnDelayMax);
        currentNode = 0;
        moveCounter = 0f;
        totalTurns = 0;
        secondTimer = 0f;
        confrontTimer = 0f;
        retreatTimer = -1f;
        retreatTarget = -1;
        return true;   // 성공
    }

    // 후퇴 예약 (종소리 + 랜덤 텀 후 실행)
    void ScheduleRetreat(int targetNode)
    {
        retreatTarget = targetNode;
        retreatTimer = bellSoundDuration + Random.Range(bellReactMin, bellReactMax);
        // TODO: 여기서 종소리 재생 (Phase 8) — bellSoundDuration이 그 길이
    }

    void DoRetreat()
    {
        retreatTimer = -1f;
        if (retreatTarget >= 0)
        {
            Debug.Log($"연호 후퇴 → 노드 {retreatTarget}");
            MoveToNode(retreatTarget);

            retreatTarget = -1;
            moveCounter = 0f;  // 바로 재이동 방지
            secondTimer = 0f;  // 초 타이머 리셋
        }
    }

    public void ResetToStart()
    {
        state = State.Moving;
        currentNode = 0;
        moveCounter = 0f;
        totalTurns = 0;
        secondTimer = 0f;
        confrontTimer = 0f;
        retreatTimer = -1f;
        retreatTarget = -1;
        MoveToNode(0);
    }
}
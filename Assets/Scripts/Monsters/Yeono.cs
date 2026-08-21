using UnityEngine;

// 연호: 메인 빌런 (엔진A). 
// 등장 → 경로(엔진A 방황) → 창문앞(대치) → 빼꼼(화면전환 시 즉사).
// 1단계+대치: 종은 2단계 훅만. 빼꼼=ScreenTransitionDetector 구독.
public class Yeono : MonoBehaviour
{
    [Header("경로 노드 (엔진A용: 공부방2→복도→로비→복도)")]
    public Transform[] nodes;   // 창문앞/빼꼼은 별도 (아래)

    [Header("대치 위치")]
    public Transform windowFrontPos;   // 창문앞 (대치 시작)
    public Transform peekPos;          // 빼꼼 (최종)

    [Header("등장 (엔진A와 분리)")]
    public GameObject model;
    public float spawnDelayMin = 10f;
    public float spawnDelayMax = 25f;

    [Header("엔진A")]
    public int aiLevel = 1;
    public int currentNight = 1;

    [Header("행동 굴림 가중치 (전진 위주)")]
    public int weightForward = 60;
    public int weightRetreat = 20;
    public int weightStay = 20;

    [Header("대치 조임 시간")]
    public float windowFrontTime = 6f;   // 창문앞→빼꼼 (종 칠 시간)

    [Header("참조")]
    public MonitorDisplay monitor;
    public ScreenTransitionDetector transitionDetector;
    public JumpscareOverlay jumpscare;

    // 상태
    private enum State { Spawning, Moving, WindowFront, Peeking }
    private State state = State.Spawning;

    // 등장
    private float spawnTimer = 0f;
    private float spawnWait = 0f;

    // 엔진A
    private int currentNode = 0;
    private float moveCounter = 0f;
    private int totalTurns = 0;
    private float secondTimer = 0f;

    // 대치
    private float confrontTimer = 0f;

    void Start()
    {
        spawnWait = Random.Range(spawnDelayMin, spawnDelayMax);
        if (model != null) model.SetActive(false);
        if (transitionDetector == null) transitionDetector = FindAnyObjectByType<ScreenTransitionDetector>();
        if (jumpscare == null) jumpscare = FindAnyObjectByType<JumpscareOverlay>();

        // 화면 전환 구독 (빼꼼 상태에서만 반응)
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
            case State.Spawning:    UpdateSpawning();    break;
            case State.Moving:      UpdateMoving();      break;
            case State.WindowFront: UpdateWindowFront(); break;
            // Peeking은 화면 전환(HandleScreenTransition)으로만 발동
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
        {
            totalTurns += 1;   // 멈춤
        }
        else if (r < weightStay + weightRetreat)
        {
            totalTurns = 0;
            if (currentNode > 0) MoveToNode(currentNode - 1);   // 후퇴
        }
        else
        {
            totalTurns = 0;
            MoveToNode(currentNode + 1);   // 전진
        }
    }

    void MoveToNode(int index)
    {
        // 경로 끝 넘으면 → 창문앞(대치) 진입
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
        Debug.Log($"연호 이동 → 노드 {index}");
    }

    // === 창문앞 (대치, 종 칠 마지막 기회) ===
    void EnterWindowFront()
    {
        state = State.WindowFront;
        confrontTimer = 0f;
        if (windowFrontPos != null)
        {
            transform.position = windowFrontPos.position;
            transform.rotation = windowFrontPos.rotation;
        }
        Debug.Log("★★ 연호 창문앞! 종 칠 마지막 기회 (안 치면 빼꼼)");
        // TODO: 종 치면 ResetToStart() (2단계)
    }

    void UpdateWindowFront()
    {
        // 랜덤 후퇴 없음. 시간 지나면 빼꼼으로 조임 (종으로만 후퇴 가능 — 2단계)
        confrontTimer += Time.deltaTime;
        if (confrontTimer >= windowFrontTime)
            EnterPeeking();
    }

    // === 빼꼼 (armed, 화면 전환 시 즉사) ===
    void EnterPeeking()
    {
        state = State.Peeking;
        if (peekPos != null)
        {
            transform.position = peekPos.position;
            transform.rotation = peekPos.rotation;
        }
        Debug.Log("★★★ 연호 빼꼼! (화면 전환 시 즉사 — 환기오류/팬텀/CCTV)");
        // 이제 화면 전환 오면 HandleScreenTransition에서 즉사
    }

    // 화면 전환 방송 수신 → 빼꼼이면 즉사
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

    // 종으로 후퇴 (2단계에서 호출) — 경로로 복귀
    public void ResetToStart()
    {
        state = State.Moving;
        currentNode = 0;
        moveCounter = 0f;
        totalTurns = 0;
        secondTimer = 0f;
        confrontTimer = 0f;
        MoveToNode(0);
    }
}
using UnityEngine;

// 현우: 환풍구 전용 귀신 (엔진B 이동 + 셔터 판정 A).
// 허공(등장대기) → 공부방2 앉음 → 환풍구 매달림 → 화장실 → 관리실 입구.
// 입구 도달 후: 셔터 닫고 5초 유지 → 격퇴 / 못 막으면 침투(armed) → 시야 이탈/유예 시 스택+1.
// 현우 모델에 붙인다.
public class Hyunwoo : MonoBehaviour
{   
    [Header("날짜 (GameManager가 세팅)")]
    public int currentNight = 1;
    
    [Header("환풍구 루트 노드")]
    public Transform[] nodes;   // [허공, 공부방2앉음, 환풍구매달림, 화장실, 관리실입구]

    [Header("이동 판정")]
    public int aiLevel = 5;
    public float cycleTime = 5f;
    public int fixedNodeCount = 3;   // 앞 3노드(허공/앉음/매달림)는 고정 시간

    [Header("고정 노드 대기 시간 (노드별, 랜덤)")]
    public Vector2[] fixedNodeWaits = {
        new Vector2(10f, 30f),   // 노드0: 허공 (등장 대기)
        new Vector2(10f, 20f),   // 노드1: 앉음
        new Vector2(3f, 7f),     // 노드2: 환풍구 매달림
    };

    [Header("셔터 판정 A")]
    [Tooltip("셔터 열린 동안 이만큼 지나면 침투(armed)")]
    public float infiltrationLimit = 15f;
    [Tooltip("셔터 닫고 이만큼 유지하면 격퇴")]
    public float holdRequired = 5f;
    [Tooltip("초당 조기 이탈 확률 (FNAF2식 1/10)")]
    public float earlyLeaveChance = 0.1f;
    [Tooltip("침투(armed) 후 시야 안 돌려도 이만큼 지나면 당함")]
    public float armedGrace = 6f;
    [Tooltip("armed 직후 인지 유예 (이 시간엔 시야 이탈 감지 안 함)")]
    public float armedRecognizeDelay = 1f;

    [Header("참조")]
    public MonitorDisplay monitor;
    public ShutterController shutter;        // 셔터 닫힘 상태 읽기
    public ViewController viewController;    // 시야 구역 읽기 (환풍구 이탈 감지)
    public JumpscareOverlay jumpscare;       // 침투 당했을 때 잔상
    public Animator animator;                // 포즈 전환 (앉음/매달림/벤트입구)
    public int ventZone = 2;                 // 환풍구 구역 번호

    [Header("스택 (현우 전용)")]
    public int stackCount = 0;               // 3 되면 정전 (지금은 로그만)

    // 상태
    private enum State { Moving, AtEntrance, Armed }
    private State state = State.Moving;

    // 이동
    private int currentNode = 0;
    private float timer = 0f;
    private float currentWait = 0f;

    // 판정 타이머
    private float infiltrationTimer = 0f;    // 침투까지 (셔터 열린 동안 참)
    private float holdTimer = 0f;            // 셔터 유지 (닫힌 동안 참)
    private float earlyLeaveTimer = 0f;      // 조기 이탈 굴림용 (초당)
    private float armedTimer = 0f;           // armed 유예

    void Start()
    {
        if (shutter == null) shutter = FindAnyObjectByType<ShutterController>();
        if (viewController == null) viewController = FindAnyObjectByType<ViewController>();
        if (jumpscare == null) jumpscare = FindAnyObjectByType<JumpscareOverlay>();
        if (animator == null) animator = GetComponent<Animator>();
        if (nodes.Length > 0) MoveToNode(0);
    }

    void Update()
    {
        switch (state)
        {
            case State.Moving:     UpdateMoving();     break;
            case State.AtEntrance: UpdateAtEntrance(); break;
            case State.Armed:      UpdateArmed();      break;
        }
    }

    // ===== 1. 이동 (엔진B) =====
    void UpdateMoving()
    {
        // 고정 노드 구간 (허공/앉음/매달림) — 정해진 랜덤 시간 대기
        if (currentNode < fixedNodeCount)
        {
            timer += Time.deltaTime;
            if (timer >= currentWait)
            {
                timer = 0f;
                MoveToNode(currentNode + 1);
            }
        }
        // 확률 이동 구간 (환풍구~)
        else
        {
            timer += Time.deltaTime;
            if (timer >= cycleTime)
            {
                timer = 0f;
                int roll = Random.Range(0, 20);
                if (roll < aiLevel)
                    MoveToNode(currentNode + 1);
            }
        }
    }

    void MoveToNode(int index)
    {
        // 마지막 노드(관리실 입구) 넘으면 판정 시작
        if (index >= nodes.Length)
        {
            ReachEntrance();
            return;
        }

        currentNode = index;
        transform.position = nodes[index].position;
        transform.rotation = nodes[index].rotation;

        // 노드별 포즈 전환 (Animator의 PoseNode Integer로)
        // 1=앉음, 2=매달림, 4=벤트입구. 안 보이는 노드(0 허공,3 화장실)는 아무거나.
        if (animator != null) animator.SetInteger("PoseNode", index);

        // CCTV 스태틱: 노드1(앉음)·2(매달림)·3(화장실=사라지는 순간)에서만.
        // 허공(0)·관리실입구(4)는 X.
        if (index == 1 || index == 2 || index == 3)
        {
            if (monitor != null) monitor.GhostMoveStatic();
        }

        // 고정 노드면 배열에서 랜덤 대기시간
        if (index < fixedNodeCount && index < fixedNodeWaits.Length)
            currentWait = Random.Range(fixedNodeWaits[index].x, fixedNodeWaits[index].y);

        Debug.Log($"현우 이동 → 노드 {index}");
    }

    // ===== 2. 입구 도달 → 판정 시작 =====
    void ReachEntrance()
    {
        state = State.AtEntrance;
        infiltrationTimer = 0f;
        holdTimer = 0f;
        earlyLeaveTimer = 0f;
        Debug.Log("★ 현우 관리실 입구 도달! 셔터 판정 시작");
    }

    // ===== 3. 입구 판정 (셔터 닫고 5초 유지 vs 침투) =====
    void UpdateAtEntrance()
    {
        bool shutterClosed = (shutter != null) && shutter.isShutterClosed;

        if (shutterClosed)
        {
            // 셔터 닫힘: 침투타이머 정지(그대로), 유지타이머 참
            holdTimer += Time.deltaTime;

            // 조기 이탈 굴림 (초당 1회)
            earlyLeaveTimer += Time.deltaTime;
            if (earlyLeaveTimer >= 1f)
            {
                earlyLeaveTimer = 0f;
                if (Random.value < earlyLeaveChance)
                {
                    Repel("조기 이탈(운)");
                    return;
                }
            }

            // 5초 유지 성공 → 격퇴
            if (holdTimer >= holdRequired)
            {
                Repel("5초 유지 성공");
                return;
            }
        }
        else
        {
            // 셔터 열림: 유지타이머 리셋, 침투타이머 참
            holdTimer = 0f;
            earlyLeaveTimer = 0f;

            infiltrationTimer += Time.deltaTime;
            if (infiltrationTimer >= infiltrationLimit)
            {
                BecomeArmed();
            }
        }
    }

    // 격퇴: 쿵쿵 + 환풍구 빔 → 처음으로 복귀
    void Repel(string reason)
    {
        Debug.Log($"★ 현우 격퇴! 쿵쿵 ({reason})");
        // TODO: 쿵쿵 사운드 (Phase 8)
        ResetToStart();
    }

    // ===== 4. 침투(armed): 쿵쿵 + 환풍구 빔 (격퇴랑 똑같이 보임) =====
    void BecomeArmed()
    {
        state = State.Armed;
        armedTimer = 0f;
        Debug.Log("★ 현우 침투(armed)! 쿵쿵 + 환풍구 빔 — 격퇴랑 똑같이 보임(뒤에 숨음)");
        // TODO: 쿵쿵 사운드 (격퇴랑 동일하게 — 심리전)
    }

    void UpdateArmed()
    {
        armedTimer += Time.deltaTime;

        // 인지 유예 후부터 시야 이탈 감지 (armed 직후 억울한 즉사 방지)
        if (armedTimer >= armedRecognizeDelay)
        {
            bool lookingVent = (viewController != null) && viewController.currentZone == ventZone;
            if (!lookingVent)
            {
                GetHit("시야 이탈 (방심)");
                return;
            }
        }

        // 계속 환풍구 봐도 유예 끝나면 당함 (뒤통수 영원히 못 지킴)
        if (armedTimer >= armedGrace)
        {
            GetHit("유예 초과");
            return;
        }
    }

    // 당함: 스택+1 + 잔상 (비즉사)
    void GetHit(string reason)
    {
        stackCount++;
        Debug.Log($"★★ 현우 침투 성공! 스택 +1 (현재 {stackCount}) — {reason}");

        // 잔상 연출 (JumpscareOverlay 재활용, 환풍구 방향 애니는 Phase 8)
        if (jumpscare != null) jumpscare.Play();

        // TODO: 스택별 효과 (1 조명깜빡 / 2 조명나감 / 3 정전) — 나중 구현
        if (stackCount >= 3)
        {
            Debug.Log("★★★ 현우 3스택 → 정전! (창고런은 나중 구현)");
            // TODO: 정전 → 창고런 통로 열림
        }

        ResetToStart();
    }

    // 처음으로 복귀 (격퇴/당함 공용)
    public void ResetToStart()
    {
        state = State.Moving;
        currentNode = 0;
        timer = 0f;
        infiltrationTimer = 0f;
        holdTimer = 0f;
        armedTimer = 0f;
        earlyLeaveTimer = 0f;
        MoveToNode(0);
    }
}
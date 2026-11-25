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

    [Header("엔진A — 밤별 AI (인덱스 = 밤 숫자, [0]은 안 쓰는 칸)")]
    [Tooltip("기획서 §8: 1일 0(비활성) / 2일 2(맛보기) / 3일 5 / 4일 9 / 5일 16")]
    public int[] aiByNight = { 0, 0, 2, 5, 9, 16 };
    [Tooltip("테스트용: 0 이상이면 밤 상관없이 이 AI 사용. -1 = 끔(평소)")]
    public int testAiOverride = -1;
    public int AI => (testAiOverride >= 0) ? testAiOverride : GameManager.ByNight(aiByNight);

    [Header("2일차 맛보기 (기획서: 4~5시 확정 1회 → 창문 접근 → 종 첫 학습)")]
    public int tasteNight = 2;       // 이 밤엔
    public int tasteHour = 4;        // 이 시부터 등장 대기 시작, 창문에서 한 번 밀려나면 오늘 밤 퇴장

    [Header("행동 굴림 가중치")]
    public int weightForward = 60;
    public int weightRetreat = 15;
    public int weightStay = 25;

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
    public AudioSystem audioSystem;
    public YeonhoAggression aggression;   // 흉포 토글 (비어 있으면 자동으로 찾고, 없으면 붙임)

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

    // 2일차 맛보기
    private bool IsTasteNight => GameManager.Night == tasteNight;
    private bool tasteDone = false;          // 오늘 밤 맛보기 끝 (더 안 나옴)
    private bool tasteRetreatPending = false; // 창문앞에서 종으로 밀려나는 중 → 후퇴 끝나면 퇴장

    void Start()
    {
        spawnWait = Random.Range(spawnDelayMin, spawnDelayMax);
        if (model != null) model.SetActive(false);
        if (monitor == null) monitor = FindAnyObjectByType<MonitorDisplay>();
        if (transitionDetector == null) transitionDetector = FindAnyObjectByType<ScreenTransitionDetector>();
        if (jumpscare == null) jumpscare = FindAnyObjectByType<JumpscareOverlay>();
        if (audioSystem == null) audioSystem = FindAnyObjectByType<AudioSystem>();
        if (aggression == null) aggression = FindAnyObjectByType<YeonhoAggression>();
        if (aggression == null) aggression = gameObject.AddComponent<YeonhoAggression>();   // 씬에 없으면 기본값으로 붙임

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
        // 2일차 맛보기: 정해진 시 전엔 안 나오고, 한 번 끝났으면 다시 안 나옴
        if (IsTasteNight && (tasteDone || GameManager.Hour < tasteHour)) return;

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
        // 흉포: 카운터 2배(+2/초) + 판정 기준 -1 (기획서 §2.5 — 이동에만 영향, 종과 무관)
        bool fury = aggression != null && aggression.IsAggressive;
        moveCounter += fury ? 2f : 1f;
        int threshold = (10 - AI) - (fury ? 1 : 0) + Random.Range(1, 16) - totalTurns;
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
        else if (state == State.WindowFront)
            EnterPeeking();   // 창문앞에서 화면 전환 = 조여짐. 이번엔 안 죽고 빼꼼으로 넘어감(다음 전환에 즉사)
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
        // 오디오 고장 중이면 아예 못 침
        if (audioSystem != null && audioSystem.IsAudioBroken())
        {
            Debug.Log("오디오 고장 중 - 종 사용 불가 (리셋 필요)");
            return;
        }

        // 쿨 중이면 무시
        if (bellCooldownTimer > 0f)
        {
            Debug.Log("종 쿨타임 중");
            return;
        }

        // 쿨·배터리는 헛방이어도 소모
        bellCooldownTimer = bellCooldown;
        if (BatteryManager.Instance != null) BatteryManager.Instance.Drain(bellDrain);

        // 밤당 종 횟수 소모 (한도 초과 시 오디오 고장 → 이번 타종은 밀어내기 효과 없음)
        if (audioSystem != null && !audioSystem.TryUseBell())
        {
            Debug.Log("★★★ 종 과사용으로 오디오 고장 - 이번 종은 안 먹힘");
            return;
        }

        // 창문앞: 연호가 방금 떠나온 마지막 방(세로복도)에 울렸을 때만 먹힘. 다른 방은 헛방.
        // 플레이어는 연호가 창문앞에 있는지 CCTV로 확인 못 함 → 감으로 치는 종(결과도 안 보임).
        // 먹히면 무시 없음(기획서: 창문앞 무시 없음), 강하게 후퇴(반응 딜레이 후)
        if (state == State.WindowFront)
        {
            int lastIdx = nodes.Length - 1;
            int frontRoom = (lastIdx >= 0 && lastIdx < nodeRoomIndex.Length) ? nodeRoomIndex[lastIdx] : -1;

            if (room != frontRoom)
            {
                Debug.Log($"종 헛방 (연호 창문앞 — 방 {frontRoom}에 쳐야 함, 친 방 {room})");
                return;
            }

            Debug.Log("★ 종! 연호 창문앞에서 반응 중...");
            state = State.Moving;
            int back = Mathf.Max(0, nodes.Length - windowFrontRetreat);
            ScheduleRetreat(back);
            if (IsTasteNight) tasteRetreatPending = true;   // 맛보기 밤: 창문에서 밀려나면 오늘은 끝
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

    // 비상 종 (EmergencyBell이 호출). 창문앞일 때만 효과 → 완전 후퇴(재등장 대기).
    // 그 외(등장 전/이동 중/빼꼼)는 헛방 — false 반환. 비용은 EmergencyBell이 무조건 소모.
    public bool EmergencyPush()
    {
        if (state != State.WindowFront) return false;

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
        if (IsTasteNight) EndTaste();   // 맛보기 밤: 비상 종으로 밀어냈으면 오늘은 끝
        return true;   // 성공
    }

    // 2일차 맛보기 끝 — 오늘 밤 더 안 나옴
    void EndTaste()
    {
        tasteDone = true;
        tasteRetreatPending = false;
        if (model != null) model.SetActive(false);
        state = State.Spawning;
        retreatTimer = -1f;
        retreatTarget = -1;
        Debug.Log("★ 연호 맛보기 끝 (창문에서 밀어냄) — 오늘 밤 더 안 나옴");
    }

    // 배터리 방전 암전 연출용 (BatteryBlackout가 호출) — AI는 꺼진 상태(enabled=false)에서 창문앞에 모습만 드러냄
    public void AppearAtWindowForBlackout()
    {
        if (windowFrontPos != null)
        {
            transform.position = windowFrontPos.position;
            transform.rotation = windowFrontPos.rotation;
        }
        if (model != null) model.SetActive(true);
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

        // 맛보기 밤: 창문앞에서 종으로 밀려난 거면 후퇴하는 순간 퇴장 (창문에서 물러나며 사라짐, CCTV엔 이동 스태틱이 덮음)
        if (tasteRetreatPending) EndTaste();
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
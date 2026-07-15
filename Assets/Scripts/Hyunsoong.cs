using UnityEngine;

// 현승: 노드 이동 → 창문 워크패스 → 응시 대응
public class Hyunsoong : MonoBehaviour
{
    [Header("노드 경로 (순서대로)")]
    public Transform[] nodes;          // [착석,기립,가로복도,세로복도상단,화장실,창문]
    public float moveInterval = 6f;    // 노드 간 이동 간격

    [Header("창문 워크패스")]
    public Transform windowStart;      // 창문 왼쪽 끝
    public Transform windowEnd;        // 창문 오른쪽 끝
    public float walkPathTime = 6f;    // 워크패스 총 시간(빡세게 6초)

    [Header("응시 게이지")]
    public float gazeFillSpeed = 1f;   // 응시 중 초당 채움
    public float gazeDrainSpeed = 2f;  // 놓쳤을 때 초당 감소(빠르게)
    public float gazeRequired = 4f;    // 필요 누적량

    [Header("참조")]
    public CCTVController cctv;         // CCTV 내림 여부 확인용

    // 상태
    private enum State { Moving, WalkPath, Armed, Gone }
    private State state = State.Moving;

    private int currentNode = 0;
    private float timer = 0f;
    private float walkTimer = 0f;       // 워크패스 경과
    private float gazeGauge = 0f;       // 응시 누적
    private bool beingGazed = false;    // 지금 GazeBox가 닿아있나 (Trigger가 갱신)

    void Start()
    {
        if (nodes.Length > 0) MoveToNode(0);
        if (cctv == null) cctv = FindAnyObjectByType<CCTVController>();
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
        // 마지막 노드(창문) 직전까지만 이동, 창문 도달하면 워크패스로
        if (currentNode >= nodes.Length - 1)
        {
            StartWalkPath();
            return;
        }
        timer += Time.deltaTime;
        if (timer >= moveInterval)
        {
            timer = 0f;
            MoveToNode(currentNode + 1);
        }
    }

    void MoveToNode(int index)
    {
        currentNode = index;
        transform.position = nodes[index].position;
        transform.rotation = nodes[index].rotation;
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
        Debug.Log($"워크패스 | beingGazed:{beingGazed} | cctvDown:{cctvDown} | 게이지:{gazeGauge:F1}");

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
            Debug.Log("현승 ARMED! (다음 화면 전환 시 오디오 고장)");
        }
    }

    // 응시 성공 → 공부방1로 복귀
    void Disappear()
    {
        state = State.Moving;
        currentNode = 0;
        timer = 0f;
        MoveToNode(0);
        Debug.Log("현승 응시 성공 → 공부방1 복귀");
    }

    // --- GazeBox가 호출 (Trigger 감지) ---
    public void SetGazed(bool value)
    {
        beingGazed = value;
    }
}
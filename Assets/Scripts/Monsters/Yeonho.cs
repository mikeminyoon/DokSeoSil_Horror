using System.Collections.Generic;
using UnityEngine;

// 연호: 메인 빌런 — 원작 FNAF3 스프링트랩식 (구현노트 §15.23~24, 유저 결정 2026-11-26)
//
// [지도] 방 번호 = CCTV 방 번호(roomFeeds 인덱스): 0 공부방1 / 1 공부방2 / 2 가로복도 / 3 화장실 / 4 로비 / 5 세로복도 / 6 창고
//   rooms[번호] = 그 방에 서 있을 자리 + 붙은 방들 (유저 맵 그림 + 창고 뒷문). 붙은 관계는 한쪽만 적어도 양쪽으로 침.
//   창문까지 거리는 시작할 때 계산 (세로복도 1칸 / 가로복도·화장실·로비·창고 2칸 / 공부방1·2 3칸).
//   서 있을 자리가 빈 방은 지도에서 빠짐 (연호가 못 감, 경고 로그). 옛 고정 루트 노드(nodes)가 빈 자리를 자동으로 채움.
// [이동 = 엔진A] 매초 카운터 > (10-AI) - 흉포 + 랜덤(1~15) - 멈춘 횟수 → 행동 굴림 (멈춤 / 후퇴 / 전진)
//   전진 = 창문에 더 가까운 옆방 (세로복도에서 전진 = 창문앞)
//   후퇴 = 더 먼 옆방 중 랜덤 (없으면 같은 거리 옆방, 그것도 없으면 멈춤으로 침 — 막다른 방)
// [일반 종 = 유인 (원작 오디오)] 지금 보는 CCTV 방에 종 → 연호가 그 방의 옆방에 있으면 종소리 뒤 그 방으로 걸어옴.
//   같은 방 / 안 붙은 방 = 헛방. 잘못 치면 창문 쪽으로 끌어옴. 무시 1/7은 어디서나 (비상 종이 100%라서 — 유저).
//   창문앞·빼꼼: 세로복도에 쳐야 세로복도로 돌아옴. 관리실 안 = 헛방(이미 늦음).
//   쿨·배터리·횟수(AudioSystem)는 헛방이어도 소모 (지금까지와 같음).
// [비상 종] 돌아다니는 중이든 창문앞이든 100% 스폰 전으로. 빼꼼부터는 헛방 (유저: A안 + 어디서든).
// [창문 쪽 3단계 — 원작의 창문 / 문간 / 사무실 안]
//   창문앞: 도착 순간 임시 숨소리. CCTV엔 안 보이고 창문 너머로만 보임. 화면 전환 상관없음(내려서 확인해도 안전).
//           windowFrontTime 지나면 빼꼼.
//   빼꼼:   가만히 있으면 안 죽음. CCTV·패널을 올리면 doorEntryTime 안에 세로복도 종이 먹혀야 함 → 못 하면 관리실 안.
//           내리면 카운트 초기화. 시야 상실(환기 흐려짐 / 다른 귀신 잔상) = 즉사.
//   관리실 안: 안 보임. CCTV 내림·패널 닫음(강제 포함) 순간 즉사. 시야 상실도 즉사.
// [연속 즉사] 다른 귀신 잔상(점프스케어) 중이면 그 잔상이 끝난 뒤 연호 점프스케어 (유저: "연속으로 당하기")
// [창고] CAM07은 화면 X → 소리(YeonhoSounds): 들어갈 때 우당탕 / 안에 있을 때 CAM07 보면 부스럭 / 나갈 땐 조용.
// [2일차 맛보기] tasteHour부터 등장 → 창문앞·빼꼼에서 종으로 끌어내거나 비상 종을 쓰면 그날 퇴장.
public class Yeonho : MonoBehaviour
{
    [System.Serializable]
    public class Room
    {
        public string name;              // 표시용
        [Tooltip("연호가 서 있을 자리 (그 방 CCTV에 보이게). 비면 이 방은 못 감")]
        public Transform standPoint;
        [Tooltip("붙은 방 번호 (한쪽만 적어도 양쪽으로 침)")]
        public int[] neighbors;
    }

    [Header("지도 (방 번호 = CCTV 방 번호 = roomFeeds 인덱스)")]
    [Tooltip("0 공부방1 / 1 공부방2 / 2 가로복도 / 3 화장실 / 4 로비 / 5 세로복도 / 6 창고")]
    public Room[] rooms = {
        new Room { name = "공부방1",  neighbors = new[] { 2 } },
        new Room { name = "공부방2",  neighbors = new[] { 2 } },
        new Room { name = "가로복도", neighbors = new[] { 0, 1, 5, 6 } },
        new Room { name = "화장실",   neighbors = new[] { 5 } },
        new Room { name = "로비",     neighbors = new[] { 5 } },
        new Room { name = "세로복도", neighbors = new[] { 2, 3, 4, 6 } },   // + 창문앞 (windowRoom이라 자동)
        new Room { name = "창고",     neighbors = new[] { 2, 5 } },         // 문 2개: 가로복도 + 세로복도 뒷문 (유저 OK)
    };
    [Tooltip("등장하는 방 (거점) — 공부방2")]
    public int spawnRoom = 1;
    [Tooltip("창문앞과 붙은 방 — 세로복도. 창문앞·빼꼼에선 이 방에 친 종만 먹힘")]
    public int windowRoom = 5;

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

    [Header("창문앞 머무르는 시간 (지나면 빼꼼)")]
    public float windowFrontTime = 6f;

    [Header("빼꼼 (문간)")]
    [Tooltip("빼꼼일 때 CCTV·패널을 올리면 이 시간 안에 세로복도 종이 먹혀야 함 — 못 하면 관리실 안으로. 내리면 초기화")]
    public float doorEntryTime = 2.5f;

    [Header("종 (유인)")]
    public float bellCooldown = 5.4f;
    public float bellDrain = 8f;
    [Tooltip("무시 확률 1/N (원작 1/7) — 어디서나 (비상 종이 100%라서)")]
    public int ignoreChance = 7;

    [Header("종 반응 (종소리 + 랜덤 텀 뒤 이동)")]
    public float bellSoundDuration = 1.0f;   // 종소리 재생 시간 (나중에 실제 사운드 길이)
    public float bellReactMin = 0.2f;        // 추가 랜덤 텀 (최소)
    public float bellReactMax = 0.6f;        // (최대)

    [Header("참조")]
    public MonitorDisplay monitor;
    public ScreenTransitionDetector transitionDetector;
    public JumpscareOverlay jumpscare;
    public AudioSystem audioSystem;
    public YeonhoAggression aggression;   // 흉포 토글 (비어 있으면 자동으로 찾고, 없으면 붙임)
    public YeonhoSounds sounds;           // 임시 소리 (비어 있으면 자동으로 찾고, 없으면 붙임)

    [Header("창문앞 복도 조명 (연호가 창문 쪽이면 깜빡)")]
    [Tooltip("직접 만든 복도 조명(Light)을 여기에 끌어다 놓기. 비우면 임시 조명을 자동으로 만듦")]
    public Light[] corridorLights;
    private WindowCorridorLight windowLight;   // 깜빡임 담당 (자동 생성 — 위 칸의 조명을 받아서 씀)

    [Header("상태 (확인용)")]
    public string status = "";

    // 옛 고정 루트 노드 (공부방2→가로복도→로비→세로복도) — 지도에서 자리가 빈 방을 자동으로 채움. Inspector엔 숨김
    [HideInInspector] public Transform[] nodes;
    [HideInInspector] public int[] nodeRoomIndex;

    // 상태
    private enum State { Spawning, Moving, WindowFront, Peeking, Inside }
    private State state = State.Spawning;

    // 지도 (Start에서 만듦)
    private List<int>[] adj;          // 붙은 방 (양쪽, 자리 있는 방끼리만)
    private int[] dist;               // 창문까지 칸 수 (못 가는 방 = -1)
    private bool mapOk = false;
    private int currentRoom = -1;     // 지금 있는 방 (Moving일 때만, 창문 쪽이면 -1)
    private static readonly List<int> pickBuffer = new List<int>();

    private float spawnTimer = 0f;
    private float spawnWait = 0f;

    // 엔진A
    private float moveCounter = 0f;
    private int totalTurns = 0;
    private float secondTimer = 0f;

    private float confrontTimer = 0f;   // 창문앞 머문 시간
    private float entryTimer = 0f;      // 빼꼼: 올리고 있는 시간

    private float bellCooldownTimer = 0f;

    // 유인 예약 (종소리 + 텀 뒤 이동)
    private bool lurePending = false;
    private float lureTimer = 0f;
    private int lureTarget = -1;
    private bool lureFromWindow = false;

    // 즉사 예약 (다른 귀신 잔상이 끝난 뒤 이어서)
    private bool killPending = false;
    private string killReason = "";
    private bool lastJumpscarePlaying = false;

    // 창문 쪽에 있음 (창문앞·빼꼼·종에 반응해 떠나는 중) → 복도 조명 깜빡 + 실루엣 (구현노트 §15.25)
    private bool atWindowSide = false;
    private Silhouette silhouette;           // 모델에 자동으로 붙임

    // 2일차 맛보기
    private bool IsTasteNight => GameManager.Night == tasteNight;
    private bool tasteDone = false;          // 오늘 밤 맛보기 끝 (더 안 나옴)

    private int StorageRoom => (monitor != null) ? monitor.storageRoomIndex : 6;
    private bool PlayerIsUp => transitionDetector != null && transitionDetector.PlayerIsUp;

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
        if (sounds == null) sounds = GetComponent<YeonhoSounds>();
        if (sounds == null) sounds = gameObject.AddComponent<YeonhoSounds>();             // 씬에 없으면 임시 소리로 붙임
        windowLight = WindowCorridorLight.GetOrCreate(windowFrontPos);   // 깜빡임 담당 (씬에 없으면 자동 생성)
        windowLight.UseLights(corridorLights);                            // 직접 만든 조명이 있으면 그걸로 (없으면 임시 조명)
        if (model != null)
        {
            silhouette = model.GetComponent<Silhouette>();
            if (silhouette == null) silhouette = model.AddComponent<Silhouette>();          // 창문 쪽에선 검은 모양만
        }

        BuildMap();

        if (transitionDetector != null)
        {
            transitionDetector.OnLowered += HandleLowered;
            transitionDetector.OnVisionLost += HandleVisionLost;
        }
        else
        {
            Debug.LogWarning("연호: ScreenTransitionDetector가 없음 — 빼꼼·관리실 안 판정이 안 됨");
        }

        SetStatus("등장 대기");
    }

    void OnDestroy()
    {
        if (transitionDetector != null)
        {
            transitionDetector.OnLowered -= HandleLowered;
            transitionDetector.OnVisionLost -= HandleVisionLost;
        }
    }

    // 꺼질 때(방전 암전) 정리 — 창고 부스럭이 계속 나거나 복도 조명이 계속 깜빡이지 않게
    void OnDisable()
    {
        if (sounds != null) sounds.StopAll();
        SetWindowPresence(false);
    }

    void Update()
    {
        if (AI <= 0) return;      // AI 0 = 비활성 (오늘 밤 안 나옴)
        if (!mapOk) return;       // 지도 설정 오류 (Start에서 에러 로그)

        // 6시 생존 / 다른 귀신으로 게임오버 → 연호도 멈춤
        if (GameManager.Instance != null && (GameManager.Instance.isNightOver || GameManager.Instance.isGameOver))
        {
            if (sounds != null) sounds.SetStorageListening(false);
            if (GameManager.Instance.isNightOver) SetWindowPresence(false);   // 6시 — 창문 깜빡임 멈춤
            return;
        }

        if (bellCooldownTimer > 0f) bellCooldownTimer -= Time.deltaTime;

        WatchJumpscare();         // 다른 귀신 잔상 시작 = 시야 상실
        if (killPending)          // 즉사 확정 — 잔상 끝나길 기다렸다 덮침
        {
            TryFinishKill();
            return;
        }

        // 종 유인 예약 처리 (종소리 + 텀 뒤 이동)
        if (lurePending)
        {
            lureTimer -= Time.deltaTime;
            if (lureTimer <= 0f) DoLure();
        }

        switch (state)
        {
            case State.Spawning:    UpdateSpawning();    break;
            case State.Moving:      UpdateMoving();      break;
            case State.WindowFront: UpdateWindowFront(); break;
            case State.Peeking:     UpdatePeeking();     break;
            // Inside: 기다림 — 내림/시야 상실 신호가 오면 즉사
        }

        UpdateStorageListening();
    }

    // ================= 지도 =================

    void BuildMap()
    {
        int n = (rooms != null) ? rooms.Length : 0;

        // 옛 고정 루트 노드로 빈 자리 채우기 (예전에 쓰던 자리 — 씬 수정 없이 그대로 이어받음)
        if (nodes != null && nodeRoomIndex != null)
        {
            for (int k = 0; k < nodes.Length && k < nodeRoomIndex.Length; k++)
            {
                int r = nodeRoomIndex[k];
                if (r >= 0 && r < n && rooms[r] != null && rooms[r].standPoint == null && nodes[k] != null)
                    rooms[r].standPoint = nodes[k];
            }
        }

        // 붙은 관계 (양쪽으로) — 자리가 있는 방끼리만
        adj = new List<int>[n];
        for (int i = 0; i < n; i++) adj[i] = new List<int>();
        for (int i = 0; i < n; i++)
        {
            if (!HasPoint(i) || rooms[i].neighbors == null) continue;
            foreach (int j in rooms[i].neighbors)
            {
                if (j < 0 || j >= n || j == i || !HasPoint(j)) continue;
                if (!adj[i].Contains(j)) adj[i].Add(j);
                if (!adj[j].Contains(i)) adj[j].Add(i);
            }
        }

        // 창문까지 거리 (세로복도 = 1칸부터 퍼져 나감)
        dist = new int[n];
        for (int i = 0; i < n; i++) dist[i] = -1;
        if (!HasPoint(windowRoom))
        {
            Debug.LogError($"연호 지도 오류: 창문과 붙은 방(windowRoom {windowRoom})에 서 있을 자리가 없음 — 연호가 안 움직임");
            mapOk = false;
            return;
        }
        var queue = new Queue<int>();
        dist[windowRoom] = 1;
        queue.Enqueue(windowRoom);
        while (queue.Count > 0)
        {
            int c = queue.Dequeue();
            foreach (int nb in adj[c])
            {
                if (dist[nb] >= 0) continue;
                dist[nb] = dist[c] + 1;
                queue.Enqueue(nb);
            }
        }

        // 등장 방이 못 가는 방이면 → 창문에서 제일 먼 방으로 대신
        if (!Reachable(spawnRoom))
        {
            int far = windowRoom;
            for (int i = 0; i < n; i++) if (Reachable(i) && dist[i] > dist[far]) far = i;
            Debug.LogWarning($"연호: 등장 방({RoomName(spawnRoom)})에 자리가 없어서 {RoomName(far)}에서 등장");
            spawnRoom = far;
        }
        mapOk = true;

        // 지도 요약 (한 번만)
        var parts = new List<string>();
        for (int i = 0; i < n; i++)
            parts.Add(Reachable(i) ? $"{RoomName(i)} {dist[i]}칸" : $"{RoomName(i)} (자리 없음 — 못 감)");
        Debug.Log("연호 지도: " + string.Join(" / ", parts));
    }

    bool HasPoint(int r) => rooms != null && r >= 0 && r < rooms.Length && rooms[r] != null && rooms[r].standPoint != null;
    bool Reachable(int r) => dist != null && r >= 0 && r < dist.Length && dist[r] > 0;
    bool IsNeighbor(int a, int b) => adj != null && a >= 0 && a < adj.Length && adj[a].Contains(b);
    string RoomName(int r) => (rooms != null && r >= 0 && r < rooms.Length && rooms[r] != null) ? rooms[r].name : $"방{r}";

    // 지금 방의 옆방 중 조건(창문까지 칸 수)에 맞는 방 하나를 랜덤으로 (없으면 -1)
    int PickNeighbor(System.Func<int, bool> distCondition)
    {
        pickBuffer.Clear();
        if (!IsValidRoom(currentRoom)) return -1;
        foreach (int nb in adj[currentRoom])
            if (Reachable(nb) && distCondition(dist[nb])) pickBuffer.Add(nb);
        return pickBuffer.Count > 0 ? pickBuffer[Random.Range(0, pickBuffer.Count)] : -1;
    }

    bool IsValidRoom(int r) => adj != null && r >= 0 && r < adj.Length && Reachable(r);

    // ================= 등장 =================

    void UpdateSpawning()
    {
        // 2일차 맛보기: 정해진 시 전엔 안 나오고, 한 번 끝났으면 다시 안 나옴
        if (IsTasteNight && (tasteDone || GameManager.Hour < tasteHour)) return;

        spawnTimer += Time.deltaTime;
        if (spawnTimer >= spawnWait)
        {
            state = State.Moving;
            moveCounter = 0f;
            totalTurns = 0;
            secondTimer = 0f;
            if (model != null) model.SetActive(true);
            MoveToRoom(spawnRoom, "등장");
            Debug.Log($"★ 연호 등장 ({RoomName(spawnRoom)})");
        }
    }

    // ================= 이동 (엔진A) =================

    void UpdateMoving()
    {
        // 종 반응 중이면 엔진 멈춤 (멈칫)
        if (lurePending) return;

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
        {
            totalTurns += 1;                                  // 멈춤 — 다음 행동이 빨라짐
        }
        else if (r < weightStay + weightRetreat)
        {
            if (TryRetreat()) totalTurns = 0;
            else totalTurns += 1;                             // 막다른 방 — 후퇴할 곳 없음 = 멈춤으로 침
        }
        else
        {
            if (Forward()) totalTurns = 0;
            else totalTurns += 1;                             // (지도 설정 실수 대비)
        }
    }

    // 전진: 창문에 더 가까운 옆방 (세로복도면 창문앞으로)
    bool Forward()
    {
        if (!IsValidRoom(currentRoom)) return false;   // (안전장치) 방 밖이면 안 움직임
        if (currentRoom == windowRoom)
        {
            EnterWindowFront();
            return true;
        }
        int my = dist[currentRoom];
        int next = PickNeighbor(d => d < my);
        if (next < 0) return false;
        MoveToRoom(next, "전진");
        return true;
    }

    // 후퇴: 더 먼 옆방 중 랜덤 → 없으면 같은 거리 옆방 → 그것도 없으면 실패(멈춤)
    bool TryRetreat()
    {
        if (!IsValidRoom(currentRoom)) return false;   // (안전장치)
        int my = dist[currentRoom];
        int next = PickNeighbor(d => d > my);
        if (next < 0) next = PickNeighbor(d => d == my);
        if (next < 0) return false;
        MoveToRoom(next, "후퇴");
        return true;
    }

    void MoveToRoom(int room, string why)
    {
        if (!IsValidRoom(room)) return;

        int prev = currentRoom;
        currentRoom = room;
        Transform p = rooms[room].standPoint;
        transform.SetPositionAndRotation(p.position, p.rotation);
        if (monitor != null) monitor.GhostMoveStatic();

        // 창고는 화면이 안 보임 → 들어가는 순간 우당탕 (어디서든 들림). 나갈 땐 소리 없음
        bool intoStorage = room == StorageRoom && prev != StorageRoom;
        if (intoStorage && sounds != null) sounds.PlayStorageEnter();

        Debug.Log($"연호 {why} → {RoomName(room)} (창문까지 {dist[room]}칸){(intoStorage ? " — 우당탕!" : "")}");
        SetStatus($"{RoomName(room)} ({dist[room]}칸)");
    }

    // 창고 안 부스럭: 연호가 창고에 있고 플레이어가 CCTV로 CAM07을 보고 있을 때만
    void UpdateStorageListening()
    {
        if (sounds == null) return;
        bool listen = state == State.Moving && currentRoom == StorageRoom
                      && monitor != null && monitor.IsShowingRoom(StorageRoom);
        sounds.SetStorageListening(listen);
    }

    // ================= 창문 쪽 3단계 =================

    // ① 창문앞: CCTV엔 안 보임, 창문 너머로만. 화면 전환 상관없음
    void EnterWindowFront()
    {
        state = State.WindowFront;
        currentRoom = -1;
        confrontTimer = 0f;
        if (windowFrontPos != null) transform.SetPositionAndRotation(windowFrontPos.position, windowFrontPos.rotation);
        if (monitor != null) monitor.GhostMoveStatic();   // 세로복도에서 사라지는 순간도 지직 (눈앞 순간이동 안 보이게)
        if (sounds != null) sounds.PlayWindowArrive();     // 임시 숨소리
        SetWindowPresence(true);                           // 창문앞 복도 조명 깜빡 + 실루엣 (유저 요청)
        Debug.Log("★★ 연호 창문앞! (숨소리 + 복도 조명 깜빡) — 세로복도 종 / 비상 종");
        SetStatus("창문앞");
    }

    void UpdateWindowFront()
    {
        confrontTimer += Time.deltaTime;
        if (confrontTimer >= windowFrontTime)
            EnterPeeking();
    }

    // ② 빼꼼 (문간): 가만히 있으면 안 죽음. 올리면 doorEntryTime 카운트
    void EnterPeeking()
    {
        state = State.Peeking;
        entryTimer = 0f;
        if (peekPos != null) transform.SetPositionAndRotation(peekPos.position, peekPos.rotation);
        Debug.Log($"★★★ 연호 빼꼼! — CCTV·패널 올리면 {doorEntryTime:F1}초 안에 세로복도 종 / 시야 잃으면 즉사");
        SetStatus("빼꼼");
    }

    void UpdatePeeking()
    {
        if (PlayerIsUp)
        {
            entryTimer += Time.deltaTime;
            if (entryTimer >= doorEntryTime)
                EnterInside();
        }
        else
        {
            entryTimer = 0f;   // 내리면 초기화 (유저 OK)
        }
    }

    // ③ 관리실 안: 안 보임. 내리는 순간 즉사
    void EnterInside()
    {
        state = State.Inside;
        if (model != null) model.SetActive(false);
        SetWindowPresence(false);   // 복도 조명이 멀쩡해짐 — "간 줄 알았는데" (어차피 올리고 있는 중이라 못 봄)
        Debug.Log("☠ 연호 관리실 안으로 숨어듦 — CCTV 내리거나 패널 닫으면 즉사");
        SetStatus("관리실 안");
    }

    // ================= 즉사 판정 =================

    // CCTV 내림 / 패널 닫음 (강제 포함)
    void HandleLowered()
    {
        if (!enabled) return;
        if (state == State.Inside)
            RequestKill("관리실 안 — 화면을 내림");
    }

    // 환기 흐려짐 (강제 전환) = 시야 상실
    void HandleVisionLost()
    {
        if (!enabled) return;
        if (state == State.Peeking || state == State.Inside)
            RequestKill("시야 상실 — 환기 흐려짐");
    }

    // 다른 귀신 잔상(점프스케어)이 시작되는 순간 = 시야 상실
    void WatchJumpscare()
    {
        bool playing = jumpscare != null && jumpscare.IsPlaying;
        if (playing && !lastJumpscarePlaying && (state == State.Peeking || state == State.Inside))
            RequestKill("시야 상실 — 다른 귀신 잔상");
        lastJumpscarePlaying = playing;
    }

    void RequestKill(string reason)
    {
        if (killPending) return;
        killPending = true;
        killReason = reason;
    }

    // 잔상 중이면 끝날 때까지 기다렸다 덮침 (연속으로 당하기)
    void TryFinishKill()
    {
        if (jumpscare != null && jumpscare.IsPlaying) return;
        Kill();
    }

    void Kill()
    {
        killPending = false;
        Debug.Log($"☠☠☠ 연호 즉사! ({killReason}) 게임오버");
        if (jumpscare != null) jumpscare.PlayGameOver();
        if (GameManager.Instance != null)
            GameManager.Instance.GameOver("연호");
    }

    // ================= 종 (모니터 종 버튼 → MonitorDisplay.RingBellCurrentRoom → 여기) =================

    public void RingBell(int room)
    {
        if (!enabled) return;   // 방전 암전 등으로 꺼짐

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
        // TODO: 종소리 (Phase 8) — bellSoundDuration이 그 길이

        // 종 횟수 소모 (한도 초과 시 오디오 고장 → 이번 종은 안 먹힘)
        if (audioSystem != null && !audioSystem.TryUseBell())
        {
            Debug.Log("★★★ 종 과사용으로 오디오 고장 - 이번 종은 안 먹힘");
            return;
        }

        if (AI <= 0 || !mapOk)
        {
            Debug.Log("종 헛방 (연호 없음)");
            return;
        }

        switch (state)
        {
            case State.WindowFront:
            case State.Peeking:
                // 창문 쪽: 세로복도에 쳐야 세로복도로 돌아옴
                if (room != windowRoom)
                {
                    Debug.Log($"종 헛방 ({RoomName(room)} — 연호는 창문 쪽, {RoomName(windowRoom)}에 쳐야 함)");
                    return;
                }
                if (Ignored()) return;
                Debug.Log($"★ 종! 연호 창문 쪽에서 반응 중... ({RoomName(windowRoom)}로)");
                state = State.Moving;     // 떠나는 중 — 빼꼼 카운트·즉사 판정 멈춤 (모습은 반응 텀 동안 그 자리)
                currentRoom = -1;
                ScheduleLure(windowRoom, true);
                return;

            case State.Moving:
                if (lurePending)
                {
                    Debug.Log("종 헛방 (연호 이미 종에 반응 중)");
                    return;
                }
                if (room == currentRoom)
                {
                    Debug.Log($"종 헛방 ({RoomName(room)} — 연호가 있는 그 방. 옆방에 쳐야 끌려옴)");
                    return;
                }
                if (!IsNeighbor(currentRoom, room))
                {
                    Debug.Log($"종 헛방 ({RoomName(room)} — 연호는 {RoomName(currentRoom)}, 안 붙은 방)");
                    return;
                }
                if (Ignored()) return;
                Debug.Log($"★ 종! 연호 반응 중... ({RoomName(currentRoom)} → {RoomName(room)})");
                ScheduleLure(room, false);
                return;

            case State.Inside:
                Debug.Log("종 헛방 (연호 이미 관리실 안)");
                return;

            default:
                Debug.Log("종 헛방 (연호 아직 안 나옴)");
                return;
        }
    }

    bool Ignored()
    {
        if (ignoreChance > 1 && Random.Range(0, ignoreChance) == 0)
        {
            Debug.Log($"종 무시됨! (1/{ignoreChance}) — 안 끌려옴");
            return true;
        }
        return false;
    }

    // 유인 예약 (종소리 + 랜덤 텀 뒤 실행)
    void ScheduleLure(int target, bool fromWindow)
    {
        lurePending = true;
        lureTarget = target;
        lureFromWindow = fromWindow;
        lureTimer = bellSoundDuration + Random.Range(bellReactMin, bellReactMax);
    }

    void DoLure()
    {
        lurePending = false;
        int target = lureTarget;
        bool fromWindow = lureFromWindow;
        lureTarget = -1;
        lureFromWindow = false;

        if (!IsValidRoom(target))
        {
            // (안전장치) 갈 방이 없음 — 창문 쪽에서 떠나던 중이었으면 길을 잃으니 스폰 전으로
            if (!IsValidRoom(currentRoom)) ResetToSpawnWait();
            return;
        }

        state = State.Moving;
        if (fromWindow) SetWindowPresence(false);   // 창문 쪽을 떠남 → 조명 정상 + 원래 모습
        MoveToRoom(target, "종에 끌려옴");
        moveCounter = 0f;   // 바로 다시 안 움직이게
        secondTimer = 0f;

        // 맛보기 밤: 창문 쪽에서 종으로 끌어낸 거면 오늘은 끝 (물러나며 사라짐)
        if (IsTasteNight && fromWindow) EndTaste();
    }

    // ================= 비상 종 (EmergencyBell이 호출) =================
    // 돌아다니는 중(유인 반응 중 포함)이든 창문앞이든 100% 스폰 전으로. 등장 전·빼꼼·관리실 안은 헛방(false).
    // 비용(횟수·쿨·배터리)은 EmergencyBell이 무조건 소모.
    public bool EmergencyPush()
    {
        if (!enabled || AI <= 0) return false;
        if (state == State.Spawning || state == State.Peeking || state == State.Inside) return false;

        Debug.Log("★★ 비상 종! 연호 완전 후퇴 (스폰 전으로)");
        ResetToSpawnWait();
        if (IsTasteNight) EndTaste();   // 맛보기 밤: 비상 종으로 밀어냈으면 오늘은 끝
        return true;
    }

    // 스폰 전으로 (모습 숨기고 등장 대기부터 다시)
    void ResetToSpawnWait()
    {
        if (model != null) model.SetActive(false);
        state = State.Spawning;
        spawnTimer = 0f;
        spawnWait = Random.Range(spawnDelayMin, spawnDelayMax);
        currentRoom = -1;
        moveCounter = 0f;
        totalTurns = 0;
        secondTimer = 0f;
        confrontTimer = 0f;
        entryTimer = 0f;
        lurePending = false;
        lureTarget = -1;
        lureFromWindow = false;
        if (sounds != null) sounds.SetStorageListening(false);
        SetWindowPresence(false);
        SetStatus("등장 대기");
    }

    // 창문 쪽에 있나 → 창문앞 복도 조명 깜빡 + 실루엣 (바뀔 때만)
    void SetWindowPresence(bool on)
    {
        if (atWindowSide == on) return;
        atWindowSide = on;
        if (windowLight != null) windowLight.SetFlicker(on);
        if (silhouette != null) silhouette.SetOn(on);
    }

    // 2일차 맛보기 끝 — 오늘 밤 더 안 나옴
    void EndTaste()
    {
        tasteDone = true;
        ResetToSpawnWait();
        SetStatus("오늘 밤 끝 (맛보기)");
        Debug.Log("★ 연호 맛보기 끝 (창문에서 밀어냄) — 오늘 밤 더 안 나옴");
    }

    // 배터리 방전 암전 연출용 (BatteryBlackout가 호출) — AI는 꺼진 상태(enabled=false)에서 창문앞에 모습만 드러냄
    public void AppearAtWindowForBlackout()
    {
        if (windowFrontPos != null)
            transform.SetPositionAndRotation(windowFrontPos.position, windowFrontPos.rotation);
        if (model != null) model.SetActive(true);
    }

    void SetStatus(string s)
    {
        status = s;
    }
}

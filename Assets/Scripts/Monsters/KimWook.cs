using UnityEngine;
using System.Text.RegularExpressions;

// 김욱 (팬텀 벌룬보이) — 엔진 B 큐형. 직접 죽이지 않는 "빈틈 만드는" 귀신. 기획서 §3 / 구현노트 §15.12
//
// 원작 팬텀 BB식 "기습형": 보던 화면에 끼어들고, 2초 안에 피하면 사라짐.
//
// [등장] 20초마다 주사위 rand(0~9) < 밤별 AI → 성공하면 등장 (지직 스태틱 한 번).
//        - CCTV 켜져 있으면 currentCamChance(50%) 확률로 "지금 보는 카메라"에 끼어듦
//        - 아니면 CAM01~06 중 랜덤 (CAM07 창고는 화면이 원래 안 보여서 제외)
//        실패하면 그 20초는 안 나옴 → 리듬을 읽을 수 없음.
// [위험] 그 카메라 화면을 밤별 노출 한계(지금 2초) 보면 → 점프스케어(CCTV 강제로 내려감) + 환기 오류.
// [피함] 한 번 본 뒤 플레이어가 "직접" 다른 방으로 넘기거나 CCTV 끄면 → 그 자리에서 사라짐 (조용히).
//        다른 귀신 때문에 지직거리는 동안은 피한 게 아님 → 시간만 잠깐 멈춤.
// [안 마주침] 아무도 안 보면 다음 주사위 때 조용히 사라지거나 옮김 ("아직 있나?" 불안 유지 — 사라질 땐 지직 없음).
//
// KimWook 모델에 직접 붙임. 숨길 때는 오브젝트를 끄지 않고 렌더러만 끔(스크립트가 계속 돌아야 해서).
public class KimWook : MonoBehaviour
{
    [Header("엔진 B — 밤별 (인덱스 = 밤 숫자, [0]은 안 쓰는 칸)")]
    [Tooltip("rand(0~9) < AI 이면 등장. 0 = 비활성. 기획서: 1~2일 비활성 / 3일 낮음 / 4일 보통 / 5일 높음 (숫자는 임의로 3/5/7)")]
    public int[] aiByNight = { 0, 0, 0, 3, 5, 7 };
    [Tooltip("테스트용: 0 이상이면 밤 상관없이 이 AI 사용. -1 = 끔(평소)")]
    public int testAiOverride = -1;
    public int AI => (testAiOverride >= 0) ? testAiOverride : GameManager.ByNight(aiByNight);
    public float cycleTime = 20f;       // 주사위 굴리는 간격 (고정)
    [Range(0f, 1f)] public float currentCamChance = 0.5f;   // CCTV 켜져 있을 때 "지금 보는 카메라"에 끼어들 확률

    [Header("노출 — 밤별")]
    [Tooltip("이만큼 보면 공격(초). 기획서: 3일 김 → 5일 짧음 (숫자 미정이라 지금 값 2초로 채움)")]
    public float[] exposureLimitByNight = { 0f, 2f, 2f, 2f, 2f, 2f };
    float ExposureLimit => GameManager.ByNight(exposureLimitByNight);

    [Header("카메라 앞 위치")]
    [Tooltip("비워두면 nodeParent(없으면 'KW_MovePoint')의 자식들을 이름 속 CAM 번호로 자동 정렬해서 채움")]
    public Transform[] camNodes;        // 인덱스 = roomFeeds 인덱스 (0=CAM01 공부방1 ... 5=CAM06 세로복도)
    public Transform nodeParent;

    [Header("참조 (비워두면 자동으로 찾음)")]
    public MonitorDisplay monitor;
    public CCTVController cctv;
    public VentilationSystem vent;
    public JumpscareOverlay jumpscare;

    [Header("상태 (확인용)")]
    public int queuedRoom = -1;         // 지금 떠 있는 카메라 (-1 = 없음)
    public float exposure = 0f;
    public bool seen = false;           // 이번 등장에서 플레이어가 한 번이라도 봤나 (봤으면 넘기는 순간 사라짐)

    private float cycleTimer;
    private Renderer[] renderers;

    void Start()
    {
        if (monitor == null) monitor = FindAnyObjectByType<MonitorDisplay>();
        if (cctv == null) cctv = FindAnyObjectByType<CCTVController>();
        if (vent == null) vent = FindAnyObjectByType<VentilationSystem>();
        if (jumpscare == null) jumpscare = FindAnyObjectByType<JumpscareOverlay>();

        renderers = GetComponentsInChildren<Renderer>(true);
        if (camNodes == null || camNodes.Length == 0) AutoFillNodes();

        SetVisible(false);
        cycleTimer = cycleTime;
    }

    void Update()
    {
        if (AI <= 0) return;   // AI 0 = 비활성 (오늘 밤 안 나옴)

        cycleTimer -= Time.deltaTime;
        if (cycleTimer <= 0f)
        {
            cycleTimer = cycleTime;
            Roll();
        }

        if (queuedRoom >= 0) UpdateExposure();
    }

    // === 20초마다 주사위 ===
    void Roll()
    {
        Leave();   // 일단 사라짐 (계속 있을지는 이번 주사위로 다시 결정)

        if (Random.Range(0, 10) >= AI) return;   // 이번엔 안 나옴

        int room = PickRoom();
        if (room >= 0) Appear(room);
    }

    int PickRoom()
    {
        // CCTV 켜져 있으면 절반 확률로 "지금 보는 카메라"에 끼어듦 (그 방에 위치가 있을 때만, 창고 제외)
        if (cctv != null && cctv.isCameraDown && monitor != null && Random.value < currentCamChance)
        {
            int cur = monitor.currentRoom;
            if (IsValidRoom(cur)) return cur;
        }

        var candidates = new System.Collections.Generic.List<int>();
        for (int i = 0; i < camNodes.Length; i++)
            if (IsValidRoom(i)) candidates.Add(i);
        return candidates.Count > 0 ? candidates[Random.Range(0, candidates.Count)] : -1;
    }

    bool IsValidRoom(int i)
    {
        int storage = (monitor != null) ? monitor.storageRoomIndex : 6;
        return i >= 0 && i < camNodes.Length && camNodes[i] != null && i != storage;
    }

    void Appear(int room)
    {
        queuedRoom = room;
        exposure = 0f;
        seen = false;
        transform.position = camNodes[room].position;
        transform.rotation = camNodes[room].rotation;
        SetVisible(true);
        if (monitor != null) monitor.GhostMoveStatic();   // 다른 귀신 이동처럼 지직 한 번
        Debug.Log($"★ 김욱 등장 — CAM{room + 1:00}");
    }

    void Leave()
    {
        if (queuedRoom < 0) return;
        queuedRoom = -1;
        exposure = 0f;
        seen = false;
        SetVisible(false);   // 조용히 (지직 없음)
    }

    // === 보고 있나 / 피했나 ===
    void UpdateExposure()
    {
        // 플레이어가 김욱 카메라에 "머물러" 있나 (CCTV 켜져 있고 그 방 선택 중)
        bool onHisCam = cctv != null && cctv.isCameraDown
                        && monitor != null && monitor.currentRoom == queuedRoom;

        // 실제로 화면에 보이나 (전환 스태틱·고장 스태틱 중엔 안 보임)
        bool cameraBroken = monitor != null && monitor.cameraSystem != null && monitor.cameraSystem.IsCameraBroken();
        bool visibleNow = onHisCam && monitor.state == MonitorDisplay.State.Active && !cameraBroken;

        if (visibleNow)
        {
            seen = true;
            exposure += Time.deltaTime;
            if (exposure >= ExposureLimit) Attack();
            return;
        }

        // 한 번 본 뒤 직접 다른 방으로 넘기거나 CCTV 끔 → 피함 → 그 자리에서 사라짐
        if (seen && !onHisCam)
        {
            Debug.Log($"김욱 피함 (CAM{queuedRoom + 1:00}에서 {exposure:F1}초 만에 빠져나감)");
            Leave();
            return;
        }

        // 그 외(아직 안 봄 / 다른 귀신 때문에 지직거리는 중 / 카메라 고장 중) → 시간만 멈춤
    }

    // === 공격: 점프스케어(CCTV 강제로 내려감) + 환기 오류 ===
    void Attack()
    {
        Debug.Log($"★★ 김욱 공격! CAM{queuedRoom + 1:00}를 너무 오래 봄 → 환기 오류");
        Leave();
        if (jumpscare != null) jumpscare.Play();        // CCTV/패널 강제 닫힘 → 그게 화면 전환이라 연호·현승 연쇄
        if (vent != null) vent.BreakVent("김욱");
        // 연호 흉포는 YeonhoAggression이 "환기 오류 중" 상태를 보고 자동으로 켬
    }

    void SetVisible(bool on)
    {
        if (renderers == null) return;
        foreach (var r in renderers) r.enabled = on;
    }

    // KW_MovePoint 자식들을 이름 속 "CAM숫자"로 정렬해서 채움 (예: "Bathroom(CAM4)" → 인덱스 3)
    void AutoFillNodes()
    {
        if (nodeParent == null)
        {
            var go = GameObject.Find("KW_MovePoint");
            if (go != null) nodeParent = go.transform;
        }
        if (nodeParent == null)
        {
            Debug.LogWarning("김욱: 카메라 앞 위치를 못 찾음 (camNodes 비어 있고 KW_MovePoint도 없음)");
            camNodes = new Transform[0];
            return;
        }

        camNodes = new Transform[6];
        foreach (Transform child in nodeParent)
        {
            var m = Regex.Match(child.name, @"cam\s*0?(\d+)", RegexOptions.IgnoreCase);
            if (!m.Success) continue;
            int idx = int.Parse(m.Groups[1].Value) - 1;
            if (idx >= 0 && idx < camNodes.Length) camNodes[idx] = child;
        }
    }
}

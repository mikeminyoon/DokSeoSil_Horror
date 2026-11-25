using UnityEngine;

// 준영 (팬텀 폭시) — AI 없음, 확률 출현. 직접 죽이지 않는 "빈틈 만드는" 귀신. 구현노트 §13·§15.13
// 김욱과 정반대: 김욱 = CCTV 보면 위험 / 준영 = CCTV 켜야 안전.
//
// [주사위] CCTV를 올릴 때마다 그 밤 확률로 굴림 (3일 1/50, 4일 1/25, 5일 1/10).
// [몰래 자리잡기] 성공하면 "카메라가 모니터 앞에 다 들어감 + 준영 자리가 플레이어 화면 밖"일 때만 나타남.
//                → 생기는 순간이 절대 안 보임. 조건이 한 번도 안 맞고 CCTV 내리면 이번 주사위는 없던 걸로.
// [드러남] CCTV 내리면 관리실 구석에 원래부터 서 있던 것처럼 보임 → 유예(5초) 시작.
// [피함] 유예 안에 CCTV를 "제대로" 올림(카메라가 다 들어감) → 사라짐 (역시 화면 밖일 때만). 20~30초 쉼.
//        CCTV 올린 동안은 유예가 멈춤 (정답 행동 중엔 안 터짐). 다 들어가기 전에 내리면 피한 게 아님.
// [armed] 유예 중 정면에서 다른 구역으로 돌림, 또는 다른 구역에 있는 채로 유예 끝남.
// [공격] 정면 보는 채로 유예 끝남, 또는 armed 상태로 정면 복귀 → 점프스케어 + 환기 오류 → 그날 퇴장.
//
// 원래 기획의 "창문 가리기"는 뺌 (응시 판정과 안 맞고, 딜레마는 규칙으로 이미 생김).
// JoonYoung 모델에 직접 붙임. 숨길 때는 렌더러만 끔. 모델은 제자리(관리실 구석)에서 안 움직임.
public class JoonYoung : MonoBehaviour
{
    [Header("밤별 출현 확률 (CCTV 올릴 때마다) — 인덱스 = 밤 숫자, [0]은 안 쓰는 칸")]
    public float[] chanceByNight = { 0f, 0f, 0.001f, 0.02f, 0.04f, 0.1f };   // 1일 0 / 2일 1/1000 / 3일 1/50 / 4일 1/25 / 5일 1/10
    [Tooltip("테스트용: 0~1이면 밤 확률 대신 이 값 사용. -1이면 끔 (평소)")]
    public float testChanceOverride = -1f;

    [Header("시간")]
    [Tooltip("밤별 유예(초) — 드러난 뒤 CCTV 올릴 때까지. [0]은 안 쓰는 칸. 기획서: 3일 김 → 5일 짧음 (숫자 미정이라 지금 값 5초로 채움)")]
    public float[] graceTimeByNight = { 0f, 5f, 5f, 5f, 5f, 5f };
    float GraceTime => GameManager.ByNight(graceTimeByNight);
    public float restAfterDodgeMin = 20f;     // 피한 뒤 주사위 안 굴리는 시간
    public float restAfterDodgeMax = 30f;

    [Header("참조 (비워두면 자동으로 찾음)")]
    public CCTVController cctv;
    public ViewController viewController;
    public VentilationSystem vent;
    public JumpscareOverlay jumpscare;
    public int centerZone = 1;

    private enum State { Absent, Pending, Hidden, Revealed, Armed, Leaving, Done }
    [Header("상태 (확인용)")]
    [SerializeField] private State state = State.Absent;
    [SerializeField] private float graceLeft = 0f;
    [SerializeField] private float restLeft = 0f;

    private Renderer[] renderers;
    private Bounds worldBounds;          // 숨기기 전에 한 번 재둔 크기 (제자리라 안 바뀜)
    private bool lastCameraDown = false;
    private bool loggedWaiting = false;

    void Start()
    {
        if (cctv == null) cctv = FindAnyObjectByType<CCTVController>();
        if (viewController == null) viewController = FindAnyObjectByType<ViewController>();
        if (vent == null) vent = FindAnyObjectByType<VentilationSystem>();
        if (jumpscare == null) jumpscare = FindAnyObjectByType<JumpscareOverlay>();

        renderers = GetComponentsInChildren<Renderer>(true);
        worldBounds = CalcBounds();
        SetVisible(false);

        lastCameraDown = cctv != null && cctv.isCameraDown;
    }

    void Update()
    {
        if (cctv == null || state == State.Done) return;

        bool camDown = cctv.isCameraDown;
        bool raisedNow = camDown && !lastCameraDown;     // CCTV 올린 순간
        bool loweredNow = !camDown && lastCameraDown;    // CCTV 내린 순간
        lastCameraDown = camDown;

        if (restLeft > 0f) restLeft -= Time.deltaTime;

        switch (state)
        {
            case State.Absent:
                if (raisedNow) TryRoll();
                break;

            case State.Pending:
                // CCTV 다 들어갔고 화면 밖이면 몰래 자리잡기
                if (cctv.isFullyIn && TryToggleUnseen(true))
                {
                    state = State.Hidden;
                    Debug.Log("준영: 관리실 구석에 몰래 자리잡음 (CCTV 내리면 드러남)");
                }
                else if (loweredNow)
                {
                    state = State.Absent;   // 이번엔 못 잡음 → 없던 걸로
                    Debug.Log("준영: 자리 못 잡고 CCTV 내려짐 → 이번 주사위 무효");
                }
                break;

            case State.Hidden:
                if (loweredNow)
                {
                    state = State.Revealed;
                    graceLeft = GraceTime;
                    Debug.Log($"★ 준영 드러남 (관리실 구석) — {GraceTime:F0}초 안에 CCTV 켜야 함");
                }
                break;

            case State.Revealed:
                UpdateRevealed();
                break;

            case State.Armed:
                if (viewController != null && viewController.currentZone == centerZone)
                    Attack();
                break;

            case State.Leaving:
                // 피해서 사라지는 중 — 화면 밖일 때 몰래 사라짐
                if (TryToggleUnseen(false))
                {
                    state = State.Absent;
                    Debug.Log("준영: 사라짐");
                }
                break;
        }
    }

    void TryRoll()
    {
        if (restLeft > 0f) return;

        float chance = (testChanceOverride >= 0f) ? testChanceOverride : ChanceForTonight();
        if (Random.value >= chance) return;

        state = State.Pending;
        loggedWaiting = false;
    }

    float ChanceForTonight()
    {
        return GameManager.ByNight(chanceByNight);
    }

    void UpdateRevealed()
    {
        // CCTV 올린 동안 = 정답 행동 중 → 유예 멈춤. 다 들어가면 피함.
        if (cctv.isCameraDown)
        {
            if (cctv.isFullyIn)
            {
                state = State.Leaving;
                restLeft = Random.Range(restAfterDodgeMin, restAfterDodgeMax);
                Debug.Log($"준영 피함 (CCTV 켬) — {restLeft:F0}초 쉼");
            }
            return;
        }

        bool inCenter = viewController == null || viewController.currentZone == centerZone;

        // 정면에서 다른 구역으로 돌림 → armed
        if (!inCenter)
        {
            state = State.Armed;
            Debug.Log("★ 준영 ARMED (무시하고 고개 돌림) — 정면 돌아오면 점프스케어");
            return;
        }

        graceLeft -= Time.deltaTime;
        if (graceLeft <= 0f) Attack();   // 정면 보는 채로 유예 끝 → 바로
    }

    // === 공격: 점프스케어 + 환기 오류 → 그날 퇴장 ===
    void Attack()
    {
        Debug.Log("★★ 준영 공격! → 환기 오류 (오늘은 퇴장)");
        state = State.Done;
        if (jumpscare != null) jumpscare.Play();   // 점프스케어 화면이 덮는 동안 숨김
        SetVisible(false);
        if (vent != null) vent.BreakVent("준영");
        // 연호 흉포는 YeonhoAggression이 "환기 오류 중" 상태를 보고 자동으로 켬
    }

    // 플레이어 화면에 안 보일 때만 켜고/끔. 성공하면 true.
    bool TryToggleUnseen(bool show)
    {
        if (IsOnPlayerScreen())
        {
            if (!loggedWaiting)
            {
                Debug.Log("준영: 화면에 보여서 대기 중 (보이면 안 생기고 안 사라짐 — 계속 뜨면 위치를 화면 밖 쪽으로 옮길 것)");
                loggedWaiting = true;
            }
            return false;
        }
        SetVisible(show);
        loggedWaiting = false;
        return true;
    }

    // 플레이어 화면에 보이나?
    // 준영 몸 안쪽 7개 지점 중 하나라도 "화면 범위 안 + 카메라와 그 점 사이를 막는 충돌체 없음"이면 보임.
    // (시야각이 넓어서 범위만 보면 모니터 뒤에 가려진 것도 '보임'이 돼버려서 가려짐까지 봄)
    // 충돌체 없는 물체(예: CrtMonitor 몸체)는 선이 통과 → '보임'으로 침 = 안전한 쪽 실수(튀어 보일 일은 없음)
    bool IsOnPlayerScreen()
    {
        Camera cam = cctv != null ? cctv.PlayerCamera : null;
        if (cam == null) return false;

        Vector3 c = worldBounds.center;
        Vector3 e = worldBounds.extents * 0.4f;   // 바깥 끝 말고 안쪽 (발끝이 바닥에 걸리는 오판 방지)
        Vector3[] points =
        {
            c,
            c + Vector3.up * e.y,    c - Vector3.up * e.y,
            c + Vector3.right * e.x, c - Vector3.right * e.x,
            c + Vector3.forward * e.z, c - Vector3.forward * e.z,
        };

        Vector3 camPos = cam.transform.position;
        foreach (var p in points)
        {
            Vector3 vp = cam.WorldToViewportPoint(p);
            if (vp.z <= 0f || vp.x < 0f || vp.x > 1f || vp.y < 0f || vp.y > 1f) continue;   // 화면 범위 밖

            // 카메라 → 점 사이에 다른 충돌체가 있으면 가려짐 (트리거·준영 자신은 무시)
            float dist = Vector3.Distance(camPos, p);
            if (Physics.Raycast(camPos, (p - camPos) / dist, out RaycastHit hit, dist - 0.05f, ~0, QueryTriggerInteraction.Ignore)
                && !hit.transform.IsChildOf(transform))
                continue;

            return true;   // 이 점은 실제로 보임
        }
        return false;
    }

    Bounds CalcBounds()
    {
        if (renderers == null || renderers.Length == 0) return new Bounds(transform.position, Vector3.one);
        Bounds b = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);
        return b;
    }

    void SetVisible(bool on)
    {
        if (renderers == null) return;
        foreach (var r in renderers) r.enabled = on;
    }
}

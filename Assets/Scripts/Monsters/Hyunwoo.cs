using UnityEngine;
using UnityEngine.Serialization;

// 현우: 환풍구 전용 귀신 (엔진B 이동 + 셔터 판정 A). 현우 모델에 붙인다.
// 허공(등장대기) → 공부방2 앉음 → 환풍구 매달림 → 화장실 → 관리실 환풍구(입구).
//
// [관리실 환풍구 — 구현노트 §15.19]
//  - 평소엔 암흑이라 안 보임. 불빛 버튼 누르고 있을 때만 보임 (VentLightReveal).
//  - 도착은 플레이어가 환풍구를 못 볼 때만 — 불빛 켜져 있으면 끄는 순간 도착 ("끄는 순간 도착").
//  1. 판정: 셔터 닫고 5초 유지 → 격퇴 (셔터 뒤라 사라지는 게 안 보임)
//  2. 판정 시간 지남 → armed: 아직 환풍구에 있음 (셔터 닫아도 소용없음, 고개 돌려도 아직 안 당함)
//  3. 화면 전환 또는 comeBehindTime(6초) → 등 뒤로 옴 = 환풍구에서 사라짐 (격퇴랑 똑같이 보임 = 심리전)
//  4. 등 뒤에 있을 때 환풍구 말고 다른 구역으로 시야를 돌리는 순간 → 갑툭튀 (스택+1)
//     환풍구 쪽으로 돌리는 건 안전 — 빈 환풍구 보고 "사라졌네?" → 다른 데로 돌리면 뒤에서 튀어나옴
//
// [스택 후폭풍 — 3일차~, 구현노트 §15.18]
//  1 조명 깜빡 / 2 조명 나감 / 3 배선 고장(밤당 1회).  (1스택 "잠깐 시야 흐림"은 유저가 뺌)
//  배선 고장 낸 뒤 또 당하면 2일차처럼 배터리만 깎음 (안 그러면 그 뒤 현우가 벌 없는 귀신이 됨).
public class Hyunwoo : MonoBehaviour
{
    [Header("환풍구 루트 노드")]
    public Transform[] nodes;   // [허공, 공부방2앉음, 환풍구매달림, 화장실, 관리실입구]
    [Tooltip("관리실 환풍구 노드 번호 (불빛에만 보이는 곳)")]
    public int roomVentNode = 4;

    [Header("이동 판정 — 밤별 AI (인덱스 = 밤 숫자, [0]은 안 쓰는 칸)")]
    [Tooltip("기획서 §8: 1일 0(비활성) / 2일 5 / 3일 6 / 4일 10 / 5일 13. rand(0~19) < AI 이면 이동")]
    public int[] aiByNight = { 0, 0, 5, 6, 10, 13 };
    [Tooltip("테스트용: 0 이상이면 밤 상관없이 이 AI 사용. -1 = 끔(평소)")]
    public int testAiOverride = -1;
    public int AI => (testAiOverride >= 0) ? testAiOverride : GameManager.ByNight(aiByNight);
    public float cycleTime = 5f;

    [Header("퇴장 (기획서: 5시 이후 현우 퇴장 → 연호가 주역)")]
    public int leaveHour = 5;                // 이 시부터 퇴장 (진행 중인 입구 판정은 끝내고 나감)

    [Header("2일차 처벌 (기획서: 2일차는 스택 없이 배터리만 뭉텅)")]
    public int stackFromNight = 3;           // 이 밤부터 스택. 그 전 밤엔 배터리만 깎음
    public float earlyNightBatteryCost = 15f;
    public int fixedNodeCount = 3;   // 앞 3노드(허공/앉음/매달림)는 고정 시간

    [Header("고정 노드 대기 시간 (노드별, 랜덤)")]
    [Tooltip("노드0(허공)은 아래 밤별 배율이 곱해짐 — 여기 값 = 배율 1일 때(2일차 기준)")]
    public Vector2[] fixedNodeWaits = {
        new Vector2(10f, 30f),   // 노드0: 허공 (등장 대기) × 밤별 배율
        new Vector2(10f, 20f),   // 노드1: 앉음
        new Vector2(3f, 7f),     // 노드2: 환풍구 매달림
    };
    [Tooltip("밤별 허공 대기 배율 (인덱스 = 밤 숫자, [0]은 안 쓰는 칸). 초반엔 별로 안 오고 후반엔 자주 오게 (유저 요청)")]
    public float[] voidWaitScaleByNight = { 0f, 1f, 1f, 0.7f, 0.45f, 0.3f };
    float VoidWaitScale => Mathf.Max(0f, GameManager.ByNight(voidWaitScaleByNight));

    [Header("셔터 판정 A")]
    [Tooltip("셔터 열린 동안 이만큼 지나면 침투(armed)")]
    public float infiltrationLimit = 15f;
    [Tooltip("셔터 닫고 이만큼 유지하면 격퇴")]
    public float holdRequired = 5f;
    [Tooltip("초당 조기 이탈 확률 (FNAF2식 1/10)")]
    public float earlyLeaveChance = 0.1f;

    [Header("등 뒤로 (판정 시간 지난 뒤)")]
    [Tooltip("armed 후 화면 전환을 안 해도 이만큼 지나면 알아서 등 뒤로 옴 (불빛 계속 켜두는 사람 대비)")]
    [FormerlySerializedAs("armedGrace")]
    public float comeBehindTime = 6f;
    [Tooltip("등 뒤로 온 뒤 이만큼 지나면 고개 안 돌려도 갑툭튀. 0 = 끔 (고개 돌릴 때만)")]
    public float behindAttackTime = 0f;

    [Header("참조")]
    public MonitorDisplay monitor;
    public ShutterController shutter;        // 셔터 닫힘·불빛 상태 읽기
    public ViewController viewController;    // 시야 구역 읽기 (등 뒤 갑툭튀 감지)
    public ScreenTransitionDetector transitionDetector;   // 화면 전환 → 등 뒤로
    public JumpscareOverlay jumpscare;       // 침투 당했을 때 잔상
    public Animator animator;                // 포즈 전환 (앉음/매달림/벤트입구)
    public int ventZone = 2;                 // 환풍구 구역 번호

    [Header("스택 (현우 전용)")]
    public int stackCount = 0;               // 1 조명 깜빡 / 2 조명 나감 / 3 배선 고장
    public int wiringStack = 3;              // 이 스택이면 배선 고장
    public RoomLights roomLights;            // 비워두면 자동 (씬에 없으면 만듦)
    public WiringFailure wiring;             // 비워두면 자동 (씬에 없으면 만듦)
    private bool stacksSpent = false;        // 오늘 밤 배선 고장 이미 냄 → 이후엔 배터리만

    // 상태
    private enum State { Moving, AtEntrance, Armed, Behind }
    private State state = State.Moving;

    // 이동
    private int currentNode = 0;
    private float timer = 0f;
    private float currentWait = 0f;
    private bool pendingVentArrival = false; // 관리실 환풍구로 올 차례인데 불빛 켜져 있어서 대기 중

    // 판정 타이머
    private float infiltrationTimer = 0f;    // 침투까지 (셔터 열린 동안 참)
    private float holdTimer = 0f;            // 셔터 유지 (닫힌 동안 참)
    private float earlyLeaveTimer = 0f;      // 조기 이탈 굴림용 (초당)
    private float armedTimer = 0f;           // armed → 등 뒤로까지
    private float behindTimer = 0f;          // 등 뒤 → 자동 갑툭튀까지 (behindAttackTime > 0일 때만)
    private int behindLastZone = -1;         // 등 뒤에 온 뒤 시야 구역 변화 감지
    private bool retired = false;            // 퇴장함 (오늘 밤 더 안 나옴)

    private VentLightReveal reveal;          // 관리실 환풍구에선 불빛에만 보이게

    void Start()
    {
        if (monitor == null) monitor = FindAnyObjectByType<MonitorDisplay>();
        if (shutter == null) shutter = FindAnyObjectByType<ShutterController>();
        if (viewController == null) viewController = FindAnyObjectByType<ViewController>();
        if (transitionDetector == null) transitionDetector = FindAnyObjectByType<ScreenTransitionDetector>();
        if (jumpscare == null) jumpscare = FindAnyObjectByType<JumpscareOverlay>();
        if (animator == null) animator = GetComponent<Animator>();
        if (roomLights == null) roomLights = RoomLights.GetOrCreate();
        if (wiring == null) wiring = WiringFailure.GetOrCreate();

        reveal = GetComponent<VentLightReveal>();
        if (reveal == null) reveal = gameObject.AddComponent<VentLightReveal>();
        reveal.shutter = shutter;

        // 화면 전환 방송 구독 — armed 중이면 등 뒤로
        if (transitionDetector != null)
            transitionDetector.OnScreenTransition += HandleScreenTransition;

        if (nodes.Length > 0) MoveToNode(0);
    }

    void OnDestroy()
    {
        if (transitionDetector != null)
            transitionDetector.OnScreenTransition -= HandleScreenTransition;
    }

    void Update()
    {
        if (retired || AI <= 0) return;   // 퇴장했거나 AI 0 = 비활성 (허공 노드에 머묾 = 안 보임)

        // 5시 퇴장: 이동 중일 때만 (입구 판정 중이면 끝난 뒤 ResetToStart에서 퇴장)
        // 관리실 환풍구에 있는데 플레이어가 불빛으로 보고 있으면 끌 때까지 기다림 (눈앞에서 사라지면 안 됨)
        if (state == State.Moving && GameManager.Hour >= leaveHour && !IsSeenInRoomVent())
        {
            Retire();
            return;
        }

        switch (state)
        {
            case State.Moving:     UpdateMoving();     break;
            case State.AtEntrance: UpdateAtEntrance(); break;
            case State.Armed:      UpdateArmed();      break;
            case State.Behind:     UpdateBehind();     break;
        }
    }

    // 관리실 환풍구에 있고, 플레이어가 불빛으로 보고 있나
    bool IsSeenInRoomVent()
    {
        return currentNode == roomVentNode && VentLightReveal.PlayerSeesVent(shutter);
    }

    // ===== 1. 이동 (엔진B) =====
    void UpdateMoving()
    {
        // 관리실 환풍구 도착 대기 중: 플레이어가 환풍구를 못 보는 순간 도착
        if (pendingVentArrival)
        {
            if (!VentLightReveal.PlayerSeesVent(shutter))
            {
                pendingVentArrival = false;
                MoveToNode(roomVentNode);
            }
            return;
        }

        timer += Time.deltaTime;

        // 고정 노드 구간 (허공/앉음/매달림) — 정해진 랜덤 시간 대기
        if (currentNode < fixedNodeCount)
        {
            if (timer >= currentWait)
            {
                timer = 0f;
                TryMoveTo(currentNode + 1);
            }
        }
        // 확률 이동 구간 (환풍구~)
        else
        {
            if (timer >= cycleTime)
            {
                timer = 0f;
                int roll = Random.Range(0, 20);
                if (roll < AI)
                    TryMoveTo(currentNode + 1);
            }
        }
    }

    // 다음 노드로 — 관리실 환풍구인데 플레이어가 불빛으로 보고 있으면 끌 때까지 미룸
    void TryMoveTo(int index)
    {
        if (index == roomVentNode && VentLightReveal.PlayerSeesVent(shutter))
        {
            pendingVentArrival = true;
            Debug.Log("현우: 불빛 켜져 있어서 환풍구 도착 대기 (끄는 순간 도착)");
            return;
        }
        MoveToNode(index);
    }

    void MoveToNode(int index)
    {
        // (안전장치) 노드 범위 밖 — 판정 시작
        if (index >= nodes.Length)
        {
            ReachEntrance();
            return;
        }

        currentNode = index;
        transform.position = nodes[index].position;
        transform.rotation = nodes[index].rotation;

        // 관리실 환풍구에선 불빛에만 보임
        if (reveal != null) reveal.inRoomVent = (index == roomVentNode);

        // 노드별 포즈 전환 (Animator의 PoseNode Integer로)
        // 1=앉음, 2=매달림, 4=벤트입구. 안 보이는 노드(0 허공,3 화장실)는 아무거나.
        if (animator != null) animator.SetInteger("PoseNode", index);

        // CCTV 스태틱: 노드1(앉음)·2(매달림)·3(화장실)·관리실 환풍구(= 화장실 CAM에서 사라지는 순간, 유저 요청).
        // 허공(0)은 X (등 뒤로 올 때·당한 뒤 처음으로 갈 때 — 관리실 안이라 CCTV와 무관).
        if (index == 1 || index == 2 || index == 3 || index == roomVentNode)
        {
            if (monitor != null) monitor.GhostMoveStatic();
        }

        // 고정 노드면 배열에서 랜덤 대기시간 (허공은 밤별 배율 — 후반일수록 짧게 = 자주 옴)
        if (index < fixedNodeCount && index < fixedNodeWaits.Length)
        {
            currentWait = Random.Range(fixedNodeWaits[index].x, fixedNodeWaits[index].y);
            if (index == 0) currentWait *= VoidWaitScale;
        }

        Debug.Log($"현우 이동 → 노드 {index}");

        // 관리실 환풍구 도착 = 바로 판정 시작
        // (예전엔 도착 후 주사위를 한 번 더 통과해야 판정이 시작돼서, 그동안 셔터를 닫아도 격퇴가 안 되고 배터리·공기만 낭비됐음)
        if (index == roomVentNode) ReachEntrance();
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

    // 격퇴: 쿵쿵 + 환풍구 빔 → 처음으로 복귀 (셔터 닫혀 있어서 사라지는 게 안 보임)
    void Repel(string reason)
    {
        Debug.Log($"★ 현우 격퇴! 쿵쿵 ({reason})");
        // TODO: 쿵쿵 사운드 (Phase 8)
        ResetToStart();
    }

    // ===== 4. armed: 판정 시간 지남 — 아직 환풍구에 있음 =====
    void BecomeArmed()
    {
        state = State.Armed;
        armedTimer = 0f;
        Debug.Log($"★ 현우 armed! 셔터로 막을 기회 지남 — 화면 전환하거나 {comeBehindTime:F0}초 지나면 등 뒤로");
    }

    void UpdateArmed()
    {
        // 화면 전환을 안 해도 시간이 지나면 등 뒤로 (불빛 계속 켜두는 사람 대비)
        armedTimer += Time.deltaTime;
        if (armedTimer >= comeBehindTime)
            ComeBehind($"{comeBehindTime:F0}초 지남");
    }

    // 화면 전환 (CCTV·패널 올림/내림, 환기 오류 강제 전환) — armed 중이면 등 뒤로
    void HandleScreenTransition()
    {
        if (!enabled) return;
        if (state == State.Armed) ComeBehind("화면 전환");
    }

    // ===== 5. 등 뒤로 옴: 환풍구에서 사라짐 (격퇴랑 똑같이 보임) =====
    void ComeBehind(string reason)
    {
        state = State.Behind;
        behindTimer = 0f;
        behindLastZone = (viewController != null) ? viewController.currentZone : -1;
        MoveToNode(0);   // 환풍구에서 사라짐 (허공 노드 = 안 보이는 곳)
        Debug.Log($"★ 현우 등 뒤로! ({reason}) — 환풍구 빔. 환풍구 말고 다른 데로 고개 돌리면 갑툭튀");
        // TODO: 쿵쿵 사운드 (격퇴랑 똑같이 — 심리전, Phase 8)
    }

    void UpdateBehind()
    {
        // 시야 구역이 바뀌는 순간: 환풍구 쪽이면 안전, 그 외 구역이면 갑툭튀
        int zone = (viewController != null) ? viewController.currentZone : behindLastZone;
        if (zone != behindLastZone)
        {
            behindLastZone = zone;
            if (zone != ventZone)
            {
                GetHit("등 뒤 — 고개 돌림");
                return;
            }
        }

        // (옵션) 고개를 안 돌려도 시간 지나면 갑툭튀
        if (behindAttackTime > 0f)
        {
            behindTimer += Time.deltaTime;
            if (behindTimer >= behindAttackTime)
                GetHit("등 뒤 — 시간 초과");
        }
    }

    // ===== 6. 당함: 스택+1 + 잔상 (비즉사) =====
    // 스택 시작 전 밤(2일차) 또는 오늘 밤 배선 고장을 이미 냈으면 → 스택 대신 배터리만 뭉텅.
    void GetHit(string reason)
    {
        if (GameManager.Night < stackFromNight || stacksSpent)
        {
            if (jumpscare != null) jumpscare.Play();   // 잔상 연출 (등 뒤 갑툭튀 애니는 Phase 8)
            if (BatteryManager.Instance != null) BatteryManager.Instance.Drain(earlyNightBatteryCost);
            string why = stacksSpent ? "오늘 배선 고장 이미 냄" : $"{GameManager.Night}일차";
            Debug.Log($"★★ 현우 갑툭튀! ({why}) 스택 대신 배터리 -{earlyNightBatteryCost:F0} — {reason}");
            ResetToStart();
            return;
        }

        stackCount++;
        Debug.Log($"★★ 현우 갑툭튀! 스택 +1 (현재 {stackCount}) — {reason}");

        // ⚠️ 후폭풍을 잔상보다 먼저: 배선 고장이 패널을 강제로 닫으면서 시야 회전을 다시 켜는데,
        //    잔상(Play)이 그 뒤에 와야 잔상 동안 조작 잠금이 유지됨
        ApplyStackEffect(stackCount);
        if (jumpscare != null) jumpscare.Play();

        ResetToStart();
    }

    // 스택별 후폭풍 (기획서 §4.4 + 구현노트 §12.2 — 2스택은 CCTV 노이즈 → 조명 나감으로 변경됨)
    void ApplyStackEffect(int stack)
    {
        if (stack >= wiringStack)
        {
            stacksSpent = true;
            if (wiring != null) wiring.Begin($"현우 {stack}스택");
            return;
        }

        if (roomLights == null) return;
        roomLights.SetStage(stack);                  // 1 깜빡 / 2 나감 (시야 흐림은 유저가 뺌)
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
        behindTimer = 0f;
        earlyLeaveTimer = 0f;
        pendingVentArrival = false;
        MoveToNode(0);
        // 퇴장 시간 지났으면 Update 첫 줄에서 바로 Retire (이미 허공 노드라 안 보임)
    }

    // 퇴장: 허공 노드로 사라지고 오늘 밤 더 안 나옴
    void Retire()
    {
        retired = true;
        pendingVentArrival = false;
        if (currentNode != 0)
        {
            if (monitor != null) monitor.GhostMoveStatic();   // 보던 화면에서 사라질 때 지직 (다른 이동과 같게)
            MoveToNode(0);
        }
        Debug.Log($"★ 현우 퇴장 ({leaveHour}시) — 오늘 밤 더 안 나옴");
    }
}

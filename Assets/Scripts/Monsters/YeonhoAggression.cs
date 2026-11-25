using UnityEngine;
using UnityEngine.Serialization;

// 연호 흉포 토글 — "연쇄 붕괴의 엔진" (기획서 §2.5, 구현노트 §15.14)
// 다른 귀신에게 당하거나 방심하면 연호가 미쳐 날뛴다.
//
// [원인] 하나라도 해당하면 흉포 ON
//   - 오디오 고장 중 (현승 STRIKE1 / 종 과사용)
//   - 환기 오류 중 (김욱 / 준영 / 셔터 장시간 폐쇄)
//   - 흉포 시간대 (furyHourStart ~ furyHourEnd 시)
//   - 방치: idleLimit초 동안 조작 없음 (시야 회전 / 옵션: CCTV 방 전환)
//   - 확률: rollInterval초마다 rand(0~randomDie-1) < 연호 AI
//   - TODO: 정전(현우 3스택 배선 고장) — 만들 때 CheckConditions에 한 줄 추가
// [유지] 원인이 사라진 뒤에도 furyMinDuration초는 흉포 유지 → 그 뒤 OFF.
//        (확률로 걸리면 딱 이 시간만큼. 고장처럼 계속되는 원인이면 고장 중엔 계속 흉포.)
// [효과] 연호(Yeonho.cs)가 IsAggressive를 읽음: 매초 이동 카운터 +2(평소 +1), 이동 판정 기준 -1. 이동에만 영향, 종과 무관.
//
// 씬에 없으면 연호가 자동으로 붙임. Inspector에서 조절하려면 연호 오브젝트에 미리 붙여둘 것.
public class YeonhoAggression : MonoBehaviour
{
    [Header("유지")]
    [Tooltip("원인이 사라진 뒤에도 흉포가 유지되는 시간(초). 확률로 걸리면 딱 이만큼만 흉포.")]
    public float furyMinDuration = 8f;

    [Header("확률")]
    [FormerlySerializedAs("checkInterval")]
    public float rollInterval = 15f;      // 확률 주사위 굴리는 간격 (유지 시간과 무관)
    public int randomDie = 20;            // rand(0 ~ randomDie-1) < AI 이면 흉포 (문서 원안 5는 3일차부터 100%라 20으로)

    [Header("흉포 시간대 (게임 시각)")]
    [Tooltip("이 시부터 흉포 (12시=0으로 침). 시작=끝이면 시간대 흉포 없음.")]
    [FormerlySerializedAs("furyHour")]
    public int furyHourStart = 5;         // 5~6시 = 마지막 한 시간 (현우 퇴장 후 "연호만 남았다")
    [Tooltip("이 시 전까지 흉포 (6 = 밤 끝까지)")]
    public int furyHourEnd = 6;

    [Header("방치 (방심 처벌)")]
    public float idleLimit = 10f;         // 이만큼 조작 없으면 흉포
    [Tooltip("켜면 CCTV 안에서 다른 방으로 넘기는 것도 조작으로 침 (CCTV 오래 봐도 방을 넘기면 방치 아님). 끄면 시야 회전만.")]
    public bool roomSwitchCounts = false;

    [Header("참조 (비워두면 자동으로 찾음)")]
    public Yeonho yeonho;
    public AudioSystem audioSystem;
    public VentilationSystem vent;
    public ViewController viewController;
    public MonitorDisplay monitor;

    [Header("상태 (확인용)")]
    public bool isAggressive = false;     // 밤 시작엔 항상 OFF
    public string reason = "";
    public float furyLeft = 0f;           // 원인 없어진 뒤 남은 유지 시간
    public float idleTime = 0f;

    private float rollTimer;
    private int lastZone = -1;
    private int lastRoom = -1;

    public bool IsAggressive => isAggressive;

    void Start()
    {
        if (yeonho == null) yeonho = FindAnyObjectByType<Yeonho>();
        if (audioSystem == null) audioSystem = FindAnyObjectByType<AudioSystem>();
        if (vent == null) vent = FindAnyObjectByType<VentilationSystem>();
        if (viewController == null) viewController = FindAnyObjectByType<ViewController>();
        if (monitor == null) monitor = FindAnyObjectByType<MonitorDisplay>();

        rollTimer = rollInterval;
        if (viewController != null) lastZone = viewController.currentZone;
        if (monitor != null) lastRoom = monitor.currentRoom;
    }

    void Update()
    {
        UpdateIdle();

        // 원인이 있는 동안은 계속 흉포 (유지 시간을 꽉 채워둠)
        string cause = CheckConditions();
        if (cause != null) Fire(cause);

        // 확률 주사위
        rollTimer -= Time.deltaTime;
        if (rollTimer <= 0f)
        {
            rollTimer = rollInterval;
            if (cause == null && RollChance()) Fire("확률");
        }

        // 원인이 없으면 유지 시간 감소 → 다 되면 OFF
        if (isAggressive && cause == null)
        {
            furyLeft -= Time.deltaTime;
            if (furyLeft <= 0f)
            {
                isAggressive = false;
                reason = "";
                Debug.Log("연호 흉포 OFF");
            }
        }
    }

    // 흉포 켜기/연장 — 유지 시간을 처음부터 채움
    void Fire(string why)
    {
        if (!isAggressive) Debug.Log($"★ 연호 흉포 ON ({why})");
        isAggressive = true;
        reason = why;
        furyLeft = furyMinDuration;
    }

    // 지금 맞는 원인이 있으면 이유 문자열, 없으면 null
    string CheckConditions()
    {
        if (audioSystem != null && audioSystem.IsAudioBroken()) return "오디오 고장";
        if (vent != null && vent.IsVentBroken()) return "환기 오류";
        if (InFuryHours()) return $"흉포 시간대 {furyHourStart}~{furyHourEnd}시";
        if (idleTime >= idleLimit) return $"방치 {idleLimit:F0}초";
        // TODO: 정전(현우 3스택 배선 고장) 중이면 return "정전";
        return null;
    }

    bool InFuryHours()
    {
        if (furyHourStart == furyHourEnd) return false;          // 시간대 흉포 없음
        int h = GameManager.Hour;                                // 0~6 (12시 = 0, 밤 끝 = 6)
        return h >= furyHourStart && h < furyHourEnd;
    }

    bool RollChance()
    {
        int ai = (yeonho != null) ? yeonho.AI : 0;
        return Random.Range(0, randomDie) < ai;
    }

    // 방치 시간: 시야 구역이 바뀌면 0으로 (옵션: CCTV 방 전환도)
    void UpdateIdle()
    {
        bool acted = false;

        if (viewController != null)
        {
            int zone = viewController.currentZone;
            if (zone != lastZone) { acted = true; lastZone = zone; }
        }

        if (monitor != null)
        {
            int room = monitor.currentRoom;
            if (room != lastRoom)
            {
                if (roomSwitchCounts) acted = true;
                lastRoom = room;
            }
        }

        idleTime = acted ? 0f : idleTime + Time.deltaTime;
    }
}

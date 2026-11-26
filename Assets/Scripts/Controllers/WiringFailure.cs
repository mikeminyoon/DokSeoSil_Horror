using UnityEngine;

// 배선 고장 (기획서가 "정전"이라 부르는 것) — 현우 3스택 (기획서 ① 전력·§4.4, 구현노트 §15.18)
//
// ⚠️ 배터리 방전(BatteryBlackout, 복구 불가 → 연호 확정 사망)과 완전히 별개. 이건 "고칠 수 있는 위기".
//
// [고장] 관리실 전기가 나감 — 밤당 1회
//   - 꺼짐: 관리실 조명 / 환풍구 불빛 / CCTV(모니터 검게 → 일반 종도 못 침) / 리셋 패널 / 시계
//   - 셔터: 열린 채 멈춤 (방전 때처럼)
//   - 살아있음: 비상 종 (유일한 수단), 시야 회전
//   - 귀신들은 계속 움직임 (기획서: 정지는 창고 통로에 들어간 순간부터)
//   - 연호 흉포 ON (YeonhoAggression이 IsActive를 봄)
// [컷오프] cutoffHour(5시) 이후엔 안 남 — 창고런이 5시 이후 없음 (유저 결정, 구현노트 §15.23). 현우는 대신 배터리를 깎음.
// [복구] 지금은 임시로 autoRepairTime초 뒤 자동 복구 → 조명도 정상으로 + 시계 분 고장도 수리 (유저 결정, §15.22).
//   ⭐ 창고런을 만들면: autoRepair 끄고, 전기실 브레이커 수리 완료에서 Repair()만 부르면 됨.
//
// 씬에 없으면 현우가 자동으로 만듦.
public class WiringFailure : MonoBehaviour
{
    public static WiringFailure Instance { get; private set; }

    [Header("임시 복구 (창고런 생기기 전까지)")]
    [Tooltip("켜면 autoRepairTime초 뒤 자동 복구. 창고런 만들면 끄고, 브레이커 수리 완료에서 Repair()를 부를 것")]
    public bool autoRepair = true;
    public float autoRepairTime = 20f;

    [Header("컷오프 (창고런 5시 컷오프와 맞춤)")]
    [Tooltip("이 시(GameManager.Hour) 이후엔 배선 고장이 안 남 — 5시 이후엔 창고런이 없으니까. 현우 3스택은 대신 배터리 -15")]
    public int cutoffHour = 5;

    [Header("상태 (확인용)")]
    public bool isActive = false;          // 배선 고장 중
    public bool happenedTonight = false;   // 오늘 밤 이미 났음 (밤당 1회)
    public float repairLeft = 0f;          // 자동 복구까지 남은 시간

    public bool IsActive => isActive;

    // 지금 배선 고장을 낼 수 있나 — 고장 중 / 오늘 이미 남 / 컷오프 시간 지남이면 false
    public bool CanBegin => !isActive && !happenedTonight && GameManager.Hour < cutoffHour;

    public static WiringFailure GetOrCreate()
    {
        if (Instance != null) return Instance;
        var found = FindAnyObjectByType<WiringFailure>();
        if (found != null) return found;
        return new GameObject("WiringFailure (자동 생성)").AddComponent<WiringFailure>();
    }

    void Awake()
    {
        Instance = this;
    }

    void Update()
    {
        if (!isActive || !autoRepair) return;

        repairLeft -= Time.deltaTime;
        if (repairLeft <= 0f) Repair();
    }

    // === 고장 (현우 3스택이 호출) ===
    public void Begin(string reason)
    {
        if (!CanBegin) return;
        isActive = true;
        happenedTonight = true;
        repairLeft = autoRepairTime;

        SetPower(false);
        Debug.Log($"★★★ 배선 고장! ({reason}) — 관리실 전기 나감. 비상 종만 작동, 연호 흉포."
                  + (autoRepair ? $" ({autoRepairTime:F0}초 뒤 임시 자동 복구)" : ""));
        // TODO: 정전 스팅 사운드 (기획서 G3, Phase 8)
        // TODO: 창고 통로 열림 (창고런 만들 때)
    }

    // === 복구 (지금은 자동, 나중엔 브레이커 수리 완료가 호출) ===
    public void Repair()
    {
        if (!isActive) return;
        isActive = false;

        // 그 사이 배터리가 방전됐으면 복구 안 함 (방전 암전이 이미 전부 꺼둠 — 다시 켜면 안 됨)
        if (BatteryManager.Instance != null && BatteryManager.Instance.isDead)
        {
            Debug.Log("배선 복구 시점에 이미 방전 — 복구 안 함");
            return;
        }

        SetPower(true);
        Debug.Log("★ 배선 복구 — 전기 들어옴 (조명 정상)");
    }

    // 관리실 전기 끄기/켜기
    void SetPower(bool on)
    {
        // CCTV: 보고 있었으면 내리고, 못 켜게
        var cctv = FindAnyObjectByType<CCTVController>();
        if (cctv != null)
        {
            if (!on) cctv.ForceCancel();
            cctv.inputLocked = !on;
        }

        // 리셋 패널: 리셋 중이어도 닫고, 못 열게
        var panel = FindAnyObjectByType<PanelController>();
        if (panel != null)
        {
            if (!on) panel.ForceCloseIgnoringReset();
            panel.inputLocked = !on;
        }

        // 셔터: 열린 채 멈춤 + 환풍구 불빛 꺼짐
        var shutter = FindAnyObjectByType<ShutterController>();
        if (shutter != null)
        {
            if (on) shutter.PowerOn();
            else shutter.PowerOff();
        }

        // 모니터·시계
        var monitor = FindAnyObjectByType<MonitorDisplay>();
        if (monitor != null)
        {
            if (on) monitor.PowerOn();
            else monitor.PowerOff();
        }
        foreach (var c in FindObjectsByType<Clock_Digital>())
        {
            if (on) { c.PowerOn(); c.UnfreezeMinute(); }   // 복구 = 현우 스택 피해(분 고장)도 같이 수리 (조명처럼)
            else c.PowerOff();
        }

        // 관리실 조명
        var lights = RoomLights.Instance;
        if (lights != null)
        {
            if (on) lights.RestoreAfterRepair();
            else lights.SetPowerCut(true);
        }

        // 비상 종은 건드리지 않음 — 배선 고장 중 유일한 수단 (유저 결정)
    }
}

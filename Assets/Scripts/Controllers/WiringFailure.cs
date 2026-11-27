using UnityEngine;

// 배선 고장 (기획서가 "정전"이라 부르는 것) — 현우 3스택 / 3일차 강제 정전 (기획서 ① 전력·§4.4, 구현노트 §15.18·§15.26)
//
// ⚠️ 배터리 방전(BatteryBlackout, 복구 불가 → 연호 확정 사망)과 완전히 별개. 이건 "고칠 수 있는 위기".
//
// [고장] 관리실 전기가 나감 — 밤당 1회
//   - 꺼짐: 관리실 조명 / 환풍구 불빛 / CCTV(모니터 검게 → 일반 종도 못 침) / 리셋 패널 / 시계
//   - 셔터: 열린 채 멈춤 (방전 때처럼)
//   - 살아있음: 비상 종 (유일한 수단), 시야 회전
//   - 귀신들은 계속 움직임 (기획서: 정지는 창고 통로에 들어간 순간부터)
//   - 연호 흉포 ON (YeonhoAggression이 IsActive를 봄)
// [복구] 창고런(StorageRun)에서 전기실 브레이커를 전부 고치면 Repair() — 그 전엔 안 고쳐짐.
//   (예전 "20초 자동 복구"는 창고런 만들기 전 임시방편이라 삭제 — 유저 결정 2026-11-27)
// [컷오프] cutoffHour(5시) 이후엔 안 남 — 창고런이 5시 이후 없음 (유저 결정, §15.23). 현우는 대신 배터리를 깎음.
// [강제 정전] forcedOutageByNight가 1인 밤(3일차)엔 정해둔 시간대(3:00~3:59) 안에 한 번 꼭 남 — 첫 창고런 학습 보장
//   (현우 3스택으로 이미 났으면 안 남. 시간은 그 시간대 안에서 랜덤)
//
// 씬에 없으면 현우·창고런이 자동으로 만듦.
public class WiringFailure : MonoBehaviour
{
    public static WiringFailure Instance { get; private set; }

    [Header("컷오프 (창고런 5시 컷오프와 맞춤)")]
    [Tooltip("이 시(GameManager.Hour) 이후엔 배선 고장이 안 남 — 5시 이후엔 창고런이 없으니까. 현우 3스택은 대신 배터리 -15")]
    public int cutoffHour = 5;

    [Header("강제 정전 (3일차 — 기획서 \"~3:30 고정 정전\")")]
    [Tooltip("밤별: 1이면 그 밤엔 아래 시간대 안에 정전이 한 번 꼭 남. 인덱스 = 밤 숫자 ([0]은 안 쓰는 칸)")]
    public int[] forcedOutageByNight = { 0, 0, 0, 1, 0, 0 };
    [Tooltip("이 시부터 (GameManager.Hour 기준, 12시=0)")]
    public int forcedHourStart = 3;
    [Tooltip("이 시 전까지 (3이면 3:00~3:59)")]
    public int forcedHourEnd = 4;

    [Header("상태 (확인용)")]
    public bool isActive = false;          // 배선 고장 중
    public bool happenedTonight = false;   // 오늘 밤 이미 났음 (밤당 1회)
    public string forcedAtText = "";       // 오늘 강제 정전 예정 시각 (없으면 빈칸)

    public bool IsActive => isActive;

    // 지금 배선 고장을 낼 수 있나 — 고장 중 / 오늘 이미 남 / 컷오프 시간 지남이면 false
    public bool CanBegin => !isActive && !happenedTonight && GameManager.Hour < cutoffHour;

    private int forcedAtMinutes = -1;      // 강제 정전 시각 (12시부터 몇 분째), -1 = 오늘은 없음

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

    void Start()
    {
        // 오늘 밤 강제 정전 시각을 미리 정해둠 (시간대 안에서 랜덤)
        if (GameManager.ByNight(forcedOutageByNight) > 0 && forcedHourEnd > forcedHourStart)
        {
            forcedAtMinutes = Random.Range(forcedHourStart * 60, forcedHourEnd * 60);
            int h = forcedAtMinutes / 60;
            forcedAtText = $"{(h == 0 ? 12 : h)}:{forcedAtMinutes % 60:00}";
        }
    }

    void Update()
    {
        // 강제 정전: 정한 시각이 됐는데 오늘 아직 정전이 없었으면 (시간대 안에서만)
        if (forcedAtMinutes < 0 || happenedTonight || isActive) return;
        if (GameManager.Instance == null || GameManager.Instance.isNightOver || GameManager.Instance.isGameOver) return;
        if (BatteryManager.Instance != null && BatteryManager.Instance.isDead) return;

        int hour = GameManager.Hour;
        if (hour >= forcedHourEnd) { forcedAtMinutes = -1; return; }   // 시간대 지남 (그 사이 못 냈으면 오늘은 없음)
        int now = hour * 60 + GameManager.Instance.gameMinute;
        if (now >= forcedAtMinutes)
            Begin($"{GameManager.Night}일차 강제 정전 ({forcedAtText})");
    }

    // === 고장 (현우 3스택 / 강제 정전이 호출) ===
    public void Begin(string reason)
    {
        if (!CanBegin) return;
        isActive = true;
        happenedTonight = true;

        SetPower(false);
        Debug.Log($"★★★ 배선 고장! ({reason}) — 관리실 전기 나감. 비상 종만 작동, 연호 흉포. 뒤쪽 창고 통로 열림");
        // TODO: 정전 스팅 사운드 (기획서 G3, Phase 8)
    }

    // 테스트용: 오늘 이미 났어도 다시 냄 (StorageRun의 우클릭 메뉴 "테스트: 지금 정전 내기"가 부름)
    public void BeginForTest()
    {
        if (isActive) return;
        happenedTonight = false;
        if (GameManager.Hour >= cutoffHour) { Debug.Log("테스트 정전: 5시 이후라 안 남 (컷오프)"); return; }
        Begin("테스트");
    }

    // === 복구 (창고런에서 브레이커 전부 수리하면 StorageRun이 부름) ===
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

using UnityEngine;

// 관리실 조명 — 현우 스택 후폭풍 (기획서 §4.4, 구현노트 §12.2·§15.18)
//
// [단계] 현우가 SetStage()로 바꿈
//   0 정상: 켜져 있음
//   1 깜빡: 가끔(8~20초마다) 지직 깜빡  (기획서의 "잠깐 시야 흐림"은 유저가 뺌 — 환기 오류 흐려짐이랑 겹쳐서)
//   2 나감: 완전히 꺼짐
// [배선 고장] WiringFailure가 SetPowerCut(true) → 단계 상관없이 꺼짐. 복구(RestoreAfterRepair) → 정상(0단계)으로.
// [방전 암전] BatteryBlackout가 PowerOff() → 영구히 꺼짐.
//
// ⚠️ 조명은 켜고 끄기(enabled)만 함 — 밝기·범위·색·위치는 절대 안 건드림. Phase 8에서 마음대로 조절해도 됨.
// ⚠️ 라이트 Mode는 Realtime(또는 Mixed)이어야 꺼짐. Baked면 라이트맵에 구워져서 안 꺼짐.
// ⚠️ 환풍구 불빛(셔터 쪽)은 여기서 안 다룸 — 배선 고장 때만 ShutterController.PowerOff로 꺼짐 (유저 결정).
//
// 씬에 없으면 현우가 자동으로 만듦. 직접 조명을 연결하려면 아무 오브젝트(예: SecurityRoom/Lights)에 붙이고 Lights 칸에 넣을 것.
public class RoomLights : MonoBehaviour
{
    public static RoomLights Instance { get; private set; }

    [Header("관리실 조명 (비워두면 임시 천장등 자동 생성)")]
    [Tooltip("켜고 끄기만 함 — 밝기·범위·색은 안 건드림. Mode는 Realtime/Mixed여야 꺼짐. (경고등 Siren은 넣지 말 것)")]
    public Light[] lights;

    [Header("1스택: 가끔 깜빡")]
    public float flickerGapMin = 8f;       // 깜빡임 사이 간격 (랜덤)
    public float flickerGapMax = 20f;
    public float flickerBurst = 0.6f;      // 한 번 깜빡일 때 지직거리는 시간
    public float flickerToggleMin = 0.04f; // 지직거릴 때 켜짐/꺼짐 바뀌는 간격 (랜덤)
    public float flickerToggleMax = 0.12f;

    [Header("상태 (확인용)")]
    public int stage = 0;                  // 0 정상 / 1 깜빡 / 2 나감
    public bool powerCut = false;          // 배선 고장 중

    private bool poweredOff = false;       // 방전 암전 (영구)
    private bool lightsOn = false;         // 지금 조명 상태 (바뀔 때만 enabled 건드림 — Start에서 한 번 켬)

    // 깜빡
    private float gapTimer;
    private float burstLeft = 0f;
    private float toggleTimer = 0f;
    private bool burstLit = true;

    public static RoomLights GetOrCreate()
    {
        if (Instance != null) return Instance;
        var found = FindAnyObjectByType<RoomLights>();
        if (found != null) return found;
        return new GameObject("RoomLights (자동 생성)").AddComponent<RoomLights>();
    }

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        if (lights == null || lights.Length == 0)
            lights = new Light[] { CreatePlaceholderLight() };

        gapTimer = Random.Range(flickerGapMin, flickerGapMax);
        ApplyLights(true);
    }

    void Update()
    {
        if (poweredOff) return;   // 방전 암전 — 꺼진 채 그대로

        ApplyLights(DesiredOn());
    }

    // === 외부에서 부르는 것 ===

    // 현우 스택 → 단계 (0 정상 / 1 깜빡 / 2 나감)
    public void SetStage(int newStage)
    {
        stage = Mathf.Clamp(newStage, 0, 2);
        burstLeft = 0f;
        gapTimer = Random.Range(flickerGapMin, flickerGapMax);
        Debug.Log(stage == 1 ? "★ 관리실 조명 깜빡이기 시작 (현우 1스택)"
                : stage == 2 ? "★★ 관리실 조명 나감 (현우 2스택)"
                : "관리실 조명 정상");
    }

    // 배선 고장: 단계 상관없이 꺼짐
    public void SetPowerCut(bool cut)
    {
        powerCut = cut;
    }

    // 배선 복구: 조명 정상으로 (유저 결정 — 브레이커 고치면 배선 피해도 같이 복구)
    public void RestoreAfterRepair()
    {
        powerCut = false;
        SetStage(0);
    }

    // 방전 암전: 영구히 꺼짐
    public void PowerOff()
    {
        poweredOff = true;
        ApplyLights(false);
    }

    // === 조명 ===

    bool DesiredOn()
    {
        if (powerCut) return false;
        if (stage >= 2) return false;
        if (stage == 1) return UpdateFlicker();
        return true;
    }

    // 1스택 깜빡: 평소엔 켜져 있다가 가끔 짧게 지직
    bool UpdateFlicker()
    {
        float dt = Time.deltaTime;

        if (burstLeft > 0f)
        {
            burstLeft -= dt;
            toggleTimer -= dt;
            if (toggleTimer <= 0f)
            {
                toggleTimer = Random.Range(flickerToggleMin, flickerToggleMax);
                burstLit = !burstLit;
            }
            if (burstLeft <= 0f)
            {
                gapTimer = Random.Range(flickerGapMin, flickerGapMax);
                return true;   // 지직 끝 → 다시 켜짐
            }
            return burstLit;
        }

        gapTimer -= dt;
        if (gapTimer <= 0f)
        {
            burstLeft = flickerBurst;
            toggleTimer = Random.Range(flickerToggleMin, flickerToggleMax);
            burstLit = false;  // 꺼지면서 시작
            return false;
        }
        return true;
    }

    // 바뀔 때만 enabled 건드림 (밝기·범위·색은 안 건드림)
    void ApplyLights(bool on)
    {
        if (on == lightsOn) return;
        lightsOn = on;
        if (lights == null) return;
        foreach (var l in lights)
            if (l != null) l.enabled = on;
    }

    // === 임시 천장등 (조명 연결 안 했을 때) ===
    Light CreatePlaceholderLight()
    {
        var go = new GameObject("RoomLight (임시 자동 생성)");
        var cctv = FindAnyObjectByType<CCTVController>();
        Camera cam = (cctv != null) ? cctv.GetComponentInChildren<Camera>() : null;   // 플레이어 카메라 (CCTV Start 순서와 무관하게)
        if (cam == null) cam = Camera.main;
        go.transform.position = (cam != null) ? cam.transform.position + Vector3.up * 1f : transform.position;

        var light = go.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = new Color(1f, 0.93f, 0.8f);   // 형광등 느낌 살짝 따뜻한 흰색
        light.intensity = 3f;
        light.range = 6f;
        light.shadows = LightShadows.None;
        Debug.Log("관리실 조명(RoomLights.lights)이 비어 있어서 임시 천장등을 만들었어요 — 직접 세운 조명을 연결하면 그걸 씀");
        return light;
    }
}

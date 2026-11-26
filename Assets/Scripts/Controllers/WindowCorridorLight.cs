using UnityEngine;

// 창문앞 복도 조명 (구현노트 §15.25) — 연호가 창문 쪽(창문앞·빼꼼)에 있으면 깜빡깜빡 (유저 요청 2026-11-27)
//   평소: 켜져 있음(normallyOn) / 연호가 창문 쪽: 불규칙하게 깜빡 — 꺼진 순간엔 연호 실루엣도 어둠에 묻힘
//   CCTV 보는 중엔 창문이 안 보이니 숨소리(YeonhoSounds)만 / 내려서 보면 깜빡임 + 실루엣 (육안 확인)
// 조명 고르는 순서: ① 연호 Inspector의 "Corridor Lights" 칸 (직접 만든 Light를 거기에 — 2026-11-27 유저가 여기 넣으려다 안 들어가서 바꿈)
//                  ② 이 컴포넌트의 lights 칸 ③ 이 컴포넌트를 붙인 오브젝트의 Light ④ 다 없으면 임시 조명을
//                  창문앞 연호 뒤쪽(복도 쪽)에 자동 생성 — 역광이라 연호가 검은 모양으로 보임.
// ⚠️ 켜고 끄기(enabled)만 함 — 밝기·범위·색은 안 건드림 (RoomLights와 같은 원칙). 라이트 Mode는 Realtime/Mixed.
// ⚠️ 관리실 조명(RoomLights)·환풍구 불빛·Siren과 별개. 배선 고장·방전 암전과는 아직 안 엮음 (방전 연출은 Phase 8에서 다시).
// 씬에 없으면 연호가 자동으로 만듦 (그래서 보통은 씬에 안 붙여도 됨).
public class WindowCorridorLight : MonoBehaviour
{
    public static WindowCorridorLight Instance { get; private set; }

    [Header("창문앞 복도 조명 (비우면 임시 조명 자동 생성)")]
    [Tooltip("켜고 끄기만 함. Mode는 Realtime/Mixed여야 꺼짐")]
    public Light[] lights;
    [Tooltip("연호가 없을 때 켜둘지 (꺼두면 연호가 올 때만 번쩍번쩍)")]
    public bool normallyOn = true;

    [Header("깜빡 (연호가 창문 쪽에 있을 때)")]
    public float onMin = 0.04f;           // 켜져 있는 시간 (랜덤)
    public float onMax = 0.25f;
    public float offMin = 0.05f;          // 꺼져 있는 시간 (랜덤)
    public float offMax = 0.45f;
    [Range(0f, 1f)] public float longOffChance = 0.15f;   // 가끔 길게 꺼짐
    public float longOffMin = 0.6f;
    public float longOffMax = 1.3f;

    [Header("상태 (확인용)")]
    public bool flickering = false;

    [HideInInspector] public Transform anchor;   // 임시 조명 놓을 기준 (창문앞 연호 자리) — 연호가 넣어줌

    private bool lit = true;
    private float toggleTimer = 0f;
    private bool lightsOn = false;
    private bool applied = false;
    private GameObject placeholder;   // 임시 조명 (직접 만든 조명이 들어오면 지움)
    private bool started = false;

    public static WindowCorridorLight GetOrCreate(Transform windowFront)
    {
        if (Instance != null) return Instance;
        var found = FindAnyObjectByType<WindowCorridorLight>();
        if (found != null) return found;
        var created = new GameObject("WindowCorridorLight (자동 생성)").AddComponent<WindowCorridorLight>();
        created.anchor = windowFront;
        return created;
    }

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        started = true;
        if (!HasAny(lights))
        {
            var own = GetComponentsInChildren<Light>();   // ③ 이 컴포넌트를 조명 오브젝트에 붙였으면 그 Light
            lights = (own.Length > 0) ? own : new Light[] { CreatePlaceholderLight() };
        }
        Apply(normallyOn);
    }

    // 연호 Inspector "Corridor Lights" 칸의 조명을 씀 (비었으면 아무것도 안 함 → 원래대로)
    public void UseLights(Light[] newLights)
    {
        if (!HasAny(newLights)) return;
        if (placeholder != null) Destroy(placeholder);   // 임시 조명이 이미 생겼으면 지움
        placeholder = null;
        lights = newLights;
        applied = false;                                 // 다음 Apply에서 새 조명에 상태 적용
        if (started) Apply(flickering ? lit : normallyOn);
    }

    static bool HasAny(Light[] arr)
    {
        if (arr == null) return false;
        foreach (var l in arr) if (l != null) return true;
        return false;
    }

    void Update()
    {
        if (!flickering)
        {
            Apply(normallyOn);
            return;
        }

        toggleTimer -= Time.deltaTime;
        if (toggleTimer <= 0f)
        {
            lit = !lit;
            if (lit) toggleTimer = Random.Range(onMin, onMax);
            else toggleTimer = (Random.value < longOffChance) ? Random.Range(longOffMin, longOffMax)
                                                                : Random.Range(offMin, offMax);
        }
        Apply(lit);
    }

    // 연호가 창문 쪽에 오면 true, 떠나면 false (매 프레임 불러도 됨 — 바뀔 때만 처리)
    public void SetFlicker(bool on)
    {
        if (flickering == on) return;
        flickering = on;
        if (on)
        {
            lit = false;                                   // 꺼지면서 시작
            toggleTimer = Random.Range(offMin, offMax);
        }
    }

    // 바뀔 때만 enabled 건드림
    void Apply(bool on)
    {
        if (applied && on == lightsOn) return;
        applied = true;
        lightsOn = on;
        if (lights == null) return;
        foreach (var l in lights)
            if (l != null) l.enabled = on;
    }

    // === 임시 조명 (조명 연결 안 했을 때) — 창문앞 연호 뒤 복도 쪽, 살짝 위 (역광) ===
    Light CreatePlaceholderLight()
    {
        var go = new GameObject("WindowCorridorLight (임시 조명)");
        placeholder = go;
        Vector3 pos = transform.position;
        if (anchor != null)
        {
            // 플레이어 → 연호 방향으로 조금 더 (연호 뒤 = 복도 안쪽). 너무 멀면 복도 벽 뒤로 넘어가서 0.6만
            var cctv = FindAnyObjectByType<CCTVController>();
            Camera cam = (cctv != null) ? cctv.GetComponentInChildren<Camera>() : null;
            if (cam == null) cam = Camera.main;
            Vector3 back = (cam != null) ? anchor.position - cam.transform.position : anchor.forward;
            back.y = 0f;
            back = (back.sqrMagnitude > 0.001f) ? back.normalized : Vector3.forward;
            pos = anchor.position + back * 0.6f + Vector3.up * 1.3f;
        }
        go.transform.position = pos;

        var light = go.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = new Color(0.85f, 0.92f, 1f);   // 차가운 형광등 느낌
        light.intensity = 2.5f;
        light.range = 6f;
        light.shadows = LightShadows.None;
        Debug.Log("창문앞 복도 조명이 비어 있어서 임시 조명을 만들었어요 — 직접 만든 조명은 연호 Inspector의 Corridor Lights 칸에 넣으면 그걸 씀");
        return light;
    }
}

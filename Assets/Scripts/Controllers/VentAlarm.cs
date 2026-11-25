using UnityEngine;

// 환기 오류 경보 — 빨간 경고등 깜빡 + 사이렌 (원작 FNAF3처럼 환기 오류에만, 구현노트 §15.17)
// 오디오·카메라 고장엔 경보 없음 (패널 error 표시만).
//
// [켜짐] 환기 오류 중 (리셋 진행 중에도 계속 — 리셋이 끝나야 꺼짐)
// [꺼짐] 정상 / 배터리 방전 암전 (전기가 없으니까)
//
// VentilationSystem이 시작할 때 없으면 자동으로 붙임. 조절하려면 VentilationSystem 오브젝트에 미리 붙여둘 것.
public class VentAlarm : MonoBehaviour
{
    [Header("참조 (비워두면 자동)")]
    public VentilationSystem vent;
    [Tooltip("비워두면 관리실(플레이어 카메라 위)에 빨간 Point Light를 자동으로 만듦")]
    public Light alarmLight;

    [Header("경고등")]
    public float flashPeriod = 0.6f;              // 켜짐+꺼짐 한 번 도는 시간
    public float lightIntensity = 4f;
    public Color lightColor = new Color(1f, 0.08f, 0.05f);
    public float lightRange = 8f;

    [Header("사이렌")]
    [Tooltip("비워두면 임시 사이렌 소리를 코드로 만들어 틈. 진짜 소리 파일이 생기면 여기에 넣기")]
    public AudioClip sirenClip;
    [Range(0f, 1f)] public float sirenVolume = 0.35f;

    private AudioSource audioSource;
    private bool alarmOn = false;

    void Start()
    {
        if (vent == null) vent = GetComponent<VentilationSystem>();
        if (vent == null) vent = FindAnyObjectByType<VentilationSystem>();
        if (alarmLight == null) alarmLight = CreateAlarmLight();
        alarmLight.enabled = false;

        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.clip = (sirenClip != null) ? sirenClip : CreateSirenClip();
        audioSource.loop = true;
        audioSource.playOnAwake = false;
        audioSource.volume = sirenVolume;
        audioSource.spatialBlend = 0f;   // 임시: 2D (Phase 8에서 관리실 스피커 위치 3D 소리로)
    }

    void Update()
    {
        bool powerDead = BatteryManager.Instance != null && BatteryManager.Instance.isDead;
        bool shouldAlarm = vent != null && vent.IsVentBroken() && !powerDead;

        if (shouldAlarm != alarmOn)
        {
            alarmOn = shouldAlarm;
            if (alarmOn)
            {
                audioSource.Play();
                Debug.Log("★ 환기 오류 경보 ON (경고등 + 사이렌)");
            }
            else
            {
                audioSource.Stop();
                alarmLight.enabled = false;
                Debug.Log("환기 경보 OFF");
            }
        }

        // 경고등 깜빡
        if (alarmOn)
            alarmLight.enabled = Mathf.Repeat(Time.time, flashPeriod) < flashPeriod * 0.5f;
    }

    // 경고등 자동 생성: 플레이어 카메라 조금 위 (관리실 천장 쯤)
    Light CreateAlarmLight()
    {
        var go = new GameObject("VentAlarmLight (자동 생성)");
        var cctv = FindAnyObjectByType<CCTVController>();
        Camera cam = (cctv != null) ? cctv.PlayerCamera : null;
        if (cam == null) cam = Camera.main;
        go.transform.position = (cam != null) ? cam.transform.position + Vector3.up * 0.6f : transform.position;

        var light = go.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = lightColor;
        light.intensity = lightIntensity;
        light.range = lightRange;
        light.shadows = LightShadows.None;
        return light;
    }

    // 임시 사이렌: 높낮이가 오르내리는 "위이-우우" (2초 한 바퀴, 반복 재생)
    AudioClip CreateSirenClip()
    {
        const int sampleRate = 44100;
        const float length = 2f;          // 한 바퀴
        const float lowHz = 550f, highHz = 950f;
        int count = (int)(sampleRate * length);
        float[] data = new float[count];

        float phase = 0f;
        for (int i = 0; i < count; i++)
        {
            float t = i / (float)sampleRate;
            // 0→1→0 삼각파로 주파수를 오르내림
            float sweep = 1f - Mathf.Abs(2f * (t / length) - 1f);
            float freq = Mathf.Lerp(lowHz, highHz, sweep);
            phase += 2f * Mathf.PI * freq / sampleRate;
            // 사인 + 약한 3배음 (조금 거친 경보 느낌)
            float s = Mathf.Sin(phase) + 0.3f * Mathf.Sin(3f * phase);
            data[i] = s * 0.3f;
        }

        var clip = AudioClip.Create("VentSiren_임시", count, 1, sampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }
}

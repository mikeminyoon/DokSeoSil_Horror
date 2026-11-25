using UnityEngine;

// 배터리 방전 암전 시퀀스 — FNAF1식. 기획서 ① 전력, 구현노트 §15.11
//
// ⚠️ "방전"(배터리 0, 복구 불가 → 연호 확정 사망) 전용.
//    현우 3스택 "배선 고장"(기획서에서 '정전'이라 부르는 것, 창고런으로 수리 가능)과는 완전히 별개 — 여기서 다루지 않음.
//
// [흐름]
//  ① 암전: 전부 꺼짐. 시야 회전만 가능. 다른 귀신·시스템 전부 정지. (조용한 시간 10~24초 랜덤)
//  ② 오르골: 창문앞에 연호가 나타나고, 오르골 소리 + 화면 깜빡임. (고정 시간)
//  ③ 완전 어둠: 소리 멈춤, 새까맘. (고정 시간)
//  ④ 연호 점프스케어 → 게임오버.
//  * 그 사이 6시가 되면 생존. (밤 타이머는 GameManager에서 계속 흐름 — 시계만 꺼져서 안 보임)
//
// BatteryManager가 방전 시 Begin() 호출. 씬에 없으면 BatteryManager가 자동으로 붙임.
public class BatteryBlackout : MonoBehaviour
{
    [Header("시간 (초)")]
    public float quietMin = 10f;        // ① 조용한 암전 — 랜덤 최소
    public float quietMax = 24f;        //                 — 랜덤 최대
    public float musicDuration = 8f;    // ② 오르골 — 매번 같음
    public float darkDuration = 3f;     // ③ 완전 어둠 — 매번 같음

    [Header("화면")]
    [Range(0f, 1f)] public float blackoutAlpha = 0.93f;     // 암전 중 어둡기 (1 = 완전 검정, 살짝 남겨서 방 윤곽만 보이게)
    [Range(0f, 1f)] public float flickerBrightAlpha = 0.6f; // 오르골 깜빡일 때 밝아지는 정도 (낮을수록 더 밝게 번쩍)
    public float flickerMinGap = 0.05f;   // 깜빡임 간격 랜덤 범위
    public float flickerMaxGap = 0.25f;

    [Header("오르골 소리")]
    [Tooltip("비워두면 임시 오르골 멜로디를 코드로 만들어서 틀어줌. 진짜 오르골 소리 파일이 생기면 여기에 넣기")]
    public AudioClip musicBoxClip;
    [Range(0f, 1f)] public float musicVolume = 0.6f;

    [Header("상태 (확인용)")]
    public bool isOut = false;

    private enum Phase { None, Quiet, Music, Dark, Done }
    private Phase phase = Phase.None;
    private float timer = 0f;
    private float flickerTimer = 0f;

    private CanvasGroup overlay;
    private AudioSource audioSource;
    private Yeonho yeonho;

    // === 시작 (BatteryManager가 방전 순간 호출) ===
    public void Begin()
    {
        if (isOut) return;
        isOut = true;

        Debug.Log("★★★ 배터리 방전 — 암전! (6시까지 버티는 것 말고는 할 수 있는 게 없음)");

        if (overlay == null) overlay = ScreenOverlayFactory.CreateBlack("BatteryBlackoutOverlay (자동 생성)", 110);
        overlay.alpha = blackoutAlpha;

        ShutEverythingDown();

        phase = Phase.Quiet;
        timer = Random.Range(quietMin, quietMax);
    }

    // 전부 끄기 — 시야 회전(ViewController)과 밤 타이머(GameManager)만 살아있음
    void ShutEverythingDown()
    {
        // 화면 전환 감지 먼저 끔 → 아래에서 CCTV/패널 강제로 닫아도 귀신이 반응 안 함
        var transition = FindAnyObjectByType<ScreenTransitionDetector>();
        if (transition != null) transition.enabled = false;

        // 귀신 전부 정지 (그 자리에 멈춤)
        foreach (var g in FindObjectsByType<Hyunsoong>()) g.enabled = false;
        foreach (var g in FindObjectsByType<Hyunwoo>()) g.enabled = false;
        foreach (var g in FindObjectsByType<KimWook>()) g.enabled = false;   // 새 귀신 추가하면 여기에도 추가할 것
        foreach (var g in FindObjectsByType<JoonYoung>()) g.enabled = false;
        yeonho = FindAnyObjectByType<Yeonho>();
        if (yeonho != null) yeonho.enabled = false;

        // 고장 시스템 정지 (오디오 방치 즉사 타이머·환기 흐려짐도 멈춤)
        var audioSys = FindAnyObjectByType<AudioSystem>();
        if (audioSys != null) audioSys.enabled = false;
        var camSys = FindAnyObjectByType<CameraSystem>();
        if (camSys != null) camSys.enabled = false;
        var vent = FindAnyObjectByType<VentilationSystem>();
        if (vent != null)
        {
            vent.enabled = false;
            if (vent.blurOverlay != null) vent.blurOverlay.alpha = 0f;   // 흐려짐 도중이었으면 지움
        }

        // CCTV: 보고 있었으면 내리고, 다시 못 켜게
        var cctv = FindAnyObjectByType<CCTVController>();
        if (cctv != null)
        {
            cctv.ForceCancel();
            cctv.inputLocked = true;
        }

        // 패널: 리셋 중이어도 강제로 닫고, 다시 못 열게
        var panel = FindAnyObjectByType<PanelController>();
        if (panel != null)
        {
            panel.ForceCloseIgnoringReset();
            panel.inputLocked = true;
        }

        // 셔터 열림 + 불빛 끔 + 잠금
        var shutter = FindAnyObjectByType<ShutterController>();
        if (shutter != null) shutter.PowerOff();

        // 비상 종 잠금
        var bell = FindAnyObjectByType<EmergencyBell>();
        if (bell != null) bell.inputLocked = true;

        // 모니터·시계 꺼짐
        var monitor = FindAnyObjectByType<MonitorDisplay>();
        if (monitor != null) monitor.PowerOff();
        foreach (var c in FindObjectsByType<Clock_Digital>()) c.PowerOff();
    }

    void Update()
    {
        if (!isOut || phase == Phase.Done) return;

        // 6시가 먼저 오면 생존
        if (GameManager.Instance != null && GameManager.Instance.isNightOver)
        {
            Survive();
            return;
        }

        timer -= Time.deltaTime;

        switch (phase)
        {
            case Phase.Quiet:
                if (timer <= 0f) StartMusic();
                break;

            case Phase.Music:
                UpdateFlicker();
                if (timer <= 0f) StartDark();
                break;

            case Phase.Dark:
                if (timer <= 0f) Jumpscare();
                break;
        }
    }

    // === ② 오르골 ===
    void StartMusic()
    {
        phase = Phase.Music;
        timer = musicDuration;
        flickerTimer = 0f;

        if (yeonho != null) yeonho.AppearAtWindowForBlackout();

        if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.clip = (musicBoxClip != null) ? musicBoxClip : CreateMusicBoxClip();
        audioSource.loop = true;
        audioSource.volume = musicVolume;
        audioSource.spatialBlend = 0f;   // 임시: 2D. (Phase 8에서 창문 쪽 3D 소리로)
        audioSource.Play();

        Debug.Log("★★★ 방전 암전 — 오르골... (창문앞 연호)");
    }

    // 오르골 동안 화면 깜빡 (임시: 어둠이 순간 옅어짐 → 창문 보고 있으면 연호가 번쩍 보임)
    void UpdateFlicker()
    {
        flickerTimer -= Time.deltaTime;
        if (flickerTimer > 0f) return;

        flickerTimer = Random.Range(flickerMinGap, flickerMaxGap);
        bool bright = Random.value < 0.5f;
        overlay.alpha = bright ? flickerBrightAlpha : blackoutAlpha;
    }

    // === ③ 완전 어둠 ===
    void StartDark()
    {
        phase = Phase.Dark;
        timer = darkDuration;
        if (audioSource != null) audioSource.Stop();
        overlay.alpha = 1f;
        Debug.Log("★★★ 방전 암전 — 오르골 멈춤. 완전한 어둠...");
    }

    // === ④ 점프스케어 ===
    void Jumpscare()
    {
        phase = Phase.Done;
        overlay.alpha = 0f;   // 점프스케어 화면이 덮개 밑에 깔리지 않게 치움

        Debug.Log("☠☠☠ 방전 암전 — 연호 강림! 게임오버");
        var jumpscare = FindAnyObjectByType<JumpscareOverlay>();
        if (jumpscare != null) jumpscare.PlayGameOver();
        if (GameManager.Instance != null) GameManager.Instance.GameOver("배터리 방전 — 연호");
    }

    // === 6시 생존 ===
    void Survive()
    {
        phase = Phase.Done;
        if (audioSource != null) audioSource.Stop();
        Debug.Log("★★★ 6시! 방전 암전 속에서 살아남음");
        // TODO: 클리어 연출 (GameManager 클리어 화면 만들 때)
    }

    // 임시 오르골 멜로디 (진짜 소리 파일 없을 때) — 맑은 '팅' 소리가 잦아드는 음 여러 개
    AudioClip CreateMusicBoxClip()
    {
        const int sampleRate = 44100;
        const float noteLength = 0.34f;
        // 음 높이 (MIDI 번호, 69 = 라). 단조풍 자장가 느낌의 짧은 반복구
        int[] notes = { 81, 76, 72, 76, 81, 83, 84, 83, 81, 76, 77, 76, 74, 72, 71, 72 };

        int perNote = (int)(sampleRate * noteLength);
        float[] data = new float[perNote * notes.Length];

        for (int n = 0; n < notes.Length; n++)
        {
            float freq = 440f * Mathf.Pow(2f, (notes[n] - 69) / 12f);
            for (int i = 0; i < perNote; i++)
            {
                float t = i / (float)sampleRate;
                float env = Mathf.Exp(-t * 7f);   // 튕기고 잦아드는 소리
                float s = Mathf.Sin(2f * Mathf.PI * freq * t)
                        + 0.35f * Mathf.Sin(2f * Mathf.PI * freq * 2f * t)   // 맑은 배음
                        + 0.15f * Mathf.Sin(2f * Mathf.PI * freq * 3f * t);
                data[n * perNote + i] = s * env * 0.25f;
            }
        }

        var clip = AudioClip.Create("MusicBox_임시", data.Length, 1, sampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }
}

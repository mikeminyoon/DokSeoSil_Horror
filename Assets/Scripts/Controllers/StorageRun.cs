using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

// 창고런 (구현노트 §15.26~27) — 배선 고장 → 뒤쪽 통로 → 전기실 브레이커 → 복귀
//   [통로]    배선 고장 중 + 후면(뒤돌아본 쪽)을 볼 때만 "들어가기" 버튼이 보임
//   [들어가기] 페이드 → 관리실 전부 멈춤(귀신·고장 타이머·배터리·밤 시계·조작) → 카메라를 전기실로 → BreakerRoom 시작
//   [나가기]  브레이커 전부 수리 → 나가기 → 페이드 → 카메라 원래 자리 → 배선 복구 → 다시 움직임 → 밤 시계 30초 건너뜀
//
// ⚠️ 멈춤 = 컴포넌트 끄기(enabled=false). 들어갈 때 "켜져 있던 것만" 기억했다가 나올 때 그것만 다시 켬
//    (다른 이유로 꺼져 있던 건 안 건드림). 코루틴·Invoke 쓰는 귀신이 없어서 끄면 깔끔하게 멈춤 (11-27 확인).
// ⚠️ 잔상(점프스케어) 중엔 못 들어감 — 잔상이 끝날 때 조작 컴포넌트를 다시 켜버려서 전기실 카메라가 엉킴.
// [전기실 어둠] 전기실에 있는 동안만 바깥 빛(방향광·환경광·환경 반사)을 끔 → 스파크 때만 보이게 (유저 11-27:
//    "철 재질 벽에 Directional Light가 반사돼서 보임"). 맵 전체 조명이라 아예 지우면 관리실·CCTV도 어두워져서 이렇게.
//    관리실은 그동안 안 보이니 영향 없음. 나오면 원래대로.
// 윤진은 삭제(§15.23)라 창고는 지나가는 연출(페이드)만.
// 씬에 직접 붙여야 함 (통로 버튼·전기실 칸 연결 때문에). 버튼 OnClick은 비워둘 것 — 코드가 연결함.
public class StorageRun : MonoBehaviour
{
    [Header("통로 (관리실 뒤쪽)")]
    [Tooltip("통로 버튼 묶음의 Canvas Group — 배선 고장 중 + 뒤돌아봤을 때만 보임")]
    public CanvasGroup passageGroup;
    [Tooltip("들어가기 버튼 (OnClick은 비워두기 — 코드가 연결)")]
    public Button enterButton;
    [Tooltip("후면 구역 번호 (ViewController: 0 왼쪽 / 1 가운데 / 2 환풍구 / 3 후면)")]
    public int rearZone = 3;

    [Header("전기실")]
    public BreakerRoom breakerRoom;

    [Header("연출")]
    public float fadeTime = 0.6f;
    [Tooltip("복귀 때 밤 시계 건너뛰기(초) — 기획서: 창고런 동안 시계 정지 → 복귀 시 30초 점프")]
    public float returnTimeSkip = 30f;

    [Header("전기실 어둠")]
    [Tooltip("전기실에 있는 동안만 바깥 빛을 끔: 방향광(Directional Light) + 환경광 + 환경 반사. 나오면 원래대로")]
    public bool darkenOutsideLight = true;

    [Header("상태 (확인용)")]
    public bool inRun = false;

    private WiringFailure wiring;
    private ViewController view;
    private CCTVController cctv;
    private JumpscareOverlay jumpscare;

    private Transform camT;
    private Vector3 savedLocalPos;
    private Quaternion savedLocalRot;
    private readonly List<Behaviour> frozen = new List<Behaviour>();

    // 전기실 어둠 (들어갈 때 저장 → 나올 때 되돌림)
    private readonly List<Light> darkenedSuns = new List<Light>();
    private SphericalHarmonicsL2 savedAmbientProbe;
    private float savedReflectionIntensity;
    private bool darkened = false;

    // 페이드 (코드가 만든 검은 덮개)
    private CanvasGroup fade;
    private enum Phase { None, FadeOut, FadeIn }
    private Phase phase = Phase.None;
    private float phaseTimer = 0f;
    private System.Action afterFadeOut;

    public bool Busy => phase != Phase.None;

    void Start()
    {
        wiring = WiringFailure.GetOrCreate();
        view = FindAnyObjectByType<ViewController>();
        cctv = FindAnyObjectByType<CCTVController>();
        jumpscare = FindAnyObjectByType<JumpscareOverlay>();
        if (breakerRoom == null) breakerRoom = FindAnyObjectByType<BreakerRoom>();

        if (enterButton != null) enterButton.onClick.AddListener(EnterPassage);
        fade = ScreenOverlayFactory.CreateBlack("StorageRunFade (자동 생성)", 120);   // 점프스케어·암전 덮개(110)보다 위
        ShowPassage(false);

        if (passageGroup == null || enterButton == null)
            Debug.LogWarning("창고런: Passage Group / Enter Button 칸이 비어 있음 — 통로 버튼이 안 나옴");
        if (breakerRoom == null)
            Debug.LogWarning("창고런: 전기실(BreakerRoom)을 못 찾음 — 들어갈 수 없음");
    }

    void Update()
    {
        UpdateFade();

        bool show = !inRun && !Busy
                    && wiring != null && wiring.IsActive
                    && view != null && view.currentZone == rearZone
                    && !(BatteryManager.Instance != null && BatteryManager.Instance.isDead)
                    && !(jumpscare != null && jumpscare.IsPlaying)
                    && !(GameManager.Instance != null && (GameManager.Instance.isNightOver || GameManager.Instance.isGameOver));
        ShowPassage(show);
    }

    void ShowPassage(bool show)
    {
        if (passageGroup == null) return;
        passageGroup.alpha = show ? 1f : 0f;
        passageGroup.interactable = show;
        passageGroup.blocksRaycasts = show;
    }

    // ===== 들어가기 (통로 버튼) =====
    public void EnterPassage()
    {
        if (inRun || Busy) return;
        if (wiring == null || !wiring.IsActive) { Debug.Log("창고 통로: 정전이 아니라 잠겨 있음"); return; }
        if (jumpscare != null && jumpscare.IsPlaying) return;
        if (BatteryManager.Instance != null && BatteryManager.Instance.isDead) return;
        if (breakerRoom == null) { Debug.LogError("창고런: 전기실(BreakerRoom)이 없어서 못 들어감"); return; }

        camT = FindPlayerCamera();
        if (camT == null) { Debug.LogError("창고런: 플레이어 카메라를 못 찾음"); return; }

        ShowPassage(false);
        Debug.Log("★ 창고 통로로 들어감...");
        StartFade(DoEnter);
    }

    void DoEnter()
    {
        Freeze();
        DarkenOutside();
        savedLocalPos = camT.localPosition;   // 멈춘 뒤에 저장 (그 뒤로 아무도 안 움직임)
        savedLocalRot = camT.localRotation;
        breakerRoom.Begin(this, camT);
        inRun = true;
        Debug.Log("★★ 전기실 도착 — 관리실 전부 멈춤 (밤 시계 정지). 브레이커를 전부 고쳐야 나갈 수 있음");
    }

    // ===== 나가기 (전기실 나가기 버튼 → BreakerRoom이 부름) =====
    public void RequestExit()
    {
        if (!inRun || Busy) return;
        if (breakerRoom == null || !breakerRoom.AllRepaired) return;
        Debug.Log("★ 전기실에서 나감...");
        StartFade(DoReturn);
    }

    void DoReturn()
    {
        breakerRoom.End();
        RestoreOutside();
        camT.localPosition = savedLocalPos;
        camT.localRotation = savedLocalRot;

        if (wiring != null) wiring.Repair();   // 전기 복구 — CCTV·패널 잠금 해제, 조명·시계 정상 (멈춘 채로 먼저)
        Unfreeze();
        if (GameManager.Instance != null) GameManager.Instance.SkipTime(returnTimeSkip);
        inRun = false;
        Debug.Log("★★ 관리실 복귀 — 전기 복구, 다시 움직임");
    }

    // ===== 관리실 멈춤 / 풀기 =====
    void Freeze()
    {
        frozen.Clear();
        // 귀신
        FreezeAll<Hyunsoong>();
        FreezeAll<Hyunwoo>();
        FreezeAll<Yeonho>();
        FreezeAll<YeonhoAggression>();
        FreezeAll<KimWook>();
        FreezeAll<JoonYoung>();
        // 고장 타이머·배터리·밤 시계
        FreezeAll<AudioSystem>();
        FreezeAll<CameraSystem>();
        FreezeAll<VentilationSystem>();
        FreezeAll<BatteryManager>();
        FreezeAll<GameManager>();
        // 관리실 조작 (전기실에서 카메라를 안 건드리게) + 화면 전환 감지
        FreezeAll<ScreenTransitionDetector>();
        FreezeAll<ViewController>();
        FreezeAll<CCTVController>();
        FreezeAll<PanelController>();
        FreezeAll<ShutterController>();
        FreezeAll<EmergencyBell>();
    }

    void FreezeAll<T>() where T : Behaviour
    {
        foreach (var c in FindObjectsByType<T>())
        {
            if (c == null || !c.enabled) continue;   // 이미 꺼져 있던 건 안 건드림 (나올 때도 안 켬)
            c.enabled = false;
            frozen.Add(c);
        }
    }

    void Unfreeze()
    {
        foreach (var c in frozen)
            if (c != null) c.enabled = true;
        frozen.Clear();
    }

    // ===== 전기실 어둠 =====
    void DarkenOutside()
    {
        if (!darkenOutsideLight || darkened) return;
        darkened = true;

        darkenedSuns.Clear();
        foreach (var l in FindObjectsByType<Light>())
        {
            if (l == null || l.type != LightType.Directional || !l.enabled) continue;
            l.enabled = false;                                   // 맵 전체를 비추는 방향광
            darkenedSuns.Add(l);
        }

        savedAmbientProbe = RenderSettings.ambientProbe;
        RenderSettings.ambientProbe = new SphericalHarmonicsL2();   // 환경광 0 (그림자 쪽도 새까맣게)
        savedReflectionIntensity = RenderSettings.reflectionIntensity;
        RenderSettings.reflectionIntensity = 0f;                    // 철 벽이 주변을 반사해 번들거리지 않게
    }

    void RestoreOutside()
    {
        if (!darkened) return;
        darkened = false;
        foreach (var l in darkenedSuns) if (l != null) l.enabled = true;
        darkenedSuns.Clear();
        RenderSettings.ambientProbe = savedAmbientProbe;
        RenderSettings.reflectionIntensity = savedReflectionIntensity;
    }

    Transform FindPlayerCamera()
    {
        if (cctv != null && cctv.PlayerCamera != null) return cctv.PlayerCamera.transform;
        if (view != null)
        {
            var cam = view.GetComponentInChildren<Camera>();
            if (cam != null) return cam.transform;
        }
        return null;
    }

    // ===== 페이드 =====
    void StartFade(System.Action atBlack)
    {
        afterFadeOut = atBlack;
        phase = Phase.FadeOut;
        phaseTimer = 0f;
    }

    void UpdateFade()
    {
        if (fade == null || phase == Phase.None) return;
        float t = Mathf.Max(0.01f, fadeTime);
        phaseTimer += Time.deltaTime;

        if (phase == Phase.FadeOut)
        {
            fade.alpha = Mathf.Clamp01(phaseTimer / t);
            if (phaseTimer >= t)
            {
                fade.alpha = 1f;
                var action = afterFadeOut;
                afterFadeOut = null;
                action?.Invoke();          // 완전히 깜깜할 때 이동
                phase = Phase.FadeIn;
                phaseTimer = 0f;
            }
        }
        else // FadeIn
        {
            fade.alpha = 1f - Mathf.Clamp01(phaseTimer / t);
            if (phaseTimer >= t)
            {
                fade.alpha = 0f;
                phase = Phase.None;
            }
        }
    }

    // ===== 테스트 =====
    // 플레이 중 StorageRun 컴포넌트 이름 옆 ⋮(또는 우클릭) → "테스트: 지금 정전 내기" — 현우 기다릴 필요 없이 창고런 시험
    [ContextMenu("테스트: 지금 정전 내기")]
    void TestOutage()
    {
        if (!Application.isPlaying) { Debug.Log("플레이 중에만 돼요"); return; }
        WiringFailure.GetOrCreate().BeginForTest();
    }
}

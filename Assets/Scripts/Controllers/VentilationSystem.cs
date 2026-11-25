using UnityEngine;
using UnityEngine.UI;

// 환기 시스템 (산소순환장치) — 직접 죽이지 않는 "빈틈 여는" 장치 (기획서 ③, 구현노트 §12·§15)
//
// [고장 원인]
//  - 셔터 장시간 폐쇄 (공기 게이지) — 지금 유일한 원인
//  - 김욱 / 준영 공격 → BreakVent() 호출 (그 귀신들 만들 때 연결)
//
// [고장 효과]
//  - 셔터 강제 개방 + 못 닫음 (질식 방지 안전장치) → 현우가 그 틈에 침투 가능
//  - 주기적으로 화면이 어두워졌다 돌아옴 = 강제 화면 전환 (연호 창문앞→빼꼼, 빼꼼→즉사, 현승 armed→즉시 STRIKE1)
//  - 연호 흉포: YeonhoAggression이 "환기 오류 중" 상태를 보고 자동으로 켬
//
// [리셋] 패널에서 5초 (고장 없어도 예방적으로 가능 — 공기 게이지 초기화)
public class VentilationSystem : MonoBehaviour
{
    [Header("참조")]
    public ShutterController shutter;
    public ScreenTransitionDetector transitionDetector;
    // ⚠️ 지금은 비워두는 게 정상 — Start에서 검은 화면을 자동 생성함.
    // Phase 8에서 직접 만든 흐려짐 연출(Canvas + CanvasGroup)로 바꾸고 싶을 때만 여기에 연결.
    // (이름은 blur지만 실제로는 "검게 어두워짐". 진짜 흐림은 Phase 8 포스트프로세싱)
    [Tooltip("비워두면 자동 생성. Phase 8에서 직접 만든 흐려짐 연출로 교체할 때만 연결 (CanvasGroup 필요)")]
    public CanvasGroup blurOverlay;

    [Header("고장 상태")]
    public bool isVentBroken = false;

    [Header("원인: 셔터 장시간 폐쇄 (공기 게이지)")]
    public float closedGrace = 5f;       // 이만큼은 닫아도 게이지 안 쌓임 (현우 격퇴 유지 시간)
    public float airLimit = 20f;         // 유예 이후 쌓인 게이지가 이만큼 되면 고장
    public float closedTime = 0f;        // (확인용) 닫힌 시간 — 열면 0이 아니라 연 만큼 줄어듦(깜빡 꼼수 방지)
    public float airGauge = 0f;          // (확인용) 공기 탁함 게이지 — 닫혀서 유예 넘으면 +, 열면 -

    [Header("효과: 주기적 흐려짐 = 강제 화면 전환")]
    public float firstBlurDelay = 8f;    // 고장 후 첫 흐려짐까지
    public float blurInterval = 12f;     // 이후 반복 간격
    public float fadeTime = 0.5f;        // 어두워지는 시간 = 돌아오는 시간

    [Header("리셋")]
    public float resetDuration = 5f;

    private float blurTimer = 0f;
    private float fadeTimer = -1f;       // 흐려짐 진행 시간 (-1 = 흐려짐 없음)
    private bool transitionFired = false;
    private float resetTimer = 0f;
    private bool isResetting = false;
    private float activeResetDuration = 5f;

    void Start()
    {
        if (shutter == null) shutter = FindAnyObjectByType<ShutterController>();
        if (transitionDetector == null) transitionDetector = FindAnyObjectByType<ScreenTransitionDetector>();
        if (blurOverlay == null) blurOverlay = CreateBlurOverlay();
        blurOverlay.alpha = 0f;
    }

    void Update()
    {
        UpdateFade();   // 진행 중인 흐려짐은 끝까지 (중간에 끊기면 어색)

        // 리셋 진행 중 — 고장 효과·게이지 모두 멈춤
        if (isResetting)
        {
            resetTimer += Time.deltaTime;
            if (resetTimer >= activeResetDuration)
                CompleteReset();
            return;
        }

        if (isVentBroken)
        {
            // 주기적 흐려짐
            blurTimer -= Time.deltaTime;
            if (blurTimer <= 0f)
            {
                StartFade();
                blurTimer = blurInterval;
            }
            return;   // 고장 중엔 셔터가 열려 있으니 게이지 안 돎
        }

        UpdateAirGauge();
    }

    // === 원인: 공기 게이지 ===
    void UpdateAirGauge()
    {
        if (shutter == null) return;
        float dt = Time.deltaTime;

        if (shutter.isShutterClosed)
        {
            closedTime += dt;
            if (closedTime > closedGrace)
                airGauge += dt;          // 유예(5초) 넘은 뒤부터 쌓임
        }
        else
        {
            closedTime = Mathf.Max(0f, closedTime - dt);   // 깜빡 열었다 닫아도 유예가 다시 생기지 않게
            airGauge = Mathf.Max(0f, airGauge - dt);
        }

        if (airGauge >= airLimit)
            BreakVent("셔터 장시간 폐쇄");
    }

    // === 고장 발생 (김욱/준영도 나중에 이걸 호출) ===
    public void BreakVent(string reason)
    {
        if (isVentBroken) return;
        isVentBroken = true;
        blurTimer = firstBlurDelay;
        if (shutter != null) shutter.SetForcedOpen(true);
        Debug.Log($"★★ 환기 오류! ({reason}) — 셔터 강제 개방, 주기적 흐려짐. 리셋 필요");
        // 연호 흉포는 YeonhoAggression이 "환기 오류 중" 상태를 보고 자동으로 켬
        // TODO: 경보/사이렌 (Phase 8 / 고장 알림 설계)
    }

    // === 흐려짐 연출 (임시: 검은 화면) ===
    void StartFade()
    {
        fadeTimer = 0f;
        transitionFired = false;
    }

    void UpdateFade()
    {
        if (fadeTimer < 0f) return;

        fadeTimer += Time.deltaTime;
        float t = fadeTimer / fadeTime;   // 0~1 어두워짐, 1~2 돌아옴

        // 가장 어두운 순간 = 강제 화면 전환 (한 번만)
        if (!transitionFired && t >= 1f)
        {
            transitionFired = true;
            if (transitionDetector != null) transitionDetector.ForceTransition();
        }

        float alpha = (t <= 1f) ? t : 2f - t;
        if (blurOverlay != null) blurOverlay.alpha = Mathf.Clamp01(alpha) * 0.95f;

        if (t >= 2f)
        {
            fadeTimer = -1f;
            if (blurOverlay != null) blurOverlay.alpha = 0f;
        }
    }

    // 흐려짐용 검은 화면 자동 생성 (Inspector 연결 누락 방지)
    // sortingOrder 100 = 다른 UI 위 (임시 — 점프스케어와 겹치면 잠깐 가려질 수 있음, Phase 8에서 정리)
    CanvasGroup CreateBlurOverlay()
    {
        return ScreenOverlayFactory.CreateBlack("VentBlurOverlay (자동 생성)", 100);
    }

    // === 리셋 ===
    // 개별 리셋 (패널 버튼 OnClick) — ⚠️ 버튼용이라 매개변수 붙이지 말 것
    public void StartReset()
    {
        StartResetFor(resetDuration);
    }

    // 시간 지정 리셋 (전체 리셋이 호출)
    public void StartResetFor(float duration)
    {
        if (isResetting) return;
        isResetting = true;
        resetTimer = 0f;
        activeResetDuration = duration;
        Debug.Log($"환기 리셋 시작... ({activeResetDuration}초)");
    }

    void CompleteReset()
    {
        isVentBroken = false;
        isResetting = false;
        resetTimer = 0f;
        closedTime = 0f;
        airGauge = 0f;           // 예방적 리셋 효과: 공기 게이지 초기화
        if (shutter != null) shutter.SetForcedOpen(false);   // 잠금만 풀림, 셔터는 열린 채
        Debug.Log("환기 리셋 완료! (셔터 잠금 해제, 공기 게이지 초기화)");
    }

    public bool IsResetting() { return isResetting; }
    public bool IsVentBroken() { return isVentBroken; }
}

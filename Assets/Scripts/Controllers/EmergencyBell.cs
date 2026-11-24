using UnityEngine;
using UnityEngine.UI;

// 비상 종: 일반 종으로 연호를 못 막았을 때 쓰는 진짜 최후 수단.
// - 효과는 연호가 "창문앞"일 때만 (완전 후퇴 → 재등장 대기). 그 외엔 헛방.
// - 누르면 효과 여부와 상관없이 횟수·쿨·배터리 소모 (없는데 누른 건 플레이어 실수).
// - 오디오 시스템과 별개 장치: 오디오 고장 중에도 작동, 소진해도 오디오 고장 안 남, 리셋으로 안 채워짐.
// - 창문 옆 벽 World Space 버튼 (가운데 구역 볼 때만 표시).
public class EmergencyBell : MonoBehaviour
{
    [Header("참조")]
    public Yeonho yeonho;
    public ViewController viewController;
    public CanvasGroup buttonGroup;      // 비상 종 버튼 (가운데 볼 때만)
    public int centerZone = 1;           // 가운데 구역

    [Header("대가")]
    public float batteryCost = 20f;      // 배터리 왕창
    public float cooldown = 60f;         // 쿨 아주 길게 (연속 실패 방지)
    public int usesPerNight = 2;         // 하루 2번

    [HideInInspector] public bool inputLocked = false;   // 방전 암전: 버튼 숨김 + 작동 안 함 (BatteryBlackout가 세팅)

    private int usesLeft;
    private float cooldownTimer = 0f;

    void Start()
    {
        usesLeft = usesPerNight;
        if (viewController == null) viewController = FindAnyObjectByType<ViewController>();
        if (yeonho == null) yeonho = FindAnyObjectByType<Yeonho>();
    }

    void Update()
    {
        // 쿨 감소
        if (cooldownTimer > 0f) cooldownTimer -= Time.deltaTime;

        // 가운데 구역 볼 때만 버튼 표시
        bool showBell = !inputLocked && (viewController != null && viewController.currentZone == centerZone);
        if (buttonGroup != null)
        {
            buttonGroup.alpha = showBell ? 1f : 0f;
            buttonGroup.interactable = showBell;
            buttonGroup.blocksRaycasts = showBell;
        }
    }

    // 버튼 OnClick이 호출
    public void PressEmergencyBell()
    {
        if (inputLocked) return;   // 방전 암전

        // 횟수 소진 → 그냥 안 눌림 (오디오와 무관)
        if (usesLeft <= 0)
        {
            Debug.Log("비상 종 소진 - 작동 안 함");
            // TODO: 거부음 사운드 (Phase 8)
            return;
        }

        // 쿨타임 중
        if (cooldownTimer > 0f)
        {
            Debug.Log($"비상 종 쿨타임 중 ({cooldownTimer:F0}초 남음)");
            // TODO: 거부음 사운드 (Phase 8)
            return;
        }

        // 누른 순간 무조건 소모 (헛방이어도)
        usesLeft--;
        cooldownTimer = cooldown;
        if (BatteryManager.Instance != null) BatteryManager.Instance.Drain(batteryCost);
        // TODO: 비상 종소리 (Phase 8)

        bool effective = (yeonho != null) && yeonho.EmergencyPush();
        if (effective)
            Debug.Log($"★ 비상 종 발동! 연호 완전 후퇴 (남은 횟수 {usesLeft})");
        else
            Debug.Log($"비상 종 헛방 — 연호 창문앞 아님 (남은 횟수 {usesLeft})");
    }
}

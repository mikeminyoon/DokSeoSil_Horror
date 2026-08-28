using UnityEngine;
using UnityEngine.UI;

// 비상 종: 창문앞 연호 최후 수단 (광역, 완전 리셋).
// 배터리 왕창 + 쿨 길게 + 하루 4번 + 초과 시 오디오 오류.
// Overlay 버튼 (가운데 구역 볼 때만). 셔터 버튼과 동일 방식.
public class EmergencyBell : MonoBehaviour
{
    [Header("참조")]
    public Yeonho yeonho;
    public ViewController viewController;
    public CanvasGroup buttonGroup;      // 비상 종 버튼 (가운데 볼 때만)
    public int centerZone = 1;           // 가운데 구역

    [Header("대가")]
    public float batteryCost = 20f;      // 배터리 왕창
    public float cooldown = 15f;         // 쿨 길게
    public int usesPerNight = 4;         // 하루 4번

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
        bool showBell = (viewController != null && viewController.currentZone == centerZone);
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
        // 횟수 소진 → 오디오 오류
        if (usesLeft <= 0)
        {
            Debug.Log("비상 종 소진! 오디오 오류");
            // TODO: 오디오 오류 발동 (오디오 고장 시스템 연결)
            // TODO: 거부음 사운드 (Phase 8)
            return;
        }

        // 쿨타임 중
        if (cooldownTimer > 0f)
        {
            Debug.Log("비상 종 쿨타임 중");
            // TODO: 거부음 사운드 (Phase 8)
            return;
        }

        // 발동
        if (yeonho != null)
        {
            bool success = yeonho.EmergencyPush();
            if (success)
            {
                usesLeft--;
                cooldownTimer = cooldown;
                if (BatteryManager.Instance != null) BatteryManager.Instance.Drain(batteryCost);
                Debug.Log($"★ 비상 종 발동! (남은 횟수 {usesLeft})");
                // TODO: 비상 종소리 (Phase 8)
            }
            else
            {
                // 빼꼼 등 이미 늦음
                Debug.Log("비상 종 무효 (연호 빼꼼 — 이미 늦음)");
            }
        }
    }
}
using UnityEngine;

// 환풍구 셔터: 오른쪽 구역에서 버튼으로 위아래 토글.
// 닫으면 벤트 막힘(윤진 차단), 열면 통로 뚫림.
public class ShutterController : MonoBehaviour
{
    [Header("참조")]
    public ViewController viewController;
    public Transform shutter;           // 움직일 셔터 큐브
    public Transform closedPos;         // 닫힘 위치 (벤트 막음)
    public Transform openPos;           // 열림 위치 (위로 올라감)

    [Header("라이트 (환풍구 확인용, 홀드)")]
    public Light ventLight;             // 환풍구 Spot Light (평소 꺼둠)

    public bool isLightOn = false;      // 라이트 켜짐? (다른 스크립트가 읽음)

    [Header("버튼 UI")]
    public CanvasGroup ventButtons;     // 환풍구 버튼 그룹 (오른쪽 구역에서만)

    [Header("작동 조건")]
    public int ventZone = 2;            // 오른쪽 구역(환풍구)

    [Header("속도")]
    public float moveSpeed = 8f;        // 셔터 여닫는 속도

    public bool isShutterClosed = false;   // 셔터 닫힘? (다른 스크립트가 읽음)
    public bool isForcedOpen = false;      // 환기 오류로 강제 개방 중? (이동안 못 닫음 — VentilationSystem이 세팅)
    public bool inputLocked = false;       // 방전 암전: 버튼 숨김 + 셔터·불빛 조작 불가 (BatteryBlackout가 세팅)

    [Header("배터리 소모")]
    public float shutterDrainRate = 2f;

    void Start()
    {
        if (viewController == null) viewController = GetComponent<ViewController>();
        // 시작은 열린 상태
        if (shutter != null && openPos != null)
            shutter.position = openPos.position;
        SetButtons(false);
        if (ventLight != null) ventLight.enabled = false;
    }

    void Update()
    {
        // 오른쪽 구역 볼 때만 버튼 보이게
        bool inVentZone = viewController.enabled && viewController.currentZone == ventZone;
        SetButtons(inVentZone && !inputLocked);

        // 셔터 닫혀있으면 배터리 소모
        if (isShutterClosed && BatteryManager.Instance != null)
            BatteryManager.Instance.DrainPerSecond(shutterDrainRate);
        
        // 셔터를 목표 위치로 부드럽게 이동
        if (shutter != null)
        {
            Transform target = isShutterClosed ? closedPos : openPos;
            shutter.position = Vector3.Lerp(shutter.position, target.position, moveSpeed * Time.deltaTime);
        }

        
    }

    // 셔터 토글 (버튼이 호출)
    public void ToggleShutter()
    {
        if (inputLocked) return;   // 방전 암전

        // 환기 오류 중엔 안전장치가 셔터를 붙잡고 있어서 못 닫음
        if (isForcedOpen)
        {
            Debug.Log("환기 오류 — 셔터가 강제로 열려 있음 (리셋 필요)");
            // TODO: 거부음 (Phase 8)
            return;
        }

        isShutterClosed = !isShutterClosed;
        Debug.Log(isShutterClosed ? "셔터 닫힘" : "셔터 열림");
    }

    // 환기 오류 강제 개방 켜기/끄기 (VentilationSystem이 호출)
    // 켜면 즉시 열리고 잠김. 끄면 잠금만 풀림 — 셔터는 열린 채로, 닫는 건 플레이어 몫.
    public void SetForcedOpen(bool on)
    {
        isForcedOpen = on;
        if (on && isShutterClosed)
        {
            isShutterClosed = false;
            Debug.Log("★ 환기 오류 — 셔터 강제 개방!");
        }
    }

    void SetButtons(bool on)
    {
        if (ventButtons == null) return;
        ventButtons.alpha = on ? 1f : 0f;
        ventButtons.interactable = on;
        ventButtons.blocksRaycasts = on;
    }
    // 라이트 켜기 (버튼 누름 = PointerDown)
    public void LightOn()
    {
        if (inputLocked) return;   // 방전 암전 — 불빛 안 켜짐
        isLightOn = true;
        if (ventLight != null) ventLight.enabled = true;
    }

    // 라이트 끄기 (버튼 뗌 = PointerUp)
    public void LightOff()
    {
        isLightOn = false;
        if (ventLight != null) ventLight.enabled = false;
    }

    // 방전 암전: 셔터 열림(원작 FNAF1처럼 문이 열려버림) + 불빛 끔 + 조작 잠금
    public void PowerOff()
    {
        inputLocked = true;
        isShutterClosed = false;
        LightOff();
        SetButtons(false);
    }

    // 점프스케어 등으로 조작 강제 잠금 (버튼 숨김 + 라이트 끔)
    public void ForceLock()
    {
        SetButtons(false);
        if (ventLight != null) ventLight.enabled = false;
        isLightOn = false;
    }
}
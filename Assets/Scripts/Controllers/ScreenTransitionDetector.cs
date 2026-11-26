using UnityEngine;
using System;

// 화면 전환 감지 (§0.5) — "기기 조작 = 하드컷"만 전환으로 친다.
// 시야 회전은 부드러운 연속이라 전환 아님.
//
// [신호]
//   OnScreenTransition: CCTV 올림/내림 + 패널 열기/닫기 + 강제 전환 — 현승 armed·현우 armed가 씀 (예전 그대로)
//   OnLowered:    올려진 상태(CCTV 켜짐 또는 패널 열림) → 내려진 상태가 되는 순간 (강제로 내려져도) — 연호 "관리실 안" 즉사용
//   OnVisionLost: 강제 전환(환기 흐려짐) = 시야를 잃음 — 연호 빼꼼·관리실 안 즉사용
//                 (다른 귀신 잔상(점프스케어)은 연호가 JumpscareOverlay.IsPlaying을 직접 봄)
//   PlayerIsUp:   지금 CCTV나 패널을 올리고 있나 — 연호 빼꼼 카운트용 (구현노트 §15.24)
public class ScreenTransitionDetector : MonoBehaviour
{
    [Header("참조")]
    public CCTVController cctv;
    public PanelController panel;

    public event Action OnScreenTransition;
    public event Action OnLowered;
    public event Action OnVisionLost;

    // CCTV 켜짐(켜는 순간부터) 또는 패널 열림(여는 순간부터)
    public bool PlayerIsUp => (cctv != null && cctv.isCameraDown) || (panel != null && panel.isPanelOpen);

    private bool lastCameraDown;
    private bool lastPanelOpen;

    void Start()
    {
        if (cctv == null) cctv = GetComponent<CCTVController>();
        if (panel == null) panel = GetComponent<PanelController>();
        lastCameraDown = cctv != null && cctv.isCameraDown;
        lastPanelOpen = panel != null && panel.isPanelOpen;
    }

    void Update()
    {
        bool wasUp = lastCameraDown || lastPanelOpen;
        bool transitioned = false;

        // CCTV 올림/내림 = 하드컷
        if (cctv != null && cctv.isCameraDown != lastCameraDown)
        {
            lastCameraDown = cctv.isCameraDown;
            transitioned = true;
        }

        // 리셋 패널 열림/닫힘 = 하드컷
        if (panel != null && panel.isPanelOpen != lastPanelOpen)
        {
            lastPanelOpen = panel.isPanelOpen;
            transitioned = true;
        }

        if (transitioned)
        {
            Debug.Log("★ 화면 전환 (기기 조작)");
            OnScreenTransition?.Invoke();
        }

        // 올려져 있다가 내려진 순간 (플레이어가 내렸든, 점프스케어·배선 고장이 강제로 내렸든)
        bool isUp = lastCameraDown || lastPanelOpen;
        if (wasUp && !isUp)
            OnLowered?.Invoke();
    }

    // 환기 오류(화면 흐려짐) 등 강제 전환용 — 도망 봉쇄. 시야를 잃는 것이기도 함
    public void ForceTransition()
    {
        Debug.Log("★ 강제 화면 전환");
        OnScreenTransition?.Invoke();
        OnVisionLost?.Invoke();
    }
}

using UnityEngine;
using System;

// 화면 전환 감지 (§0.5) — "기기 조작 = 하드컷"만 전환으로 친다.
// 시야 회전은 부드러운 연속이라 전환 아님.
public class ScreenTransitionDetector : MonoBehaviour
{
    [Header("참조")]
    public CCTVController cctv;
    public PanelController panel;    

    public event Action OnScreenTransition;

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
        bool transitioned = false;

        // CCTV 올림/내림 = 하드컷
        if (cctv != null && cctv.isCameraDown != lastCameraDown)
        {
            lastCameraDown = cctv.isCameraDown;
            transitioned = true;
        }

        // 리셋 패널 열림/닫힘 = 하드컷                                    ← 추가
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
    }

    // 환기 오류(화면 흐려짐) 등 강제 전환용 — 도망 봉쇄
    public void ForceTransition()
    {
        Debug.Log("★ 강제 화면 전환");
        OnScreenTransition?.Invoke();
    }
}
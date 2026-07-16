using UnityEngine;
using System;

// 화면 전환 감지 (§0.5) — "기기 조작 = 하드컷"만 전환으로 친다.
// 시야 회전은 부드러운 연속이라 전환 아님.
public class ScreenTransitionDetector : MonoBehaviour
{
    [Header("참조")]
    public CCTVController cctv;
    // TODO: public ResetPanelController resetPanel;  // 왼쪽 구역 리셋 패널 (미구현)

    public event Action OnScreenTransition;

    private bool lastCameraDown;

    void Start()
    {
        if (cctv == null) cctv = GetComponent<CCTVController>();
        lastCameraDown = cctv != null && cctv.isCameraDown;
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

        // TODO: 리셋 패널 올림/내림도 여기에 추가

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
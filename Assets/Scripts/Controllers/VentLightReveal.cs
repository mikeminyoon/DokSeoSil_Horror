using UnityEngine;

// 환풍구 불빛에만 보임 — FNAF1 문 불빛 방식 (구현노트 §15.19)
// 관리실 환풍구에 있는 귀신은 불빛 버튼을 누르고 있을 때만 보이고, 평소엔 암흑이라 안 보임.
//
// 귀신 스크립트가 inRoomVent를 켜고 끔 (관리실 환풍구 노드에 있을 때만 true).
//   inRoomVent = false → 항상 보임 (CCTV로 보는 다른 노드들)
//   inRoomVent = true  → 불빛 켜졌을 때만 보임 (셔터가 닫혀 가리고 있으면 안 보임 — 셔터 모양과 무관하게 보장)
// 숨길 땐 렌더러만 끔 (모델·애니메이터는 그대로).
//
// 지금: 현우. 나중에 현승 환풍구 루트(R3)를 만들면 현승 모델에도 붙여서 똑같이 쓰면 됨.
public class VentLightReveal : MonoBehaviour
{
    [Header("참조 (비워두면 자동)")]
    public ShutterController shutter;     // 불빛 상태(isLightOn) 읽기

    [Header("상태 (귀신 스크립트가 세팅)")]
    public bool inRoomVent = false;       // 관리실 환풍구에 있나

    private Renderer[] renderers;
    private bool shown = true;

    // 지금 플레이어가 환풍구 안을 볼 수 있나 (불빛 켜짐 + 셔터 열림)
    // 귀신이 "보이는 앞에서 나타나거나 사라지면 안 될 때" 확인용
    public static bool PlayerSeesVent(ShutterController s)
    {
        return s != null && s.isLightOn && !s.isShutterClosed;
    }

    void Awake()
    {
        renderers = GetComponentsInChildren<Renderer>(true);
    }

    void Start()
    {
        if (shutter == null) shutter = FindAnyObjectByType<ShutterController>();
        Apply();
    }

    void LateUpdate()
    {
        Apply();
    }

    void Apply()
    {
        // 관리실 환풍구: 불빛 켜짐 + 셔터가 안 가림 일 때만 보임
        bool show = !inRoomVent || (shutter != null && shutter.isLightOn && !shutter.IsCoveringVent);
        if (show == shown) return;
        shown = show;
        foreach (var r in renderers)
            if (r != null) r.enabled = show;
    }
}

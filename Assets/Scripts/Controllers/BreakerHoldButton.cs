using UnityEngine;
using UnityEngine.EventSystems;

// 브레이커 버튼 "누르고 있기" 판정 (구현노트 §15.27)
// BreakerRoom이 브레이커 버튼마다 자동으로 붙임 — EventTrigger 연결할 필요 없음.
// 왼쪽 버튼을 누르는 순간 true, 떼거나 버튼 밖으로 나가면 false.
public class BreakerHoldButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
{
    public bool IsHeld { get; private set; }

    public void OnPointerDown(PointerEventData e)
    {
        if (e.button == PointerEventData.InputButton.Left) IsHeld = true;
    }

    public void OnPointerUp(PointerEventData e) { IsHeld = false; }
    public void OnPointerExit(PointerEventData e) { IsHeld = false; }
    void OnDisable() { IsHeld = false; }

    // 패널을 내리는 등 더 못 누르게 될 때 BreakerRoom이 부름
    public void Release() { IsHeld = false; }
}

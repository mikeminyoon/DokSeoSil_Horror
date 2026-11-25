using UnityEngine;
using UnityEngine.EventSystems;

// 리셋 패널 한 줄의 "마우스 올림" 감지 → ResetPanelMenu에 알려줌 (>>> 커서 표시용)
// ResetPanelMenu가 플레이 시작할 때 각 줄 버튼에 자동으로 붙임 — 직접 붙일 필요 없음.
public class ResetPanelRowHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [HideInInspector] public bool isHovered = false;

    public void OnPointerEnter(PointerEventData eventData) { isHovered = true; }
    public void OnPointerExit(PointerEventData eventData) { isHovered = false; }

    void OnDisable() { isHovered = false; }
}

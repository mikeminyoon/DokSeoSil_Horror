using UnityEngine;
using UnityEngine.UI;

// 화면 전체를 덮는 검은 덮개를 코드로 만들어 주는 도구.
// 환기 흐려짐(VentilationSystem), 배터리 방전 암전(BatteryBlackout)이 같이 씀.
// 반환하는 CanvasGroup의 alpha로 어둡기 조절 (0 = 안 보임, 1 = 완전 검정). 클릭은 막지 않음.
public static class ScreenOverlayFactory
{
    public static CanvasGroup CreateBlack(string name, int sortingOrder)
    {
        var root = new GameObject(name);
        var canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = sortingOrder;   // 클수록 위에 그려짐

        var dark = new GameObject("Dark");
        dark.transform.SetParent(root.transform, false);
        var img = dark.AddComponent<Image>();
        img.color = Color.black;
        img.raycastTarget = false;            // 클릭 막지 않음
        var rt = img.rectTransform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        var group = root.AddComponent<CanvasGroup>();
        group.alpha = 0f;
        group.interactable = false;
        group.blocksRaycasts = false;
        return group;
    }
}

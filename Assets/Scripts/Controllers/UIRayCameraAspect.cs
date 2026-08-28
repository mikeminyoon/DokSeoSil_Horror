using UnityEngine;

// World UI 클릭 판정 전용 카메라(UIRayCamera)의 종횡비를 저해상도 RT 비율에 맞춘다.
// RT(4:3 등)를 화면(16:9 등)에 늘려서 보여주는 구조라서, 클릭 판정도
// 화면 비율이 아니라 RT 비율로 계산해야 마우스 위치와 실제 클릭 지점이 맞는다.
public class UIRayCameraAspect : MonoBehaviour
{
    public RenderTexture referenceRT;   // 기준 저해상도 RT (LowResRT)

    private Camera cam;

    void Awake()
    {
        cam = GetComponent<Camera>();
    }

    void LateUpdate()
    {
        if (referenceRT != null && cam != null)
            cam.aspect = (float)referenceRT.width / referenceRT.height;
    }
}

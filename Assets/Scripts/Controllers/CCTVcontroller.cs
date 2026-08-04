using UnityEngine;

// CCTV 컨트롤러 (FNAF식 토글):
// 마우스가 화면 맨 아래에 "새로 진입"하면 CCTV on/off 전환.
// 켜진 동안 마우스 자유(방 클릭용). 끄려면 마우스를 위로 뺐다가 다시 아래로.
public class CCTVController : MonoBehaviour
{
    [Header("참조")]
    public ViewController viewController;
    public Transform cctvViewPoint;
    public PanelController panel;

    [Header("작동 조건")]
    public int allowedZone = 1;         // 가운데 구역에서만 켤 수 있음

    [Header("토글 영역 (화면 비율 0~1)")]
    [Tooltip("마우스 Y가 이 아래로 들어오면 토글 트리거")]
    public float bottomZone = 0.12f;

    [Header("전진 속도")]
    public float moveSpeed = 6f;

    public bool isCameraDown = false;
    private Camera cam;
    private Transform camT;

    // 카메라 원래 집 위치 (고정 — 저장 안 하고 이걸로 복귀)
    private Vector3 homeLocalPos;
    private Quaternion homeLocalRot;

    private bool returning = false;
    private bool wasInBottom = false;   // 직전 프레임에 아래 영역에 있었나 (재진입 감지용)

    void Start()
    {
        cam = GetComponentInChildren<Camera>();
        camT = cam.transform;
        if (viewController == null)
            viewController = GetComponent<ViewController>();

        // 처음 위치 = 영원한 집 (구역 전환은 부모 회전이라 카메라 자식 위치는 안 바뀜)
        homeLocalPos = camT.localPosition;
        homeLocalRot = camT.localRotation;
    }

    void Update()
    {
        float my = Input.mousePosition.y / Screen.height;
        bool inBottom = my < bottomZone;

        // === 토글 판정: 아래 영역에 새로 진입한 순간만 ===
        if (inBottom && !wasInBottom)
        {
            if (isCameraDown)
                ExitCCTV();
            else
            {
                if (viewController.currentZone == allowedZone)
                    EnterCCTV();
            }
        }
        wasInBottom = inBottom;

        // === 카메라 위치 제어 ===
        if (isCameraDown)
        {
            // CCTV 켜짐: 모니터 앞으로 전진 (위치 + 회전 둘 다 CCTV가 잡음)
            camT.position = Vector3.Lerp(camT.position, cctvViewPoint.position, moveSpeed * Time.deltaTime);
            camT.rotation = Quaternion.Slerp(camT.rotation, cctvViewPoint.rotation, moveSpeed * Time.deltaTime);
        }
        else if (returning)
        {
            // 복귀: 항상 고정된 집으로 (어중간한 위치 저장 문제 없음)
            camT.localPosition = Vector3.Lerp(camT.localPosition, homeLocalPos, moveSpeed * Time.deltaTime);
            camT.localRotation = Quaternion.Slerp(camT.localRotation, homeLocalRot, moveSpeed * Time.deltaTime);

            float posDist = Vector3.Distance(camT.localPosition, homeLocalPos);
            float rotDist = Quaternion.Angle(camT.localRotation, homeLocalRot);

            if (posDist < 0.02f && rotDist < 0.5f)
            {
                camT.localPosition = homeLocalPos;
                camT.localRotation = homeLocalRot;
                returning = false;
            }
        }
    }

    void EnterCCTV()
    {
        isCameraDown = true;

        // 패널이 복귀 중이었으면 중단시킴 (카메라 뺏기)
        if (panel != null) panel.CancelReturn();

        // 저장 안 함 — 복귀는 항상 homeLocalPos로. (어중간한 위치 저장 버그 제거)
        returning = false;

        viewController.enabled = false;
    }

    void ExitCCTV()
    {
        isCameraDown = false;
        returning = true;                 // 위치 복귀 시작 (CCTV가 Lerp로 처리)

        viewController.enabled = true;    // 회전은 즉시 ViewController에 넘김 (딜레이 없음)
    }

    // 점프스케어 등으로 CCTV를 강제 취소 (켜기 전 상태로)
    public void ForceCancel()
    {
        if (isCameraDown)
        {
            isCameraDown = false;
            returning = true;              // 위치는 부드럽게 집으로
            viewController.enabled = true; // 시야 회전 복구
        }
    }

    public bool GetReturning() { return returning; }

    public void CancelReturn()
    {
        returning = false;
    }
}
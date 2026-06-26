using UnityEngine;

// CCTV 컨트롤러 (FNAF식 토글):
// 마우스가 화면 맨 아래에 "새로 진입"하면 CCTV on/off 전환.
// 켜진 동안 마우스 자유(방 클릭용). 끄려면 마우스를 위로 뺐다가 다시 아래로.
public class CCTVController : MonoBehaviour
{
    [Header("참조")]
    public ViewController viewController;
    public Transform cctvViewPoint;

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

    private Vector3 savedLocalPos;
    private Quaternion savedLocalRot;
    private bool returning = false;

    private bool wasInBottom = false;   // 직전 프레임에 아래 영역에 있었나 (재진입 감지용)

    void Start()
    {
        cam = GetComponentInChildren<Camera>();
        camT = cam.transform;
        if (viewController == null)
            viewController = GetComponent<ViewController>();
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
            // CCTV 꺼짐, 복귀 중: 위치만 집으로 Lerp (회전은 ViewController가 이미 잡는 중)
            camT.localPosition = Vector3.Lerp(camT.localPosition, savedLocalPos, moveSpeed * Time.deltaTime);

            // 집에 거의 도착하면 복귀 완료
            if (Vector3.Distance(camT.localPosition, savedLocalPos) < 0.001f)
            {
                camT.localPosition = savedLocalPos;
                returning = false;
            }
        }
    }

    void EnterCCTV()
    {
        isCameraDown = true;

        // 복귀 중이 아닐 때만 집 위치 저장 (복귀 중 재진입 시 원래 집 유지)
        if (!returning)
            savedLocalPos = camT.localPosition;

        returning = false;
        viewController.enabled = false;   // 시야 회전 잠금
    }

    void ExitCCTV()
    {
        isCameraDown = false;
        returning = true;                 // 위치 복귀 시작 (CCTV가 Lerp로 처리)

        viewController.enabled = true;    // 회전은 즉시 ViewController에 넘김 (딜레이 없음)
    }
}
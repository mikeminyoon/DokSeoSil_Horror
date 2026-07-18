using UnityEngine;

// 리셋 패널 컨트롤러: 왼쪽 구역 가면 카메라가 패널로 전진(자동),
// 마우스 아래로 내리면 패널 끄고 가운데 복귀.
// Player에 붙인다.
public class PanelController : MonoBehaviour
{
    [Header("참조")]
    public ViewController viewController;
    public Transform panelViewPoint;        // 카메라가 다가갈 목표점 (패널 앞)
    public CCTVController cctv;  

    [Header("작동 조건")]
    public int panelZone = 0;               // 왼쪽 구역
    public int centerZone = 1;              // 끄면 돌아갈 구역

    [Header("나가기 (마우스 오른쪽")]
    public float rightZone = 0.95f;
    public float rightHoldTime = 0.4f;    // 오른쪽에 이 시간 머물면 패널 닫기

    private float rightTimer = 0f;        // 오른쪽에 머문 시간 누적

    [Header("전진 속도")]
    public float moveSpeed = 6f;

    public bool isPanelOpen = false;        // 다른 스크립트가 읽을 수 있게
    private Camera cam;
    private Transform camT;

    private Vector3 savedLocalPos;
    private Quaternion savedLocalRot;
    private bool returning = false;
    private bool wasInRight = false;

    void Start()
    {
        cam = GetComponentInChildren<Camera>();
        camT = cam.transform;
        if (viewController == null) viewController = GetComponent<ViewController>();
    }

    void Update()
    {
        // 마우스 X를 화면 비율로 (0=왼쪽끝, 1=오른쪽끝)
        float mx = Input.mousePosition.x / Screen.width;
        bool inRight = mx > rightZone;

        if (isPanelOpen)
        {
            Debug.Log($"[PANEL] open:{isPanelOpen} ret:{returning} | VC.enabled:{viewController.enabled} | CCTV.down:{(cctv!=null?cctv.isCameraDown:false)} CCTV.ret:{(cctv!=null?cctv.GetReturning():false)} | camPos:{camT.localPosition}");
        }

        if (!isPanelOpen && !returning)
        {   
            if (viewController.enabled && viewController.currentZone == panelZone)
                OpenPanel();
        }
        else if(isPanelOpen)
        {
            // 패널로 전진 (위치 + 회전 둘 다) — ViewController가 꺼져있으므로 패널이 책임
            camT.position = Vector3.Lerp(camT.position, panelViewPoint.position, moveSpeed * Time.deltaTime);
            camT.rotation = Quaternion.Slerp(camT.rotation, panelViewPoint.rotation, moveSpeed * Time.deltaTime);

            // 오른쪽 끝에 0.4초 머물면 끄기
            if (inRight)
            {
                rightTimer += Time.deltaTime;
                if (rightTimer >= rightHoldTime)
                {
                    rightTimer = 0f;
                    ClosePanel();
                }
            }
            else
            {
                rightTimer = 0f;   // 벗어나면 리셋
            }
        }

        wasInRight = inRight;

        if (returning)
        {
            camT.localPosition = Vector3.Lerp(camT.localPosition, savedLocalPos, moveSpeed * Time.deltaTime);
            camT.localRotation = Quaternion.Slerp(camT.localRotation, savedLocalRot, moveSpeed * Time.deltaTime);

            // 위치 + 회전 둘 다 충분히 가까우면 완료
            float posDist = Vector3.Distance(camT.localPosition, savedLocalPos);
            float rotDist = Quaternion.Angle(camT.localRotation, savedLocalRot);

            if (posDist < 0.02f && rotDist < 0.5f)   // 좀 더 관대하게
            {
                camT.localPosition = savedLocalPos;
                camT.localRotation = savedLocalRot;
                returning = false;
            }
        }
    }

    // 패널 열기 (왼쪽 구역 진입 시 자동)
    void OpenPanel()
    {
        isPanelOpen = true;
        if (!returning)
        {
            savedLocalPos = camT.localPosition;
            savedLocalRot = camT.localRotation;
        }
        returning = false;

        // CCTV가 복귀 중이었으면 중단 (카메라 뺏기)
        if (cctv != null) cctv.CancelReturn();

        viewController.enabled = false;
        Debug.Log("리셋 패널 열림");
    }

    // 패널 닫기 (마우스 아래)
    void ClosePanel()
    {
        isPanelOpen = false;
        returning = true;

        viewController.currentZone = centerZone;
        viewController.enabled = true;
        viewController.SuppressEdgeUntilRelease();

        Debug.Log($"패널 닫힘 | VC.enabled:{viewController.enabled} | zone:{viewController.currentZone}");
    }

    // 점프스케어 등으로 강제 취소
    public void ForceCancel()
    {
        if (isPanelOpen)
            ClosePanel();
    }

    // 다른 기기(CCTV)가 카메라를 가져갈 때 호출 → 패널은 손 뗌
    public void CancelReturn()
    {
        returning = false;
    }
}
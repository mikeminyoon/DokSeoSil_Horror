using UnityEngine;

// 리셋 패널 컨트롤러: 왼쪽 구역 가면 카메라가 패널로 전진(자동),
// Exit 버튼으로 패널 끄고 가운데 복귀.
// Player에 붙인다.
public class PanelController : MonoBehaviour
{
    [Header("참조")]
    public ViewController viewController;
    public Transform panelViewPoint;        // 카메라가 다가갈 목표점 (패널 앞)
    public CCTVController cctv;
    public AudioSystem audioSystem;
    public CameraSystem cameraSystem;

    [Header("패널 구역")]
    public CanvasGroup panelButtons;        // 패널 UI CanvasGroup (패널 열릴 때만 활성화)

    [Header("작동 조건")]
    public int panelZone = 0;               // 왼쪽 구역
    public int centerZone = 1;              // 끄면 돌아갈 구역

    [Header("전진 속도")]
    public float moveSpeed = 6f;

    [Header("전체 리셋 (오디오+카메라 동시 — §12.3 '둘 8s')")]
    public float resetAllDuration = 8f;

    public bool isPanelOpen = false;        // 다른 스크립트가 읽을 수 있게
    private Camera cam;
    private Transform camT;

    // 카메라 원래 집 위치 (고정 — 저장 안 하고 이걸로 복귀)
    private Vector3 homeLocalPos;
    private Quaternion homeLocalRot;

    private bool returning = false;

    void Start()
    {
        cam = GetComponentInChildren<Camera>();
        camT = cam.transform;
        if (audioSystem == null) audioSystem = FindAnyObjectByType<AudioSystem>();
        if (viewController == null) viewController = GetComponent<ViewController>();

        // 처음 위치 = 영원한 집 (구역 전환은 부모 회전이라 카메라 자식 위치는 안 바뀜)
        homeLocalPos = camT.localPosition;
        homeLocalRot = camT.localRotation;

        SetButtons(false);   // 시작 시 꺼둠
    }

    void Update()
    {
        if (!isPanelOpen && !returning)
        {
            if (viewController.enabled && viewController.currentZone == panelZone)
                OpenPanel();
        }
        else if (isPanelOpen)
        {
            // 패널로 전진 (위치 + 회전 둘 다) — ViewController가 꺼져있으므로 패널이 책임
            camT.position = Vector3.Lerp(camT.position, panelViewPoint.position, moveSpeed * Time.deltaTime);
            camT.rotation = Quaternion.Slerp(camT.rotation, panelViewPoint.rotation, moveSpeed * Time.deltaTime);
        }

        if (returning)
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

    // 패널 열기 (왼쪽 구역 진입 시 자동)
    void OpenPanel()
    {
        isPanelOpen = true;
        returning = false;

        // CCTV가 복귀 중이었으면 중단 (카메라 뺏기)
        if (cctv != null) cctv.CancelReturn();

        viewController.enabled = false;

        SetButtons(true);
    }

    // 패널 닫기 (Exit 버튼)
    public void ClosePanel()
    {
        // 리셋 중이면 못 나감 (손 묶임) — 오디오/카메라 어느 쪽이든
        if (audioSystem != null && audioSystem.IsResetting())
        {
            Debug.Log("오디오 리셋 중 - 패널 못 나감");
            return;
        }
        if (cameraSystem != null && cameraSystem.IsResetting())
        {
            Debug.Log("카메라 리셋 중 - 패널 못 나감");
            return;
        }

        isPanelOpen = false;
        returning = true;

        viewController.currentZone = centerZone;
        viewController.enabled = true;
        viewController.SuppressEdgeUntilRelease();

        SetButtons(false);
    }

    // 전체 리셋 버튼이 호출 — 오디오+카메라 동시 시작(§12.3 "둘 8s", 고장 여부 무관 예방적 가능)
    public void ResetAll()
    {
        if (audioSystem != null) audioSystem.StartReset(resetAllDuration);
        if (cameraSystem != null) cameraSystem.StartReset(resetAllDuration);
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

    void SetButtons(bool on)
    {
        if (panelButtons == null) return;
        panelButtons.alpha = on ? 1f : 0f;
        panelButtons.interactable = on;
        panelButtons.blocksRaycasts = on;
    }
}
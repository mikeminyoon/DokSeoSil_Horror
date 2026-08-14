using UnityEngine;

// GazeBox: 창문 정면 볼 때 + CCTV 내렸을 때만 활성화.
// 활성 중 현승과 겹치면 응시 인정.
public class GazeDetector : MonoBehaviour
{
    [Header("참조")]
    public ViewController viewController;
    public CCTVController cctv;

    [Header("설정")]
    public int windowZone = 1;          // 창문 보는 구역 (가운데)

    private Hyunsoong target;
    private Collider box;               // 내 콜라이더

    void Start()
    {
        target = FindAnyObjectByType<Hyunsoong>();
        box = GetComponent<Collider>();

        if (viewController == null) viewController = FindAnyObjectByType<ViewController>();
        if (cctv == null) cctv = FindAnyObjectByType<CCTVController>();
    }

    void Update()
    {
        // 활성 조건: 가운데 구역(창문) + CCTV 내림
        bool zoneOK = (viewController == null) || (viewController.currentZone == windowZone);
        bool cctvOK = (cctv == null) || !cctv.isCameraDown;
        bool shouldBeOn = zoneOK && cctvOK;

        if (box.enabled != shouldBeOn)
        {
            box.enabled = shouldBeOn;

            // 꺼지는 순간 응시 해제 (게이지 감소 시작)
            if (!shouldBeOn && target != null)
                target.SetGazed(false);
        }
    }

    void OnTriggerStay(Collider other)
    {
        var h = other.GetComponentInParent<Hyunsoong>();
        if (h != null) h.SetGazed(true);
    }

    void OnTriggerExit(Collider other)
    {
        var h = other.GetComponentInParent<Hyunsoong>();
        if (h != null) h.SetGazed(false);
    }
}
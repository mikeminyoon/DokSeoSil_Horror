using UnityEngine;
using UnityEngine.UI;

// 점프스케어 연출 (임시: 빨간 화면).
// 나중에 Image의 sprite만 현승 얼굴로 바꾸면 완성.
// Canvas 아래 화면 꽉 채우는 Image에 붙인다.
public class JumpscareOverlay : MonoBehaviour
{
    [Header("설정")]
    public float duration = 1.5f;        // 잔상 지속 시간 (1~2초)

    [Header("조작 잠금 대상")]
    public ViewController viewController;
    public CCTVController cctv;
    public PanelController panel;  

    private Image image;
    private float timer = 0f;
    private bool playing = false;

    void Start()
    {
        image = GetComponent<Image>();
        if (viewController == null) viewController = FindAnyObjectByType<ViewController>();
        if (cctv == null) cctv = FindAnyObjectByType<CCTVController>();
        if (panel == null) panel = FindAnyObjectByType<PanelController>();

        // 시작 시 꺼둠
        if (image != null) image.enabled = false;
    }

    void Update()
    {
        if (!playing) return;

        timer += Time.deltaTime;
        if (timer >= duration)
            Stop();
    }

    public void Play()
    {
        playing = true;
        timer = 0f;

        if (image != null) image.enabled = true;

        // CCTV 켜는 중이었으면 취소 (후폭풍 연출 보이게)
        if (cctv != null) cctv.ForceCancel();
        if (panel != null) panel.ForceCancel();  
        // 조작 전부 잠금
        if (viewController != null) viewController.enabled = false;
        if (cctv != null) cctv.enabled = false;
        if (panel != null) panel.enabled = false; 
        // TODO: 점프스케어 스팅
    }

    void Stop()
    {
        playing = false;
        if (image != null) image.enabled = false;
        if (viewController != null) viewController.enabled = true;
        if (cctv != null) cctv.enabled = true;
         if (panel != null) panel.enabled = true;
    }
}
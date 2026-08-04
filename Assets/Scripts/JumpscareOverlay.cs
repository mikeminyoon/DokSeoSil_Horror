using UnityEngine;
using UnityEngine.UI;

// 점프스케어 연출 (임시: 빨간 화면).
// 나중에 Image의 sprite만 현승 얼굴로 바꾸면 완성.
public class JumpscareOverlay : MonoBehaviour
{
    [Header("설정")]
    public float duration = 1.5f;

    [Header("조작 잠금 대상")]
    public ViewController viewController;
    public CCTVController cctv;
    public PanelController panel;
    public ShutterController shutter;   // ← 추가

    [Header("게임오버")]
    public Color strikeColor = Color.red;
    public Color gameOverColor = new Color(0.3f, 0f, 0.4f);

    private bool isGameOver = false;
    private Image image;
    private float timer = 0f;
    private bool playing = false;

    void Start()
    {
        image = GetComponent<Image>();
        if (viewController == null) viewController = FindAnyObjectByType<ViewController>();
        if (cctv == null) cctv = FindAnyObjectByType<CCTVController>();
        if (panel == null) panel = FindAnyObjectByType<PanelController>();
        if (shutter == null) shutter = FindAnyObjectByType<ShutterController>();   // ← 추가

        if (image != null) image.enabled = false;
    }

    void Update()
    {
        if (isGameOver) return;
        if (!playing) return;

        timer += Time.deltaTime;
        if (timer >= duration)
            Stop();
    }

    public void Play()
    {
        playing = true;
        timer = 0f;

        if (image != null) { image.color = strikeColor; image.enabled = true; }

        if (cctv != null) cctv.ForceCancel();
        if (panel != null) panel.ForceCancel();

        // 조작 전부 잠금
        if (viewController != null) viewController.enabled = false;
        if (cctv != null) cctv.enabled = false;
        if (panel != null) panel.enabled = false;
        if (shutter != null) { shutter.ForceLock(); shutter.enabled = false; }   // ← 추가
        // TODO: 점프스케어 스팅
    }

    public void PlayGameOver()
    {
        isGameOver = true;
        if (image != null) { image.color = gameOverColor; image.enabled = true; }

        if (cctv != null) cctv.ForceCancel();
        if (panel != null) panel.ForceCancel();
        if (viewController != null) viewController.enabled = false;
        if (cctv != null) cctv.enabled = false;
        if (panel != null) panel.enabled = false;
        if (shutter != null) { shutter.ForceLock(); shutter.enabled = false; }   // ← 추가
    }

    void Stop()
    {
        playing = false;
        if (image != null) image.enabled = false;
        if (viewController != null) viewController.enabled = true;
        if (cctv != null) cctv.enabled = true;
        if (panel != null) panel.enabled = true;
        if (shutter != null) shutter.enabled = true;   // ← 추가 (Update가 버튼 복구)
    }
}
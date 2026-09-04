using UnityEngine;

// 모니터 화면 상태 관리 (페이드 대비 구조)
// OFF: 스태틱만 / SWITCHING: 전환 스태틱(버튼 잠금) / ACTIVE: 방 영상(버튼 활성)
public class MonitorDisplay : MonoBehaviour
{
    // 모니터 상태
    public enum State { Off, Switching, Active }
    public State state = State.Off;

    [Header("참조")]
    public CCTVController cctv;
    public CanvasGroup buttonGroup;      // 방 전환 버튼 묶음 (CanvasGroup)
    public Yeonho yeonho;
    public CameraSystem cameraSystem;    // 고장 시 완전 먹통(스태틱) 처리용

    [Header("방 영상들")]
    public Texture[] roomFeeds;
    public int currentRoom = 0;

    [Header("창고 카메라 (CAM07 — 화면 안 보임, 소리만)")]
    public int storageRoomIndex = 6;   // roomFeeds 기준 [6]=창고

    [Header("스태틱 설정")]
    public int staticSize = 128;
    public float staticInterval = 0.05f;

    [Header("전환 스태틱")]
    public float switchStaticTime = 0.25f;

    private Texture2D staticTex;
    private Material monitorMat;
    private Color32[] pixels;
    private float staticTimer = 0f;
    private float switchTimer = 0f;

    void Start()
    {
        monitorMat = GetComponent<Renderer>().material;
        staticTex = new Texture2D(staticSize, staticSize);
        staticTex.filterMode = FilterMode.Point;
        pixels = new Color32[staticSize * staticSize];

        if (cctv == null)
            cctv = FindAnyObjectByType<CCTVController>();

        ApplyButtonState();  // 시작 시 버튼 상태 반영
    }

    void Update()
    {
        // 카메라 시스템 고장 = 완전 먹통. 방/상태 무관하게 항상 스태틱만.
        if (cameraSystem != null && cameraSystem.IsCameraBroken())
        {
            ShowStatic();
            return;
        }

        // === 1. 상태 결정 ===
        UpdateState();

        // === 2. 상태별 화면 처리 ===
        switch (state)
        {
            case State.Off:
            case State.Switching:
                ShowStatic();               // 둘 다 스태틱 표시
                break;
            case State.Active:
                // 창고(CAM07)는 언제나 화면이 안 보임 — 소리만(Phase 8 사운드 연결 예정)
                if (currentRoom == storageRoomIndex)
                    ShowStatic();
                else
                    ShowRoom();
                break;
        }
    }

    // 현재 상황에 따라 상태 전환
    void UpdateState()
    {
        bool camOn = (cctv != null && cctv.isCameraDown);

        if (!camOn)
        {
            // CCTV 꺼짐 → 무조건 OFF
            SetState(State.Off);
            return;
        }

        // CCTV 켜진 상태
        if (state == State.Switching)
        {
            // 전환 스태틱 시간 소진 중
            switchTimer -= Time.deltaTime;
            if (switchTimer <= 0f)
                SetState(State.Active);     // 스태틱 끝 → 방 영상 + 버튼 활성
        }
        else if (state == State.Off)
        {
            // 방금 CCTV 켜짐 → 전환 스태틱부터 시작 (첫 진입도 스태틱 한 번)
            StartSwitch();
        }
        // Active면 그대로 유지 (방 바꿀 때 SwitchRoom이 Switching으로 보냄)
    }

    // 상태 변경 + 버튼 상태 반영
    void SetState(State next)
    {
        if (state == next) return;
        state = next;
        ApplyButtonState();
    }

    // 상태에 따라 버튼 표시/클릭 설정 (지금은 즉시, 나중에 alpha를 Lerp로)
    void ApplyButtonState()
    {
        if (buttonGroup == null) return;

        switch (state)
        {
            case State.Off:
                buttonGroup.alpha = 0f;
                buttonGroup.interactable = false;
                buttonGroup.blocksRaycasts = false;
                break;
            case State.Switching:
                buttonGroup.alpha = 1f;             // 보이지만
                buttonGroup.interactable = false;   // 클릭 잠금 (스태틱 중)
                buttonGroup.blocksRaycasts = false;
                break;
            case State.Active:
                buttonGroup.alpha = 1f;
                buttonGroup.interactable = true;    // 클릭 가능
                buttonGroup.blocksRaycasts = true;
                break;
        }
    }

    // 방 전환 요청 (버튼이 호출)
    public void SwitchRoom(int roomIndex)
    {
        if (roomIndex < 0 || roomIndex >= roomFeeds.Length) return;
        if (roomIndex == currentRoom && state == State.Active) return;
        currentRoom = roomIndex;
        StartSwitch();
    }

    // 귀신 이동 시 짧은 스태틱 (외부에서 호출)
    public void GhostMoveStatic()
    {
        // CCTV 켜져 있을 때만 (꺼져있으면 어차피 스태틱)
        if (state == State.Active)
            StartSwitch();
    }

    // 전환 스태틱 시작
    void StartSwitch()
    {
        switchTimer = switchStaticTime;
        SetState(State.Switching);
    }

    // === 화면 표시 ===
    void ShowRoom()
    {
        if (roomFeeds != null && roomFeeds.Length > 0)
            monitorMat.mainTexture = roomFeeds[currentRoom];
    }

    void ShowStatic()
    {
        staticTimer += Time.deltaTime;
        if (staticTimer >= staticInterval)
        {
            GenerateStatic();
            staticTimer = 0f;
        }
        monitorMat.mainTexture = staticTex;
    }

    void GenerateStatic()
    {
        for (int y = 0; y < staticSize; y++)
        {
            int rowBase = Random.Range(0, 256);
            for (int x = 0; x < staticSize; x++)
            {
                int v = Mathf.Clamp(rowBase + Random.Range(-40, 40), 0, 255);
                byte b = (byte)v;
                pixels[y * staticSize + x] = new Color32(b, b, b, 255);
            }
        }
        staticTex.SetPixels32(pixels);
        staticTex.Apply();
    }

    public void RingBellCurrentRoom()
    {
        if (yeonho != null)
            yeonho.RingBell(currentRoom);
    }
}
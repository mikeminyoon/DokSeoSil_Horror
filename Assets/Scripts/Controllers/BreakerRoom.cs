using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// 전기실 미니게임 (시스터 로케이션 브레이커 룸식) — 구현노트 §15.26~27, 기획서 §6
//
// [1단계 — 지금] 3일차판
//   - 조작: 마우스를 화면 아래로 "새로" 내리면 패널 올림/내림 (CCTV와 같은 조작)
//           방을 볼 땐 경비실처럼 시선이 마우스를 따라 살짝 움직임 (유저 11-27 — 양끝 회전은 불편해서 바꿈)
//           패널은 패널 쪽(panelYaw ± panelOpenTolerance)을 보고 있을 때만 열림 → 윤석 쪽을 보는 중엔 못 엶 (유저 11-27)
//   - 패널 올림 = 카메라가 브레이커 상자 앞으로(panelView) / 내림 = 방 전경(roomView)
//   - 패널이 다 올라온 동안 브레이커 버튼을 꾹 누르고 있으면 수리 (holdTime초). 떼면 거기서 멈춤(진행은 남음)
//   - 브레이커 전부 수리해야 "나가기" 버튼이 켜짐 (유저: 전부 고쳐야 전기 돌아오고 나갈 수 있음)
//   - 방은 어둡고, 가끔 스파크가 번쩍 → 그 순간에만 윤석이 보임 (지금은 뒤에 서 있기만 — 3일차판)
// [2단계 — TODO] 4일차~: 위험도 게이지(수리 중 상승·패널 내리면 하락) / 윤석 4단계 접근 / 달래기(스페이스) /
//   위험도 최대 + 마지막 단계에서 패널 내리면 즉사 (기획서 §6.3)
//
// 창고런(StorageRun)이 Begin/End를 부름. 버튼 OnClick·EventTrigger는 연결 안 해도 됨 (코드가 붙임).
public class BreakerRoom : MonoBehaviour
{
    [Header("시점 (빈 오브젝트 — 위치 + 바라보는 방향)")]
    [Tooltip("패널 내렸을 때 눈 (방 전경)")]
    public Transform roomView;
    [Tooltip("패널 올렸을 때 눈 (브레이커 상자 바로 앞)")]
    public Transform panelView;
    public float moveSpeed = 6f;
    [Tooltip("마우스 Y가 화면 아래 이 비율 안으로 새로 들어오면 패널 올림/내림 (CCTV와 같은 값)")]
    public float bottomZone = 0.12f;
    public float fullyInPosDist = 0.03f;   // 패널 앞에 "다 도착" 판정 (이때부터 버튼 눌림)
    public float fullyInRotDeg = 1f;

    [Header("방 시선 (경비실처럼 마우스를 따라 살짝)")]
    [Tooltip("마우스를 화면 왼쪽·오른쪽 끝까지 옮기면 Room View 정면에서 이만큼(도) 돎")]
    public float gazeYawRange = 35f;
    [Tooltip("마우스를 화면 위·아래 끝까지 옮기면 이만큼(도) 위아래로 봄")]
    public float gazePitchRange = 8f;
    [Tooltip("시선이 마우스를 따라가는 빠르기 (경비실 rotateSpeed와 같은 뜻)")]
    public float gazeFollowSpeed = 8f;

    [Header("패널 여는 방향 (이쪽을 볼 때만 패널이 열림)")]
    [Tooltip("패널이 있는 방향 — Room View 정면 기준 좌우 각도(도). 아래 '지금 시선 각도'를 보고 맞추면 됨")]
    public float panelYaw = 0f;
    [Tooltip("panelYaw에서 좌우로 이만큼(도)까지는 열림. 벗어나면(윤석 쪽 등) 안 열림")]
    public float panelOpenTolerance = 12f;

    [Header("브레이커 패널 (World 캔버스)")]
    [Tooltip("브레이커 캔버스의 Canvas Group — 패널이 다 올라왔을 때만 눌림")]
    public CanvasGroup panelGroup;
    [Tooltip("비우면 Canvas Group 아래에서 이름이 Breaker로 시작하는 버튼을 이름순으로 자동으로 씀. 각 버튼의 자식 Image \"Fill\" = 진행 막대")]
    public Button[] breakers;
    [Tooltip("비우면 Canvas Group 아래에서 이름에 Exit가 들어간 버튼을 씀")]
    public Button exitButton;
    [Tooltip("브레이커 하나 수리에 누르고 있어야 하는 시간(초)")]
    public float holdTime = 3f;
    public Color doneColor = new Color(0.35f, 1f, 0.45f);

    [Header("스파크 + 윤석 (1단계: 뒤에 서 있기만)")]
    [Tooltip("번쩍이는 조명 (Light 컴포넌트는 꺼둔 채로 — 코드가 켰다 끔)")]
    public Light sparkLight;
    [Tooltip("윤석 (모델). 스파크 때만 보임")]
    public GameObject yunseok;
    public float sparkGapMin = 2.5f;       // 스파크 사이 간격 (랜덤)
    public float sparkGapMax = 6f;
    public float sparkFlashMin = 0.06f;    // 한 번 번쩍이는 시간
    public float sparkFlashMax = 0.15f;
    [Range(0f, 1f)] public float doubleFlashChance = 0.4f;   // 두 번 연달아 번쩍일 확률

    [Header("상태 (확인용)")]
    public bool active = false;
    public bool panelUp = false;
    public int repairedCount = 0;
    [Tooltip("지금 시선 좌우 각도 — 플레이 중 패널을 바라보며 이 숫자를 적어뒀다가 Panel Yaw에 넣으면 됨")]
    public float currentGazeYaw = 0f;
    public bool facingPanel = false;      // 지금 패널 쪽을 보고 있나 (열 수 있나)

    // 브레이커가 하나도 없으면(설정 실수) 바로 나갈 수 있게 — 갇히는 것보다 나음 (경고 로그)
    public bool AllRepaired => breakers == null || breakers.Length == 0 || repairedCount >= breakers.Length;

    private StorageRun owner;
    private Transform camT;
    private bool setupDone = false;
    private bool wasInBottom = false;
    private bool fullyIn = false;
    private float gazeYaw = 0f;              // 시선 (Room View 정면 기준, -왼쪽 / +오른쪽)
    private float gazePitch = 0f;            // 시선 위아래 (+아래)
    private bool loggedNotFacing = false;

    private float[] progress;
    private bool[] done;
    private Image[] fills;
    private Color[] fillColors;
    private BreakerHoldButton[] holds;

    private Renderer[] yunseokRenderers;
    private float sparkTimer = 0f;
    private float flashLeft = 0f;
    private int extraFlashes = 0;

    void Start()
    {
        Setup();
        HideAll();
    }

    // 버튼 찾기 + 누르기 판정 붙이기 (한 번만)
    void Setup()
    {
        if (setupDone) return;
        setupDone = true;

        if ((breakers == null || breakers.Length == 0) && panelGroup != null)
        {
            var list = new List<Button>();
            foreach (var b in panelGroup.GetComponentsInChildren<Button>(true))
                if (b.name.StartsWith("Breaker", System.StringComparison.OrdinalIgnoreCase)) list.Add(b);
            list.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
            breakers = list.ToArray();
        }
        if (exitButton == null && panelGroup != null)
        {
            foreach (var b in panelGroup.GetComponentsInChildren<Button>(true))
                if (b.name.IndexOf("Exit", System.StringComparison.OrdinalIgnoreCase) >= 0) { exitButton = b; break; }
        }

        int n = (breakers != null) ? breakers.Length : 0;
        progress = new float[n];
        done = new bool[n];
        fills = new Image[n];
        fillColors = new Color[n];
        holds = new BreakerHoldButton[n];
        for (int i = 0; i < n; i++)
        {
            if (breakers[i] == null) continue;
            holds[i] = breakers[i].GetComponent<BreakerHoldButton>();
            if (holds[i] == null) holds[i] = breakers[i].gameObject.AddComponent<BreakerHoldButton>();

            Transform f = FindChild(breakers[i].transform, "Fill");
            if (f != null) fills[i] = f.GetComponent<Image>();
            if (fills[i] != null)
            {
                fills[i].type = Image.Type.Filled;               // 혹시 Simple로 뒀어도 진행 막대로 보이게
                fills[i].fillMethod = Image.FillMethod.Horizontal;
                fillColors[i] = fills[i].color;
            }
            else
            {
                Debug.LogWarning($"전기실: {breakers[i].name}에 자식 Image \"Fill\"이 없어서 진행 막대가 안 보임 (수리는 됨)");
            }
        }

        if (exitButton != null) exitButton.onClick.AddListener(OnExitClicked);
        if (yunseok != null) yunseokRenderers = yunseok.GetComponentsInChildren<Renderer>(true);

        if (n == 0) Debug.LogWarning("전기실: 브레이커 버튼을 못 찾음 — Breakers 칸에 넣거나 이름을 Breaker1, Breaker2...로 (지금은 바로 나갈 수 있음)");
        if (exitButton == null) Debug.LogWarning("전기실: 나가기 버튼을 못 찾음 — Exit Button 칸에 넣어주세요 (못 나감!)");
        if (roomView == null || panelView == null) Debug.LogWarning("전기실: Room View / Panel View 칸이 비어 있음");
    }

    // === 창고런이 부름 ===

    public void Begin(StorageRun runOwner, Transform cameraTransform)
    {
        Setup();
        owner = runOwner;
        camT = cameraTransform;
        active = true;
        panelUp = false;
        fullyIn = false;
        gazeYaw = 0f;
        gazePitch = 0f;

        // 새 정전 = 처음부터 수리
        repairedCount = 0;
        for (int i = 0; i < progress.Length; i++)
        {
            progress[i] = 0f;
            done[i] = false;
            if (breakers[i] != null) breakers[i].interactable = true;
            if (fills[i] != null) { fills[i].fillAmount = 0f; fills[i].color = fillColors[i]; }
            if (holds[i] != null) holds[i].Release();
        }

        // 페이드로 가려진 순간이라 바로 이동
        if (camT != null && roomView != null) camT.SetPositionAndRotation(roomView.position, roomView.rotation);
        wasInBottom = MouseInBottom();   // 들어오자마자 패널이 확 올라가지 않게 (아래에 있었으면 한 번 빼야 함)

        sparkTimer = Random.Range(sparkGapMin, sparkGapMax);
        flashLeft = 0f;
        extraFlashes = 0;
        SetSpark(false);
        ApplyPanel();
        Debug.Log($"★ 전기실 — 브레이커 {progress.Length}개 수리 (마우스를 화면 아래로 = 패널 올림/내림, 버튼 꾹 누르기)");
    }

    public void End()
    {
        active = false;
        panelUp = false;
        fullyIn = false;
        HideAll();
    }

    void HideAll()
    {
        if (holds != null) foreach (var h in holds) if (h != null) h.Release();
        SetSpark(false);
        if (panelGroup != null)
        {
            panelGroup.interactable = false;
            panelGroup.blocksRaycasts = false;
        }
    }

    void Update()
    {
        if (!active || camT == null) return;

        // 방을 볼 때만 시선이 마우스를 따라감 (패널 올린 동안은 그대로 둠)
        if (!panelUp) UpdateGaze();
        facingPanel = Mathf.Abs(Mathf.DeltaAngle(gazeYaw, panelYaw)) <= panelOpenTolerance;

        // 패널 올림/내림 (아래 영역에 새로 들어온 순간만) — 여는 건 패널 쪽을 볼 때만, 닫는 건 언제나
        bool inBottom = MouseInBottom();
        if (inBottom && !wasInBottom)
        {
            if (panelUp)
            {
                panelUp = false;
            }
            else if (facingPanel)
            {
                panelUp = true;
                loggedNotFacing = false;
            }
            else if (!loggedNotFacing)
            {
                loggedNotFacing = true;
                Debug.Log($"전기실: 패널 쪽을 봐야 열림 (지금 시선 {gazeYaw:F0}°, 패널 {panelYaw:F0}° ±{panelOpenTolerance:F0}°)");
            }
        }
        wasInBottom = inBottom;

        // 카메라 이동 (패널 ↔ 방, Lerp)
        Transform target = panelUp ? panelView : roomView;
        if (target != null)
        {
            Quaternion targetRot = panelUp ? target.rotation : target.rotation * Quaternion.Euler(gazePitch, gazeYaw, 0f);
            float rotSpeed = panelUp ? moveSpeed : gazeFollowSpeed;
            camT.position = Vector3.Lerp(camT.position, target.position, moveSpeed * Time.deltaTime);
            camT.rotation = Quaternion.Slerp(camT.rotation, targetRot, rotSpeed * Time.deltaTime);
            fullyIn = panelUp
                      && Vector3.Distance(camT.position, target.position) < fullyInPosDist
                      && Quaternion.Angle(camT.rotation, targetRot) < fullyInRotDeg;
        }
        else
        {
            fullyIn = panelUp;   // (시점 칸이 비었을 때 대비)
        }

        ApplyPanel();
        UpdateRepair();
        UpdateSpark();
        // TODO(2단계): 위험도·윤석 접근·달래기·즉사
    }

    // 경비실처럼: 마우스 위치(화면 가운데 = 0, 끝 = ±1)에 따라 시선이 살짝 따라감
    void UpdateGaze()
    {
        float mx = (Input.mousePosition.x / Mathf.Max(1, Screen.width)) * 2f - 1f;
        float my = (Input.mousePosition.y / Mathf.Max(1, Screen.height)) * 2f - 1f;
        mx = Mathf.Clamp(mx, -1f, 1f);
        my = Mathf.Clamp(my, -1f, 1f);
        gazeYaw = mx * gazeYawRange;          // 마우스 오른쪽 = 오른쪽을 봄
        gazePitch = -my * gazePitchRange;     // 마우스 위 = 위를 봄
        currentGazeYaw = gazeYaw;
    }

    // 패널이 다 올라왔을 때만 버튼이 눌림
    void ApplyPanel()
    {
        if (panelGroup != null)
        {
            panelGroup.interactable = fullyIn;
            panelGroup.blocksRaycasts = fullyIn;
        }
        if (!fullyIn && holds != null)
            foreach (var h in holds) if (h != null) h.Release();
        if (exitButton != null) exitButton.interactable = AllRepaired;
    }

    void UpdateRepair()
    {
        if (!fullyIn) return;
        for (int i = 0; i < progress.Length; i++)
        {
            if (done[i] || holds[i] == null || !holds[i].IsHeld) continue;

            progress[i] += Time.deltaTime / Mathf.Max(0.1f, holdTime);
            if (fills[i] != null) fills[i].fillAmount = Mathf.Clamp01(progress[i]);

            if (progress[i] >= 1f)
            {
                done[i] = true;
                repairedCount++;
                holds[i].Release();
                if (breakers[i] != null) breakers[i].interactable = false;
                if (fills[i] != null) fills[i].color = doneColor;
                Debug.Log($"★ 브레이커 수리 ({repairedCount}/{progress.Length}) — {breakers[i].name}");
                // TODO: 브레이커 켜지는 소리 (기획서 K3, Phase 8)
                if (AllRepaired) Debug.Log("★★ 브레이커 전부 수리 — 나가기 버튼 켜짐");
            }
        }
    }

    // 스파크: 가끔 번쩍(가끔 두 번 연달아) — 그 순간에만 윤석이 보임
    void UpdateSpark()
    {
        if (flashLeft > 0f)
        {
            flashLeft -= Time.deltaTime;
            if (flashLeft <= 0f)
            {
                SetSpark(false);
                if (extraFlashes > 0)
                {
                    extraFlashes--;
                    sparkTimer = Random.Range(0.05f, 0.12f);   // 바로 한 번 더
                }
                else
                {
                    sparkTimer = Random.Range(sparkGapMin, sparkGapMax);
                    extraFlashes = (Random.value < doubleFlashChance) ? 1 : 0;
                }
            }
            return;
        }

        sparkTimer -= Time.deltaTime;
        if (sparkTimer <= 0f)
        {
            SetSpark(true);
            flashLeft = Random.Range(sparkFlashMin, sparkFlashMax);
            // TODO: 스파크 소리 (기획서 K2, Phase 8)
        }
    }

    // 스파크 조명 + 윤석 모습 (켜고 끄기만)
    void SetSpark(bool on)
    {
        if (sparkLight != null) sparkLight.enabled = on;
        if (yunseokRenderers != null)
            foreach (var r in yunseokRenderers) if (r != null) r.enabled = on;
    }

    void OnExitClicked()
    {
        if (!active || !AllRepaired) return;
        if (owner != null) owner.RequestExit();
    }

    bool MouseInBottom()
    {
        return (Input.mousePosition.y / Mathf.Max(1, Screen.height)) < bottomZone;
    }

    static Transform FindChild(Transform parent, string childName)
    {
        foreach (Transform c in parent)
        {
            if (c.name == childName) return c;
            var deeper = FindChild(c, childName);
            if (deeper != null) return deeper;
        }
        return null;
    }
}

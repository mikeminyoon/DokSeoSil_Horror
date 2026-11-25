using UnityEngine;
using TMPro;

// 리셋 패널 메뉴 표시 — FNAF3 "system restart menu" 식 (구현노트 §15.16)
//
//   >>> audio devices   error        ← 마우스 올린 줄 앞에 >>> / 고장이면 빨간 error 깜빡
//       camera system   rebooting....← 개별 리셋 중이면 rebooting.... 깜빡 (줄 글자색)
//       ventilation
//       reboot all      rebooting..  ← 전체 리셋 중이면 여기만 깜빡 (위 세 줄은 조용히)
//       exit
//
// ResetPanelWorldCanvas에 붙임. 줄은 이름으로 자동으로 찾음:
//   줄 오브젝트 이름: AudioReset / CameraReset / VentReset / ResetAll / Exit
//   줄 안의 글자 이름: ">>" (커서), "Error" (상태), "Label" (항목 이름), "Reboot" (reboot all 줄의 rebooting — 글자는 씬에 적은 그대로)
// 클릭 자체는 각 줄의 Button(OnClick)이 그대로 처리 — 이 스크립트는 보여주기만 함.
public class ResetPanelMenu : MonoBehaviour
{
    [Header("깜빡임")]
    public float blinkPeriod = 0.8f;          // 켜짐+꺼짐 한 번 도는 시간(초)

    [Header("상태 글자")]
    public string errorText = "error";
    public string rebootingText = "rebooting....";
    [Tooltip("켜두면 씬에 칠해둔 색을 그대로 씀: error = Error 글자 색(빨강), rebooting = Label 글자 색(초록). 끄면 아래 두 색 사용")]
    public bool useSceneColors = true;
    public Color errorColor = new Color(0.88f, 0f, 0.11f);
    public Color rebootingColor = new Color(0.03f, 0.69f, 0f);

    [Header("빛 번짐 재질 (비워두면 씬에 입혀둔 재질을 그대로 씀)")]
    [Tooltip("error일 때 — 비우면 Error 글자에 입혀둔 재질(빨간 번짐 프리셋)")]
    public Material errorMaterial;
    [Tooltip("rebooting일 때 — 비우면 Label 글자에 입혀둔 재질(초록 번짐)")]
    public Material rebootingMaterial;

    [Header("참조 (비워두면 자동으로 찾음)")]
    public PanelController panel;
    public AudioSystem audioSystem;
    public CameraSystem cameraSystem;
    public VentilationSystem ventSystem;

    // 줄 하나의 부품
    class Row
    {
        public TMP_Text cursor;               // >>>
        public TMP_Text status;               // error / rebooting.... (시스템 줄만)
        public ResetPanelRowHover hover;
        public System.Func<bool> isBroken;    // 시스템 줄만
        public System.Func<bool> isResetting;
    }
    private Row[] rows;
    private TMP_Text rebootAllText;   // reboot all 줄 옆 "rebooting.." (전체 리셋 중에만 깜빡)

    void Start()
    {
        if (panel == null) panel = FindAnyObjectByType<PanelController>();
        if (audioSystem == null) audioSystem = FindAnyObjectByType<AudioSystem>();
        if (cameraSystem == null) cameraSystem = FindAnyObjectByType<CameraSystem>();
        if (ventSystem == null) ventSystem = FindAnyObjectByType<VentilationSystem>();

        rows = new Row[]
        {
            MakeRow("AudioReset",  () => audioSystem != null && audioSystem.IsAudioBroken(),
                                   () => audioSystem != null && audioSystem.IsResetting()),
            MakeRow("CameraReset", () => cameraSystem != null && cameraSystem.IsCameraBroken(),
                                   () => cameraSystem != null && cameraSystem.IsResetting()),
            MakeRow("VentReset",   () => ventSystem != null && ventSystem.IsVentBroken(),
                                   () => ventSystem != null && ventSystem.IsResetting()),
            MakeRow("ResetAll", null, null),
            MakeRow("Exit",     null, null),
        };

        Transform allRow = FindChildDeep(transform, "ResetAll");
        if (allRow != null) rebootAllText = FindText(allRow, "Reboot");
        if (rebootAllText != null)
        {
            rebootAllText.raycastTarget = false;
            SetVisible(rebootAllText, false);
        }
    }

    Row MakeRow(string rowName, System.Func<bool> isBroken, System.Func<bool> isResetting)
    {
        var row = new Row { isBroken = isBroken, isResetting = isResetting };
        Transform rowT = FindChildDeep(transform, rowName);
        if (rowT == null)
        {
            Debug.LogWarning($"리셋 패널 메뉴: '{rowName}' 줄을 못 찾음");
            return row;
        }

        row.cursor = FindText(rowT, ">>");
        row.status = FindText(rowT, "Error");

        // 마우스 올림 감지는 줄의 Button 오브젝트에 (없으면 줄 자체에)
        var button = rowT.GetComponentInChildren<UnityEngine.UI.Button>(true);
        GameObject hoverTarget = (button != null) ? button.gameObject : rowT.gameObject;
        row.hover = hoverTarget.GetComponent<ResetPanelRowHover>();
        if (row.hover == null) row.hover = hoverTarget.AddComponent<ResetPanelRowHover>();

        // >>>·error 글자는 클릭 판정에서 뺌 (깜빡일 때 마우스 올림이 흔들리지 않게 — 클릭은 줄 전체가 받음)
        if (row.cursor != null) row.cursor.raycastTarget = false;
        if (row.status != null)
        {
            row.status.raycastTarget = false;
            if (useSceneColors) errorColor = row.status.color;   // 씬의 빨강 그대로
            if (errorMaterial == null) errorMaterial = row.status.fontSharedMaterial;   // Error 글자 재질(빨간 번짐)
        }
        var label = FindText(rowT, "Label");
        if (label != null)
        {
            if (useSceneColors) rebootingColor = label.color;      // 줄 글자 색 그대로
            if (rebootingMaterial == null) rebootingMaterial = label.fontSharedMaterial;   // 줄 글자 재질(초록 번짐)
        }

        SetVisible(row.cursor, false);
        SetVisible(row.status, false);
        return row;
    }

    void Update()
    {
        if (rows == null) return;

        bool open = panel == null || panel.isPanelOpen;
        bool blinkOn = Mathf.Repeat(Time.unscaledTime, blinkPeriod) < blinkPeriod * 0.5f;
        bool resettingAll = panel != null && panel.IsResettingAll;

        // 전체 리셋 중: reboot all 옆에만 rebooting 깜빡
        SetVisible(rebootAllText, resettingAll && blinkOn);

        foreach (var row in rows)
        {
            // >>> 커서: 패널 열려 있고 마우스 올린 줄만
            SetVisible(row.cursor, open && row.hover != null && row.hover.isHovered);

            // 상태: (전체 리셋 중이면 조용히) > 개별 리셋 중 > 고장 > 정상 순으로
            if (row.status == null || row.isBroken == null) continue;

            if (resettingAll)
            {
                SetVisible(row.status, false);
            }
            else if (row.isResetting())
            {
                row.status.text = rebootingText;
                row.status.color = rebootingColor;
                SetMaterial(row.status, rebootingMaterial);
                SetVisible(row.status, blinkOn);
            }
            else if (row.isBroken())
            {
                row.status.text = errorText;
                row.status.color = errorColor;
                SetMaterial(row.status, errorMaterial);
                SetVisible(row.status, blinkOn);
            }
            else
            {
                SetVisible(row.status, false);
            }
        }
    }

    // --- 도우미 ---
    static void SetVisible(TMP_Text t, bool on)
    {
        if (t != null && t.enabled != on) t.enabled = on;
    }

    // 재질(빛 번짐 색) 바꾸기 — 같은 폰트 아틀라스를 쓰는 프리셋끼리만 (바뀔 때만 적용)
    static void SetMaterial(TMP_Text t, Material m)
    {
        if (t != null && m != null && t.fontSharedMaterial != m) t.fontSharedMaterial = m;
    }

    static TMP_Text FindText(Transform root, string name)
    {
        Transform t = FindChildDeep(root, name);
        return t != null ? t.GetComponent<TMP_Text>() : null;
    }

    static Transform FindChildDeep(Transform root, string name)
    {
        foreach (Transform child in root)
        {
            if (child.name == name) return child;
            Transform found = FindChildDeep(child, name);
            if (found != null) return found;
        }
        return null;
    }
}

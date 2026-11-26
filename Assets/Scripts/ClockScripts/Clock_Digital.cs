using UnityEngine;

// 디지털 시계 (에셋 개조): 게임 시간(GameManager)을 텍스처 오프셋으로 표시.
// 원본: Andre "AEG" Bürger / VIS-Games (2021) — 현실시간 → 게임시간으로 개조, URP 전용.
//
// [분 고장] 현우 1스택부터 (구현노트 §15.22, 유저 설계) — 고장난 시계처럼 보이게
//   당한 순간의 "분"에서 분 숫자가 멈춤. 시는 계속 감 (2:37에 당하면 3시엔 3:37, 4시엔 4:37).
//   → 시는 알 수 있고 분만 엉터리. 콜론(:)은 계속 깜빡 (살아있는데 숫자만 안 넘어감).
//   2스택에 또 당해도 처음 멈춘 분 그대로. 배선 고장 복구 때 같이 고쳐짐 (조명처럼).
//   ⚠️ 표시만 고장 — 게임 시간(GameManager)은 그대로 흐름. 시간 판정은 전부 GameManager를 읽음.
public class Clock_Digital : MonoBehaviour
{
    [Header("분 고장 (확인용)")]
    public bool minuteFrozen = false;   // 분 숫자 멈춤 중 (현우 1스택~)
    public int frozenMinute = 0;        // 멈춘 분

    Renderer objRenderer;
    float tickDelay;
    bool tick;
    bool poweredOff = false;   // 전기 나감(방전 암전 / 배선 고장) — 숫자 꺼짐 (게임 시간 자체는 GameManager에서 계속 흐름)
    Color[] savedBase = new Color[6];       // 꺼지기 전 원래 색 (배선 복구 때 되돌림)
    Color[] savedEmission = new Color[6];

    void Start()
    {
        objRenderer = GetComponent<Renderer>();
    }

    // === 분 고장 (현우 스택) ===

    // 지금 분에서 멈춤. 이미 멈췄으면 처음 멈춘 분 그대로 (2스택에 또 당해도 안 바뀜)
    public void FreezeMinute()
    {
        if (minuteFrozen || GameManager.Instance == null) return;
        minuteFrozen = true;
        frozenMinute = GameManager.Instance.gameMinute;
        Debug.Log($"★ 시계 분 고장 — {frozenMinute:00}분에서 멈춤 (시는 계속 감)");
    }

    // 분 고장 수리 (배선 고장 복구 때)
    public void UnfreezeMinute()
    {
        if (!minuteFrozen) return;
        minuteFrozen = false;
        Debug.Log("시계 분 고장 수리됨");
    }

    // 씬의 디지털 시계 전부 분 고장 (현우가 부름 — 시계 참조 칸 없이)
    public static void FreezeMinuteAll()
    {
        foreach (var c in FindObjectsByType<Clock_Digital>())
            c.FreezeMinute();
    }

    // 전기 나감: 숫자·콜론(materials[1~5])을 검게 + 발광 끔 → 시계가 꺼진 것처럼
    public void PowerOff()
    {
        if (poweredOff) return;   // 이미 꺼짐 (배선 고장 중 방전 등) — 검은색을 원래 색으로 저장하면 안 됨
        poweredOff = true;
        if (objRenderer == null) objRenderer = GetComponent<Renderer>();
        var mats = objRenderer.materials;
        for (int i = 1; i <= 5 && i < mats.Length; i++)
        {
            savedBase[i] = mats[i].GetColor("_BaseColor");
            savedEmission[i] = mats[i].GetColor("_EmissionColor");
            mats[i].SetColor("_BaseColor", Color.black);
            mats[i].SetColor("_EmissionColor", Color.black);
        }
    }

    // 전기 복구 (배선 고장 수리): 원래 색으로
    public void PowerOn()
    {
        if (!poweredOff) return;
        poweredOff = false;
        if (objRenderer == null) objRenderer = GetComponent<Renderer>();
        var mats = objRenderer.materials;
        for (int i = 1; i <= 5 && i < mats.Length; i++)
        {
            mats[i].SetColor("_BaseColor", savedBase[i]);
            mats[i].SetColor("_EmissionColor", savedEmission[i]);
        }
    }

    void Update()
    {
        if (poweredOff) return;
        // GameManager 없으면 아무것도 안 함
        if (GameManager.Instance == null) return;

        int hour = GameManager.Instance.gameHour;      // 게임 시 (12→6)
        int minute = GameManager.Instance.gameMinute;  // 게임 분 (0~59)
        if (minuteFrozen) minute = frozenMinute;       // 분 고장: 분만 멈춤, 시는 그대로 흐름

        // --- 분 1의 자리 (materials[4]) ---
        float offset = 0.0f - 0.1f * (float)(minute % 10);
        objRenderer.materials[4].SetTextureOffset("_BaseMap", new Vector2(0.0f, offset));
        objRenderer.materials[4].SetTextureOffset("_EmissionMap", new Vector2(0.0f, offset));

        // --- 분 10의 자리 (materials[3]) ---
        offset = 0.0f - 0.1f * (float)((minute / 10) % 10);
        objRenderer.materials[3].SetTextureOffset("_BaseMap", new Vector2(0.0f, offset));
        objRenderer.materials[3].SetTextureOffset("_EmissionMap", new Vector2(0.0f, offset));

        // --- 시 1의 자리 (materials[1]) ---
        offset = 0.0f - 0.1f * (float)(hour % 10);
        objRenderer.materials[1].SetTextureOffset("_BaseMap", new Vector2(0.0f, offset));
        objRenderer.materials[1].SetTextureOffset("_EmissionMap", new Vector2(0.0f, offset));

        // --- 시 10의 자리 (materials[2]) ---
        offset = 0.0f - 0.1f * (float)((hour / 10) % 10);
        objRenderer.materials[2].SetTextureOffset("_BaseMap", new Vector2(0.0f, offset));
        objRenderer.materials[2].SetTextureOffset("_EmissionMap", new Vector2(0.0f, offset));

        // --- 콜론(:) 깜빡임 (materials[5]) ---
        tickDelay -= Time.deltaTime;
        if (tickDelay < 0.0f)
        {
            tickDelay += 0.5f;
            tick = !tick;
            float colonOffset = tick ? 0.0f : 0.9f;   // 켜짐/꺼짐
            objRenderer.materials[5].SetTextureOffset("_BaseMap", new Vector2(0.0f, colonOffset));
            objRenderer.materials[5].SetTextureOffset("_EmissionMap", new Vector2(0.0f, colonOffset));
        }
    }
}
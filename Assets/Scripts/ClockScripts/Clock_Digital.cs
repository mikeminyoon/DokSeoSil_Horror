using UnityEngine;

// 디지털 시계 (에셋 개조): 게임 시간(GameManager)을 텍스처 오프셋으로 표시.
// 원본: Andre "AEG" Bürger / VIS-Games (2021) — 현실시간 → 게임시간으로 개조, URP 전용.
public class Clock_Digital : MonoBehaviour
{
    Renderer objRenderer;
    float tickDelay;
    bool tick;

    void Start()
    {
        objRenderer = GetComponent<Renderer>();
    }

    void Update()
    {
        // GameManager 없으면 아무것도 안 함
        if (GameManager.Instance == null) return;

        int hour = GameManager.Instance.gameHour;      // 게임 시 (12→6)
        int minute = GameManager.Instance.gameMinute;  // 게임 분 (0~59)

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
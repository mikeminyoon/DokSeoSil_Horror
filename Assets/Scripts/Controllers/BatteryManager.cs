using UnityEngine;

// 배터리 매니저: 공통 전력. 장치들이 소모 요청.
// 0 되면 방전 → 게임오버. 빈 오브젝트에 붙인다.
public class BatteryManager : MonoBehaviour
{
    public static BatteryManager Instance;

    [Header("배터리")]
    public float battery = 100f;           // 현재 잔량 (%)
    public float maxBattery = 100f;

    [Header("자연 감소")]
    public float naturalDrain = 0.2f;      // 초당 자연 감소 (적게)

    [Header("상태")]
    public bool isDead = false;            // 방전됨?

    void Awake()
    {
        Instance = this;
    }

    void Update()
    {
        if (isDead) return;

        // 자연 감소
        battery -= naturalDrain * Time.deltaTime;

        if (battery <= 0f)
        {
            battery = 0f;
            Deplete();
        }

        if (Mathf.FloorToInt(battery) != Mathf.FloorToInt(battery + naturalDrain * Time.deltaTime))
            Debug.Log($"배터리: {battery:F0}%");
    }

    // 즉시 소모 (종 칠 때 등 한 번에)
    public void Drain(float amount)
    {
        if (isDead) return;
        battery -= amount;
        if (battery < 0f) battery = 0f;
    }

    // 지속 소모 (셔터 닫는 동안 등 — 매 프레임 호출)
    public void DrainPerSecond(float rate)
    {
        if (isDead) return;
        battery -= rate * Time.deltaTime;
        if (battery < 0f) battery = 0f;
    }

    // 방전 → 게임오버
    void Deplete()
    {
        isDead = true;
        Debug.Log("★★★ 배터리 방전! 암전 → 게임오버");
        // TODO: 암전 연출 → 연호 강제 점프스케어 (연호 만들면)
        if (GameManager.Instance != null)
            GameManager.Instance.GameOver("배터리 방전");
    }

    // 잔량 비율 (0~1) — UI용
    public float GetRatio()
    {
        return battery / maxBattery;
    }
}
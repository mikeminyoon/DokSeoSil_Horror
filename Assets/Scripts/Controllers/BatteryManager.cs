using UnityEngine;

// 배터리 매니저: 공통 전력. 장치들이 소모 요청.
// 0 되면 방전 → 암전 시퀀스(BatteryBlackout). 빈 오브젝트에 붙인다.
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

        //if (Mathf.FloorToInt(battery) != Mathf.FloorToInt(battery + naturalDrain * Time.deltaTime))
            //Debug.Log($"배터리: {battery:F0}%");
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

    // 방전 → 암전 시퀀스 (조용한 암전 → 오르골 → 어둠 → 연호 점프스케어, 6시 오면 생존)
    // 게임오버는 여기서 안 함 — 밤 타이머가 계속 흘러야 6시 생존이 가능해서. BatteryBlackout가 처리.
    void Deplete()
    {
        isDead = true;

        var outage = FindAnyObjectByType<BatteryBlackout>();
        if (outage == null)
        {
            // 씬에 없으면 자동으로 붙임 (기본값으로 작동). Inspector에서 조절하려면 이 오브젝트에 BatteryBlackout를 미리 붙여둘 것.
            outage = gameObject.AddComponent<BatteryBlackout>();
        }
        outage.Begin();
    }

    // 잔량 비율 (0~1) — UI용
    public float GetRatio()
    {
        return battery / maxBattery;
    }
}
using UnityEngine;
using TMPro;

// CCTV 모니터에 배터리 % 표시 (World Space).
// CCTV 켜졌을 때만 보임. 배터리 텍스트 오브젝트에 붙인다.
public class BatteryDisplay : MonoBehaviour
{
    public TMP_Text batteryText;      // 표시할 텍스트 (같은 오브젝트면 자동)
    public CCTVController cctv;        // CCTV 켜짐 확인

    void Start()
    {
        if (batteryText == null) batteryText = GetComponent<TMP_Text>();
        if (cctv == null) cctv = FindAnyObjectByType<CCTVController>();
    }

    void Update()
    {
        // CCTV 켜졌을 때만 표시
        bool cctvOn = (cctv != null) && cctv.isCameraDown;
        if (batteryText != null) batteryText.enabled = cctvOn;

        if (cctvOn && BatteryManager.Instance != null)
        {
            int percent = Mathf.CeilToInt(BatteryManager.Instance.battery);
            batteryText.text = $"{percent}%";
        }
    }
}
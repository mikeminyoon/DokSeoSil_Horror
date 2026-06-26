using UnityEngine;

// 관리실 시야 4구역 컨트롤러
// Player 오브젝트에 붙인다. 자식 카메라를 회전시킨다.
public class ViewController : MonoBehaviour
{
    // ===== 구역 정의 =====
    // 4구역의 기준 Y각도 (왼쪽, 가운데, 오른쪽, 후면 순서)
    // Inspector에서 방 크기 보고 직접 조절
    [Header("구역 기준 각도 (Y축)")]
    public float[] zoneAngles = { -60f, 0f, 60f, 180f };
    // 0=왼쪽, 1=가운데, 2=오른쪽, 3=후면

    [Header("구역 기준 상하각 (Pitch)")]
    [Tooltip("구역별 기본 상하 각도. +면 아래, -면 위를 봄")]
    public float[] zonePitch = { 0f, 0f, -20f, 25f };
    // 0=왼쪽(수평), 1=가운데(수평), 2=오른쪽/환풍구(-20=위 봄), 3=후면/창고통로(25=아래 봄)

    [Tooltip("시작 구역 (1 = 가운데)")]
    public int currentZone = 1;   // 현재 보고 있는 구역 번호

    // ===== 미세 추적 (구역 안에서 마우스 따라 까딱) =====
    [Header("미세 추적 범위")]
    public float microYaw = 12f;    // 좌우로 까딱이는 최대 각도
    public float microPitch = 7f;   // 상하로 까딱이는 최대 각도

    // ===== 구역 전환 =====
    [Header("구역 전환")]
    [Tooltip("마우스가 이 값(0~1)을 넘으면 가장자리로 판정")]
    public float edgeThreshold = 0.9f;
    [Tooltip("가장자리에 이 시간(초) 머물면 구역 전환")]
    public float edgeHoldTime = 0.25f;

    // ===== 회전 부드러움 =====
    [Header("회전 속도")]
    public float rotateSpeed = 8f;  // 클수록 빨리 따라감

    // ===== 내부 상태 =====
    private float edgeTimer = 0f;        // 가장자리에 머문 시간 누적
    private Camera cam;                  // 자식 카메라

    void Start()
    {
        // 자식에서 카메라 찾기
        cam = GetComponentInChildren<Camera>();
    }

    void Update()
    {
        // --- 1. 마우스 위치를 -1 ~ +1로 환산 ---
        // 화면 가운데=0, 왼끝=-1, 오른끝=+1 / 아래=-1, 위=+1
        float mx = (Input.mousePosition.x / Screen.width) * 2f - 1f;
        float my = (Input.mousePosition.y / Screen.height) * 2f - 1f;
        mx = Mathf.Clamp(mx, -1f, 1f);
        my = Mathf.Clamp(my, -1f, 1f);

        // --- 2. 구역 전환 체크 (좌우 끝에 머물기) ---
        HandleZoneSwitch(mx);

        // --- 3. 목표 각도 계산 ---
        // 현재 구역 기준각 + 마우스에 따른 미세 추적
        float targetYaw = zoneAngles[currentZone] + mx * microYaw;
        float targetPitch = zonePitch[currentZone] - my * microPitch;  // 마우스 위로=화면 위로 보려면 부호 반전

        // --- 4. 부드럽게 회전 ---
        Quaternion targetRot = Quaternion.Euler(targetPitch, targetYaw, 0f);
        transform.rotation = Quaternion.Slerp(
            transform.rotation, targetRot, rotateSpeed * Time.deltaTime);
    }

    // 좌우 가장자리에 일정 시간 머물면 옆 구역으로 전환
    void HandleZoneSwitch(float mx)
    {
        if (mx > edgeThreshold)          // 오른쪽 끝
        {
            edgeTimer += Time.deltaTime;
            if (edgeTimer >= edgeHoldTime)
            {
                // 다음 구역으로 (오른쪽). 후면(3)이 끝이라 더는 안 감
                if (currentZone < zoneAngles.Length - 1) currentZone++;
                edgeTimer = -0.1f;
            }
        }
        else if (mx < -edgeThreshold)    // 왼쪽 끝
        {
            edgeTimer += Time.deltaTime;
            if (edgeTimer >= edgeHoldTime)
            {
                // 이전 구역으로 (왼쪽). 왼쪽(0)이 끝
                if (currentZone > 0) currentZone--;
                edgeTimer = -0.1f;
            }
        }
        else
        {
            edgeTimer = 0f;  // 가운데로 돌아오면 타이머 리셋
        }
    }
}
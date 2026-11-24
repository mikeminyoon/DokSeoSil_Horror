using UnityEngine;

// 카메라 시스템: CCTV 과사용 처벌 (FNAF3 비디오 에러 이식, §12.1).
// CCTV가 켜져 있는 동안만 일정 주기로 확률 판정 → 걸리면 완전 먹통(스태틱).
// 종(오디오)은 이거랑 무관하게 계속 사용 가능. 리셋 패널에서 복구.
public class CameraSystem : MonoBehaviour
{
    [Header("참조")]
    public CCTVController cctv;

    [Header("고장 상태")]
    public bool isCameraBroken = false;

    [Header("확률 판정 (CCTV 켜져 있는 동안만 굴림)")]
    public float rollInterval = 3.5f;   // 몇 초마다 굴릴지
    public float breakChance = 0.03f;   // 굴릴 때마다 고장 확률

    [Header("리셋")]
    public float resetDuration = 5f;

    private float rollTimer = 0f;
    private float resetTimer = 0f;
    private bool isResetting = false;
    private float activeResetDuration = 5f;  // 이번 리셋에 실제 적용되는 시간(전체 리셋이면 더 길어짐)

    void Update()
    {
        // 리셋 진행 중 — 최우선
        if (isResetting)
        {
            resetTimer += Time.deltaTime;
            if (resetTimer >= activeResetDuration)
                CompleteReset();
            return;
        }

        if (isCameraBroken) return;   // 고장 상태로 대기 (플레이어가 리셋 눌러야 함)

        // CCTV 켜져 있을 때만 확률 굴림. 꺼지면 누적 리셋(다시 켜면 처음부터).
        if (cctv != null && cctv.isCameraDown)
        {
            rollTimer += Time.deltaTime;
            if (rollTimer >= rollInterval)
            {
                rollTimer = 0f;
                if (Random.value < breakChance)
                    BreakCamera();
            }
        }
        else
        {
            rollTimer = 0f;
        }
    }

    void BreakCamera()
    {
        if (isCameraBroken) return;
        isCameraBroken = true;
        Debug.Log("★★★ 카메라 시스템 고장! (CCTV 과사용) - 리셋 필요");
    }

    // 개별 리셋 (패널 버튼 OnClick이 호출) — 고장 안 났어도 예방적으로 실행 가능.
    // ⚠️ 버튼 OnClick은 매개변수 없는 함수만 찾으므로 여기엔 매개변수를 붙이지 말 것.
    public void StartReset()
    {
        StartResetFor(resetDuration);
    }

    // 시간 지정 리셋 (전체 리셋이 호출 — §12.3 "둘 8s")
    public void StartResetFor(float duration)
    {
        if (isResetting) return;
        isResetting = true;
        resetTimer = 0f;
        activeResetDuration = duration;
        Debug.Log($"카메라 리셋 시작... ({activeResetDuration}초)");
    }

    void CompleteReset()
    {
        isCameraBroken = false;
        isResetting = false;
        resetTimer = 0f;
        rollTimer = 0f;
        Debug.Log("카메라 리셋 완료!");
    }

    public bool IsResetting() { return isResetting; }
    public bool IsCameraBroken() { return isCameraBroken; }
}

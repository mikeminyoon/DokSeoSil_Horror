using UnityEngine;

// 오디오 시스템 (최소 버전): 오디오 고장 상태 + 리셋 관리.
// 나중에 종 봉쇄 등 확장.
public class AudioSystem : MonoBehaviour
{   
    [Header("참조")]
    public Hyunsoong hyunsoong;
    [Header("오디오 고장")]
    public bool isAudioBroken = false;
    public float breakTimeLimit = 17f;    // 이 시간 안에 리셋 못 하면 STRIKE2 (다음 구현)

    [Header("리셋")]
    public float resetDuration = 5f;      // 리셋에 걸리는 시간

    private float breakTimer = 0f;        // 고장 후 경과
    private float resetTimer = 0f;        // 리셋 진행
    private bool isResetting = false;

    void Update()
    {
        if (!isAudioBroken) return;

        if (isResetting)
        {
            // 리셋 진행 중
            resetTimer += Time.deltaTime;
            if (resetTimer >= resetDuration)
                CompleteReset();
        }
        else
        {
            // 고장 상태 방치 → 타이머 (초과 시 STRIKE2, 다음 구현)
            breakTimer += Time.deltaTime;
            if (breakTimer >= breakTimeLimit)
            {
                Debug.Log("★★★ 오디오 리셋 실패! (STRIKE2 = 퍼펫 즉사,)");
                if (hyunsoong != null) hyunsoong.Strike2();
                isAudioBroken = false;   // 상태 종료 (게임오버니 더 안 돎)

                breakTimer = 0f;   // 임시로 리셋 (무한 로그 방지)
            }
        }
    }

    // 오디오 고장 발생 (현승 STRIKE1이 호출)
    public void BreakAudio()
    {
        if (isAudioBroken) return;
        isAudioBroken = true;
        breakTimer = 0f;
        Debug.Log("오디오 고장! 리셋 필요");
    }

    // 리셋 시작 (패널 버튼이 호출)
    public void StartReset()
    {
        if (!isAudioBroken || isResetting) return;
        isResetting = true;
        resetTimer = 0f;
        Debug.Log("오디오 리셋 시작... (5초)");
    }

    void CompleteReset()
    {
        isAudioBroken = false;
        isResetting = false;
        breakTimer = 0f;
        resetTimer = 0f;
        Debug.Log("오디오 리셋 완료!");
    }

    public bool IsResetting() { return isResetting; }
    public bool IsAudioBroken() { return isAudioBroken; }
}
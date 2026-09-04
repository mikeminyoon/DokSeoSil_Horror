using UnityEngine;

// 오디오 시스템 (최소 버전): 오디오 고장 상태 + 리셋 관리.
// 나중에 종 봉쇄 등 확장.
public class AudioSystem : MonoBehaviour
{
    [Header("참조")]
    public Hyunsoong hyunsoong;
    public Yeonho yeonho;                 // 밤당 종 횟수 한도 조회용 (currentNight)
    [Header("오디오 고장")]
    public bool isAudioBroken = false;
    public float breakTimeLimit = 32f;    // 이 시간 안에 리셋 못 하면 STRIKE2 (다음 구현)

    [Header("리셋")]
    public float resetDuration = 5f;      // 리셋에 걸리는 시간

    [Header("종 사용 횟수 (밤당 제한 — §8.3 예방적 리셋)")]
    public int bellUses = 0;              // 이번 밤 소모한 종 횟수
    public int[] bellLimitByNight = { 0, 0, 5, 4, 3, 2 };  // index = night (1일 미사용, 2~5일 5/4/3/2)

    private float breakTimer = 0f;        // 고장 후 경과
    private float resetTimer = 0f;        // 리셋 진행
    private bool isResetting = false;
    private float activeResetDuration = 5f;  // 이번 리셋에 실제 적용되는 시간(전체 리셋이면 더 길어짐)

    void Update()
    {
        // 리셋 진행 중 — 고장 여부 무관(예방적 리셋도 여기서 진행)
        if (isResetting)
        {
            resetTimer += Time.deltaTime;
            if (resetTimer >= activeResetDuration)
                CompleteReset();
            return;
        }

        if (!isAudioBroken) return;

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

    // 오디오 고장 발생 (현승 STRIKE1이 호출)
    public void BreakAudio()
    {
        if (isAudioBroken) return;
        isAudioBroken = true;
        breakTimer = 0f;
        Debug.Log("오디오 고장! 리셋 필요");
    }

    // 리셋 시작 (패널 버튼이 호출) — 고장 안 났어도 예방적으로 실행 가능.
    // overrideDuration: 전체 리셋(§12.3 "둘 8s")처럼 다른 시간을 쓸 때만 넘김. 안 넘기면 개별 resetDuration.
    public void StartReset(float overrideDuration = -1f)
    {
        if (isResetting) return;
        isResetting = true;
        resetTimer = 0f;
        activeResetDuration = (overrideDuration > 0f) ? overrideDuration : resetDuration;
        Debug.Log($"오디오 리셋 시작... ({activeResetDuration}초)");
    }

    void CompleteReset()
    {
        isAudioBroken = false;
        isResetting = false;
        breakTimer = 0f;
        resetTimer = 0f;
        bellUses = 0;          // 예방적 리셋의 핵심 효과: 종 사용 횟수 초기화
        Debug.Log("오디오 리셋 완료! (종 횟수 초기화)");
    }

    // 종 사용 시도 (Yeonho.RingBell이 호출). 밤당 한도 초과하면 오디오 고장시키고 false 반환.
    public bool TryUseBell()
    {
        if (isAudioBroken) return false;

        bellUses++;
        if (bellUses > GetBellLimit())
        {
            Debug.Log("★★★ 종 사용 횟수 초과! 오디오 고장 (리셋 필요)");
            BreakAudio();
            return false;
        }
        return true;
    }

    int GetBellLimit()
    {
        int night = (yeonho != null) ? yeonho.currentNight : 1;
        night = Mathf.Clamp(night, 0, bellLimitByNight.Length - 1);
        return bellLimitByNight[night];
    }

    public bool IsResetting() { return isResetting; }
    public bool IsAudioBroken() { return isAudioBroken; }
}
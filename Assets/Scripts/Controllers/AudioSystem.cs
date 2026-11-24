using UnityEngine;

// 오디오 시스템 (최소 버전): 오디오 고장 상태 + 리셋 관리.
// 나중에 종 봉쇄 등 확장.
public class AudioSystem : MonoBehaviour
{
    [Header("참조")]
    public Hyunsoong hyunsoong;           // 방치 시 STRIKE2(퍼펫 즉사) 호출용
    // 날짜는 GameManager.Night에서 읽음

    [Header("오디오 고장")]
    public bool isAudioBroken = false;
    public float breakTimeLimit = 32f;    // 이 시간 안에 리셋 못 하면 STRIKE2 (원인 무관 — FNAF2 퍼펫 느낌, 의도됨)
    public int noBreakUntilNight = 1;     // 이 밤까지는 오디오 고장 자체가 없음 (1일차 교육 — STRIKE1도 점프스케어만)

    [Header("리셋")]
    public float resetDuration = 5f;      // 리셋에 걸리는 시간

    [Header("종 사용 횟수 (밤당 제한 — §8.3 예방적 리셋)")]
    public int bellUses = 0;              // 이번 밤 소모한 종 횟수
    public int[] bellLimitByNight = { 0, 0, 5, 4, 3, 2 };  // index = 밤 숫자 그대로 ([0]은 안 쓰는 칸). 0 이하 = 무제한. 1일 무제한, 2~5일 5/4/3/2

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

    // 오디오 고장 발생 (현승 STRIKE1 / 종 과사용이 호출)
    public void BreakAudio()
    {
        if (isAudioBroken) return;

        // 1일차: 고장 없음 → 방치 즉사(STRIKE2)도 자연히 없음
        if (GameManager.Night <= noBreakUntilNight)
        {
            Debug.Log($"오디오 고장 무시 ({GameManager.Night}일차 — 고장 없는 밤)");
            return;
        }

        isAudioBroken = true;
        breakTimer = 0f;
        Debug.Log("오디오 고장! 리셋 필요");
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
        int limit = GetBellLimit();
        if (limit <= 0) return true;   // 0 이하 = 무제한 (1일차: 원작 FNAF3처럼 고장 없음)

        if (bellUses > limit)
        {
            Debug.Log("★★★ 종 사용 횟수 초과! 오디오 고장 (리셋 필요)");
            BreakAudio();
            return false;
        }
        return true;
    }

    int GetBellLimit()
    {
        int night = Mathf.Clamp(GameManager.Night, 0, bellLimitByNight.Length - 1);
        return bellLimitByNight[night];
    }

    public bool IsResetting() { return isResetting; }
    public bool IsAudioBroken() { return isAudioBroken; }
}
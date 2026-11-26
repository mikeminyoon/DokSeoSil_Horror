using UnityEngine;

// 게임 매니저: 밤 타이머(12→6시), currentNight 관리, 클리어/게임오버.
// 빈 오브젝트에 붙인다.
public class GameManager : MonoBehaviour
{
    public static GameManager Instance;   // 다른 스크립트가 쉽게 접근

    // ⭐ 밤 숫자는 여기 한 곳에만 있다. 다른 스크립트는 자기 필드를 두지 말고 GameManager.Night를 읽을 것.
    // Instance는 Awake에서 세팅 → 모든 스크립트의 Start보다 먼저라 실행 순서 문제 없음.
    // GameManager가 없는 테스트 씬이면 1일차로 취급.
    public static int Night => (Instance != null) ? Instance.currentNight : 1;

    // ⭐ 밤별 배열에서 이번 밤 값 꺼내기 — 모든 귀신·시스템이 이것만 씀 (각자 계산하지 말 것)
    // 배열 규칙: 인덱스 = 밤 숫자 그대로, [0]은 안 쓰는 칸, [1]~[5] = 1~5일. 배열이 짧으면 마지막 칸 값.
    public static int ByNight(int[] arr)
    {
        if (arr == null || arr.Length == 0) return 0;
        return arr[Mathf.Clamp(Night, 0, arr.Length - 1)];
    }
    public static float ByNight(float[] arr)
    {
        if (arr == null || arr.Length == 0) return 0f;
        return arr[Mathf.Clamp(Night, 0, arr.Length - 1)];
    }

    // 게임 시각을 0~6으로 (12시 = 0, 1~5시 그대로, 밤 끝 = 6). 시간대 판정은 이걸 쓸 것.
    public static int Hour
    {
        get
        {
            if (Instance == null) return 0;
            if (Instance.isNightOver) return 6;
            return (Instance.gameHour == 12) ? 0 : Instance.gameHour;
        }
    }

    [Header("밤 설정")]
    public int currentNight = 1;           // 지금 몇 밤 (수동, 나중에 저장 시스템으로) — 테스트할 밤은 여기서만 바꿈
    public float nightDuration = 390f;     // 밤 길이 (6분 30초)
    public float firstNightDuration = 240f; // 1일차는 짧게 (~4분)

    [Header("게임 시간 (읽기용 — 시계가 참조)")]
    public int gameHour = 12;   // 현재 게임상 시 (12→1→...→6)
    public int gameMinute = 0;  // 현재 게임상 분

    [Header("상태")]
    public bool isNightOver = false;
    public bool isGameOver = false;

    private float elapsed = 0f;
    private float duration;   // 이번 밤 실제 길이

    void Awake()
    {
        Instance = this;

        // 상태 표시는 밤 시작 때 항상 초기화 — Inspector에서 체크된 채 씬에 저장돼도 시작하자마자 "6시"가 되지 않게
        // (2026-11-26: isNightOver가 체크된 채 저장돼 있었음 → Hour가 처음부터 6 → 현우 즉시 퇴장·연호 흉포 고정 위험)
        // Awake에서 하는 이유: 다른 스크립트 Start가 GameManager.Hour를 읽기 전에 풀려 있어야 함
        isNightOver = false;
        isGameOver = false;
    }

    void Start()
    {
        // 1일차만 짧게
        duration = (currentNight == 1) ? firstNightDuration : nightDuration;

        Debug.Log($"=== 밤 {currentNight} 시작 (길이 {duration}초) ===");
    }

    void Update()
    {
        if (isNightOver || isGameOver) return;

        elapsed += Time.deltaTime;

        // 게임 시간 계산 (12시 → 6시)
        float progress = elapsed / duration;         // 0~1
        float totalMinutes = progress * 6f * 60f;    // 6시간 = 360분
        int hoursPassed = (int)(totalMinutes / 60f); // 몇 시간 지났나 (0~6)
        gameMinute = (int)(totalMinutes % 60f);


        // 12시 시작 → 1,2,3,4,5,6시
        gameHour = (12 + hoursPassed);
        if (gameHour >= 13) gameHour -= 12;   // 12 다음은 1시

        // 임시 확인용 (1초마다 게임 시각 출력)
        //if (Mathf.FloorToInt(elapsed) != Mathf.FloorToInt(elapsed - Time.deltaTime))
            //Debug.Log($"게임 시각: {gameHour}시 {gameMinute}분 (경과 {elapsed:F0}s)");

        // 6시 도달 → 생존
        if (elapsed >= duration)
            NightClear();
    }

    // 밤 생존 클리어
    void NightClear()
    {
        isNightOver = true;
        gameHour = 6;
        gameMinute = 0;
        Debug.Log($"=== 밤 {currentNight} 생존! 6 AM ===");
        // TODO: 다음 밤으로 (저장 시스템 — 나중)
        // TODO: 클리어 연출
    }

    // 게임오버 (즉사 귀신이 호출 — 나중에 연결)
    public void GameOver(string cause)
    {
        if (isGameOver) return;
        isGameOver = true;
        Debug.Log($"=== 게임오버: {cause} (밤 {currentNight}) ===");
        // TODO: 게임오버 연출 / 재시작
    }
}
using UnityEngine;

// 게임 매니저: 밤 타이머(12→6시), currentNight 관리, 클리어/게임오버.
// 빈 오브젝트에 붙인다.
public class GameManager : MonoBehaviour
{
    public static GameManager Instance;   // 다른 스크립트가 쉽게 접근

    [Header("밤 설정")]
    public int currentNight = 1;           // 지금 몇 밤 (수동, 나중에 저장 시스템으로)
    public float nightDuration = 390f;     // 밤 길이 (6분 30초)
    public float firstNightDuration = 240f; // 1일차는 짧게 (~4분)

    [Header("게임 시간 (읽기용 — 시계가 참조)")]
    public int gameHour = 12;   // 현재 게임상 시 (12→1→...→6)
    public int gameMinute = 0;  // 현재 게임상 분

    [Header("귀신 참조 (currentNight 전달)")]
    public Hyunsoong hyunsoong;
    public Hyunwoo hyunwoo;

    [Header("상태")]
    public bool isNightOver = false;
    public bool isGameOver = false;

    private float elapsed = 0f;
    private float duration;   // 이번 밤 실제 길이

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        // 1일차만 짧게
        duration = (currentNight == 1) ? firstNightDuration : nightDuration;

        // 귀신들한테 currentNight 전달
        if (hyunsoong != null) hyunsoong.currentNight = currentNight;
        //if (hyunwoo != null) hyunwoo.currentNight = currentNight;   // 현우도 (지금은 안 쓰지만 대비)

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
        // (12 → 1 → 2 ... → 6)

        // 임시 확인용 (1초마다 게임 시각 출력)
        if (Mathf.FloorToInt(elapsed) != Mathf.FloorToInt(elapsed - Time.deltaTime))
            Debug.Log($"게임 시각: {gameHour}시 {gameMinute}분 (경과 {elapsed:F0}s)");

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
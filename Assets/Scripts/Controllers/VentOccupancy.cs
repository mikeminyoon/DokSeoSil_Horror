using System.Collections.Generic;
using UnityEngine;

// 환풍구 자리 관리 — 관리실 환풍구는 한 번에 한 귀신만 (구현노트 §15.21)
// 귀신은 관리실 환풍구에 들어가기 전에 여기에 물어봄 (TryEnter), 사건이 끝나면 자리를 비움 (Release).
//
// [쓰는 중] 들어오기로 한 순간(불빛 꺼지길 기다리는 시간 포함) ~ 사건 끝(격퇴 / STRIKE1 / 갑툭튀 / 퇴장)
//   현우 "등 뒤로"도 쓰는 중 — 환풍구는 비어 보여도 사건은 안 끝남 (현우 등 뒤 + 현승 환풍구 이중 처벌 방지)
// [비운 뒤] freeGap초 동안 아무도 못 들어옴 (나중에 격퇴 쿵쿵 소리 자리 — 유저 결정 7초)
// [막혔을 때] 귀신마다 다름 — 현우: 화장실에서 기다림(대기 등록) / 현승: 창문으로 돌아감(대기 안 함)
// [우선순위] 숫자 큰 쪽 먼저 (현우 2 > 현승 1 — 유저: "현우가 먼저 오게")
//   - 나보다 우선순위 높은 귀신이 기다리고 있으면, 환풍구가 비어 있어도 못 들어옴.
//   - 낮은 귀신이 쓰는 중에 높은 귀신이 오면 → 낮은 귀신이 비켜줌 (ShouldYield). 단 비켜줄지는 귀신이 정함:
//     현승은 armed 전까지만 비켜줌 (armed면 STRIKE1 벌은 그대로), 환풍구 안이면 플레이어가 못 볼 때만.
//
// 씬에 없으면 귀신(현우·현승)이 자동으로 만듦.
public class VentOccupancy : MonoBehaviour
{
    public static VentOccupancy Instance { get; private set; }

    [Header("비운 뒤 간격")]
    [Tooltip("환풍구가 빈 뒤 이만큼은 아무도 못 들어옴 (격퇴 쿵쿵 소리 자리). 0 = 끔")]
    public float freeGap = 7f;

    [Header("상태 (확인용)")]
    public string occupantName = "";     // 지금 쓰는 귀신
    public string waitingNames = "";     // 기다리는 귀신들
    public float gapLeft = 0f;           // 다음 귀신이 들어올 수 있을 때까지

    private MonoBehaviour occupant;
    private int occupantPriority = 0;    // 지금 쓰는 귀신의 우선순위 (비켜줘야 하나 판단용)
    private readonly Dictionary<MonoBehaviour, int> waiters = new Dictionary<MonoBehaviour, int>();   // 귀신 → 우선순위
    private float freeAt = 0f;           // 이 시각부터 들어올 수 있음

    // 지금 누가 환풍구를 쓰는 중인가
    public bool IsVentUsing => occupant != null;
    public MonoBehaviour Occupant => occupant;

    public static VentOccupancy GetOrCreate()
    {
        if (Instance != null) return Instance;
        var found = FindAnyObjectByType<VentOccupancy>();
        if (found != null) return found;
        return new GameObject("VentOccupancy (자동 생성)").AddComponent<VentOccupancy>();
    }

    void Awake()
    {
        Instance = this;
    }

    void Update()
    {
        gapLeft = Mathf.Max(0f, freeAt - Time.time);   // 확인용 표시만
    }

    // 들어가도 되나? 되면 자리 차지하고 true.
    //   막힘 = 다른 귀신이 쓰는 중 / 비운 뒤 간격 중 / 나보다 우선순위 높은 귀신이 기다리는 중
    //   waitIfBlocked = 막혔을 때 대기자로 등록할지 (현우 true — 기다림 / 현승 false — 돌아감)
    public bool TryEnter(MonoBehaviour ghost, int priority, bool waitIfBlocked)
    {
        if (ghost == null) return false;
        if (occupant == ghost) return true;

        bool blocked = occupant != null || Time.time < freeAt || HasHigherWaiter(ghost, priority);
        if (blocked)
        {
            if (waitIfBlocked && !waiters.ContainsKey(ghost))
            {
                waiters[ghost] = priority;
                RefreshNames();
                Debug.Log($"환풍구 자리: {ghost.name} 대기 시작 ({BlockReason()})");
            }
            return false;
        }

        occupant = ghost;
        occupantPriority = priority;
        waiters.Remove(ghost);
        RefreshNames();
        Debug.Log($"★ 환풍구 자리: {ghost.name} 차지");
        return true;
    }

    // 자리 비우기 (사건 끝) — 대기 중이었으면 대기도 취소. 아무 상태에서 불러도 안전.
    public void Release(MonoBehaviour ghost)
    {
        if (ghost == null) return;
        bool changed = waiters.Remove(ghost);

        if (occupant == ghost)
        {
            occupant = null;
            freeAt = Time.time + freeGap;
            changed = true;
            Debug.Log($"★ 환풍구 자리: {ghost.name} 비움 — {freeGap:F0}초 동안 아무도 못 들어옴");
        }

        if (changed) RefreshNames();
    }

    // 내가 쓰는 중인데 나보다 우선순위 높은 귀신이 기다리나 → 비켜줘야 함 (언제 비켜줄지는 귀신이 정함)
    public bool ShouldYield(MonoBehaviour ghost)
    {
        return ghost != null && occupant == ghost && HasHigherWaiter(ghost, occupantPriority);
    }

    // 나보다 우선순위 높은 귀신이 기다리고 있나 (꺼진 귀신·사라진 귀신은 무시 — 방전 암전 등)
    bool HasHigherWaiter(MonoBehaviour ghost, int priority)
    {
        foreach (var w in waiters)
        {
            if (w.Key == null || w.Key == ghost || !w.Key.isActiveAndEnabled) continue;
            if (w.Value > priority) return true;
        }
        return false;
    }

    string BlockReason()
    {
        if (occupant != null) return $"{occupant.name} 사용 중";
        if (Time.time < freeAt) return "비운 뒤 간격";
        return "우선순위 높은 귀신 대기 중";
    }

    void RefreshNames()
    {
        occupantName = (occupant != null) ? occupant.name : "";
        var names = new List<string>();
        foreach (var w in waiters)
            if (w.Key != null) names.Add($"{w.Key.name}({w.Value})");
        waitingNames = string.Join(", ", names);
    }
}

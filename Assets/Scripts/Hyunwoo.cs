using UnityEngine;

// 현우: 환풍구 전용 귀신 (엔진B).
// 허공(등장대기) → 공부방2 앉음 → 기립 → 화장실 → 관리실 입구.
// 현우 오브젝트(캡슐)에 붙인다.
public class Hyunwoo : MonoBehaviour
{
    [Header("환풍구 루트 노드")]
    public Transform[] nodes;   // [허공, 공부방2앉음, 기립, 화장실, 관리실입구]

    [Header("이동 판정")]
    public int aiLevel = 5;
    public float cycleTime = 5f;
    public int fixedNodeCount = 3;   // 앞 3노드(허공/앉음/기립)는 고정 시간

    [Header("고정 노드 대기 시간 (노드별, 랜덤)")]
    public Vector2[] fixedNodeWaits = {
        new Vector2(10f, 30f),   // 노드0: 허공 (등장 대기)
        new Vector2(10f, 20f),   // 노드1: 앉음
        new Vector2(3f, 7f),     // 노드2: 기립
    };

    [Header("참조")]
    public MonitorDisplay monitor;

    private int currentNode = 0;
    private float timer = 0f;
    private float currentWait = 0f;
    private bool atEntrance = false;

    void Start()
    {
        if (nodes.Length > 0) MoveToNode(0);
    }

    void Update()
    {
        if (atEntrance) return;

        // 고정 노드 구간 (허공/앉음/기립) — 정해진 랜덤 시간 대기
        if (currentNode < fixedNodeCount)
        {
            timer += Time.deltaTime;
            if (timer >= currentWait)
            {
                timer = 0f;
                MoveToNode(currentNode + 1);
            }
        }
        // 확률 이동 구간 (환풍구~)
        else
        {
            timer += Time.deltaTime;
            if (timer >= cycleTime)
            {
                timer = 0f;
                int roll = Random.Range(0, 20);
                if (roll < aiLevel)
                    MoveToNode(currentNode + 1);
            }
        }
    }

    void MoveToNode(int index)
    {
        if (index >= nodes.Length)
        {
            ReachEntrance();
            return;
        }

        currentNode = index;
        transform.position = nodes[index].position;
        transform.rotation = nodes[index].rotation;

        // CCTV 스태틱은 공부방2(앉음/기립)에서만. 허공(0)·환풍구는 X.
        // 노드1(앉음)=등장 순간 지직, 노드2(기립). 노드0(허공)·노드3+(환풍구)는 스태틱 없음.
        if (index == 1 || index == 2 || index == 3)
        {
            if (monitor != null) monitor.GhostMoveStatic();
        }

        // 고정 노드면 배열에서 랜덤 대기시간
        if (index < fixedNodeCount && index < fixedNodeWaits.Length)
            currentWait = Random.Range(fixedNodeWaits[index].x, fixedNodeWaits[index].y);

        Debug.Log($"현우 이동 → 노드 {index}");
    }

    void ReachEntrance()
    {
        atEntrance = true;
        Debug.Log("★ 현우 관리실 입구 도달! (셔터 판정 다음 구현)");
        // TODO: 유예 타이머 → 셔터 닫혔나 확인 → 막힘/armed
    }

    public void ResetToStart()
    {
        atEntrance = false;
        currentNode = 0;
        timer = 0f;
        MoveToNode(0);
    }
}
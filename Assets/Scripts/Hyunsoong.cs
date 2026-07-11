using UnityEngine;

// 현승 노드 이동 (첫 조각): 노드 배열을 순서대로 순간이동.
// 지금은 타이머 자동 진행. 나중에 엔진B 확률 이동으로 교체.
public class Hyeonsoong : MonoBehaviour
{
    [Header("노드 경로 (순서대로)")]
    public Transform[] nodes;          // [착석, 기립, 가로복도, 세로복도상단, 화장실, 창문]

    [Header("각 노드 머무는 시간(초)")]
    public float moveInterval = 6f;    // 일단 느리게(2배). 프로토타입 튜닝

    private int currentNode = 0;       // 현재 위치 인덱스
    private float timer = 0f;

    void Start()
    {
        // 시작 위치 = 첫 노드(착석)
        if (nodes.Length > 0)
            MoveToNode(0);
    }

    void Update()
    {
        // 마지막 노드(창문) 도달하면 더 안 감 (여기부턴 나중에 워크패스/응시)
        if (currentNode >= nodes.Length - 1) return;

        timer += Time.deltaTime;
        if (timer >= moveInterval)
        {
            timer = 0f;
            MoveToNode(currentNode + 1);   // 다음 노드로
        }
    }

    // 캡슐을 지정 노드로 순간이동
    void MoveToNode(int index)
    {
        currentNode = index;
        transform.position = nodes[index].position;
        transform.rotation = nodes[index].rotation;
        // TODO: 나중에 여기서 이동 사운드 / 상태별 애니(착석·기립) 처리
    }
}
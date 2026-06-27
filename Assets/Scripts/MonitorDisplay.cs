using UnityEngine;

// 모니터 화면 관리: 평소엔 스태틱 노이즈, CCTV 켜지면 방 영상.
// Monitor 큐브에 붙인다.
public class MonitorDisplay : MonoBehaviour
{
    [Header("참조")]
    public CCTVController cctv;          // CCTV 상태 읽기용
    public Texture cctvFeed;             // CCTV 방 영상 (CCTV_RT_01)

    [Header("스태틱 설정")]
    public int staticSize = 128;         // 노이즈 텍스처 해상도 (작을수록 거칠고 가벼움)

    private Texture2D staticTex;         // 매 프레임 랜덤으로 칠할 텍스처
    private Material monitorMat;         // 모니터 머티리얼 (인스턴스)
    private Color32[] pixels;            // 픽셀 버퍼 (재사용)
    private float staticTimer = 0f;
    public float staticInterval = 0.05f;  // 이 간격(초)마다 노이즈 갱신

    void Start()
    {
        // 모니터 머티리얼 가져오기 (이 큐브의 머티리얼)
        monitorMat = GetComponent<Renderer>().material;

        // 스태틱용 텍스처 생성
        staticTex = new Texture2D(staticSize, staticSize);
        staticTex.filterMode = FilterMode.Point;   // 픽셀 쨍하게
        pixels = new Color32[staticSize * staticSize];

        if (cctv == null)
            cctv = FindAnyObjectByType<CCTVController>();
    }

    void Update()
    {

        if (cctv != null && cctv.isCameraDown)
        {
            // CCTV 켜짐 → 방 영상 표시
            monitorMat.mainTexture = cctvFeed;
        }
        else
        {
            // 일정 간격마다만 노이즈 갱신 (너무 빠른 깜빡임 방지)
            staticTimer += Time.deltaTime;
            if (staticTimer >= staticInterval)
            {
                GenerateStatic();
                staticTimer = 0f;
            }
            monitorMat.mainTexture = staticTex;
        }
    }

    // 모든 픽셀을 랜덤 회색으로 칠해 노이즈 생성
    // 가로줄 느낌의 노이즈 생성 (FNAF식)
    void GenerateStatic()
    {
        for (int y = 0; y < staticSize; y++)
        {
            // 한 줄(row)마다 기준 밝기를 정함 → 같은 줄은 비슷한 값 = 가로줄 느낌
            int rowBase = Random.Range(0, 256); 

            for (int x = 0; x < staticSize; x++)
            {
                // 줄 기준값에 약간의 흔들림만 더함 (가로로 이어지는 느낌)
                int v = rowBase + Random.Range(-80, 30);
                v = Mathf.Clamp(v, 0, 255);

                byte b = (byte)v;
                pixels[y * staticSize + x] = new Color32(b, b, b, 255);
            }
        }
        staticTex.SetPixels32(pixels);
        staticTex.Apply();
    }
}
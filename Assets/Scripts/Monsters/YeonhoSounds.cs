using UnityEngine;

// 연호 임시 소리 (구현노트 §15.24) — 진짜 소리는 나중에(Phase 8) 각 칸에 AudioClip만 넣으면 됨. 비우면 코드가 임시 소리를 만듦.
//   ① 창문앞 도착: 숨소리 — 어디서든 들림 (유저 계획: 진짜 연출은 창문앞 복도 조명 깜빡 + 숨소리)
//   ② 창고 들어감: 우당탕 — 어디서든 들림 (들어간 건 알 수 있음)
//   ③ 창고 안: CAM07을 보고 있을 때만 부스럭·숨소리 반복 (아직 있는지 확인하는 방법 — FNAF2식)
//   나갈 땐 소리 없음 → 나갔는지 모름 (유저 설계: "간 줄 알았는데 사실 있었다")
// 언제 울릴지는 연호(Yeonho.cs)가 정함 — 여기는 소리만 냄.
// 씬에 없으면 연호가 자동으로 붙임. 연호가 꺼지면(방전 암전) Yeonho.OnDisable이 StopAll을 부름.
public class YeonhoSounds : MonoBehaviour
{
    [Header("진짜 소리 (비우면 임시 소리 자동 생성)")]
    public AudioClip windowArriveClip;    // 창문앞 도착 (숨소리)
    public AudioClip storageEnterClip;    // 창고 들어감 (우당탕)
    public AudioClip storageInsideClip;   // 창고 안 (부스럭, 반복)

    [Header("볼륨")]
    [Range(0f, 1f)] public float windowArriveVolume = 0.7f;
    [Range(0f, 1f)] public float storageEnterVolume = 0.8f;
    [Range(0f, 1f)] public float storageInsideVolume = 0.6f;

    private AudioSource oneShot;      // 숨소리·우당탕 (한 번씩)
    private AudioSource insideLoop;   // 창고 안 부스럭 (CAM07 볼 때만, 반복)

    void Awake()
    {
        oneShot = gameObject.AddComponent<AudioSource>();
        oneShot.playOnAwake = false;
        oneShot.spatialBlend = 0f;    // 임시: 2D (Phase 8에서 위치 소리로)

        insideLoop = gameObject.AddComponent<AudioSource>();
        insideLoop.playOnAwake = false;
        insideLoop.loop = true;
        insideLoop.spatialBlend = 0f;

        if (windowArriveClip == null) windowArriveClip = CreateBreathClip();
        if (storageEnterClip == null) storageEnterClip = CreateCrashClip();
        if (storageInsideClip == null) storageInsideClip = CreateRustleClip();
        insideLoop.clip = storageInsideClip;
    }

    // === 연호가 부름 ===

    public void PlayWindowArrive()
    {
        if (oneShot != null && windowArriveClip != null)
            oneShot.PlayOneShot(windowArriveClip, windowArriveVolume);
    }

    public void PlayStorageEnter()
    {
        if (oneShot != null && storageEnterClip != null)
            oneShot.PlayOneShot(storageEnterClip, storageEnterVolume);
    }

    // 창고 안 부스럭: 켜야 할 때 true (매 프레임 불러도 됨 — 바뀔 때만 재생/정지)
    public void SetStorageListening(bool on)
    {
        if (insideLoop == null) return;
        if (on)
        {
            if (!insideLoop.isPlaying)
            {
                insideLoop.volume = storageInsideVolume;
                insideLoop.Play();
            }
        }
        else if (insideLoop.isPlaying)
        {
            insideLoop.Stop();
        }
    }

    public void StopAll()
    {
        if (oneShot != null) oneShot.Stop();
        if (insideLoop != null) insideLoop.Stop();
    }

    // === 임시 소리 만들기 (진짜 소리 파일 없을 때) ===
    const int SampleRate = 44100;

    // 숨소리: 들숨 + 날숨 (어둡게 깎은 잡음이 천천히 부풀었다 잦아듦)
    AudioClip CreateBreathClip()
    {
        var rng = new System.Random(11);
        float total = 2.6f;
        var data = new float[(int)(SampleRate * total)];
        float lp = 0f;
        for (int i = 0; i < data.Length; i++)
        {
            float t = i / (float)SampleRate;
            float env = 0f, cutoff = 0.05f;
            if (t < 1.1f) { env = Mathf.Sin(Mathf.PI * t / 1.1f); cutoff = 0.05f; }                       // 들숨 (어두움)
            else if (t > 1.3f && t < 2.5f) { env = Mathf.Sin(Mathf.PI * (t - 1.3f) / 1.2f); cutoff = 0.09f; } // 날숨 (조금 밝음)
            env *= env;
            float noise = (float)(rng.NextDouble() * 2.0 - 1.0);
            lp += cutoff * (noise - lp);                              // 간단한 저역 통과 (쉭 소리를 어둡게)
            float rasp = 1f + 0.25f * Mathf.Sin(2f * Mathf.PI * 28f * t);   // 살짝 거친 떨림
            data[i] = lp * env * rasp * 3.2f;
        }
        return MakeClip("Yeonho_Breath_임시", data);
    }

    // 우당탕: 쿵·쿵·쿵 세 번 + 잡음 튀김 + 마지막에 달그락
    AudioClip CreateCrashClip()
    {
        var rng = new System.Random(23);
        float total = 1.4f;
        var data = new float[(int)(SampleRate * total)];
        float[] hits = { 0.00f, 0.17f, 0.42f, 0.78f };
        float[] power = { 1.0f, 0.8f, 0.9f, 0.35f };
        for (int h = 0; h < hits.Length; h++)
        {
            int start = (int)(hits[h] * SampleRate);
            float thumpFreq = 55f + (float)rng.NextDouble() * 40f;
            float lp = 0f;
            for (int i = start; i < data.Length; i++)
            {
                float t = (i - start) / (float)SampleRate;
                if (t > 0.6f) break;
                float noise = (float)(rng.NextDouble() * 2.0 - 1.0);
                lp += 0.35f * (noise - lp);
                float thump = Mathf.Sin(2f * Mathf.PI * thumpFreq * t) * Mathf.Exp(-t * 11f);   // 묵직한 쿵
                float crash = lp * Mathf.Exp(-t * 22f);                                          // 부딪히는 잡음
                data[i] += (thump * 0.7f + crash * 0.9f) * power[h];
            }
        }
        for (int i = 0; i < data.Length; i++) data[i] = Mathf.Clamp(data[i] * 0.8f, -1f, 1f);
        return MakeClip("Yeonho_Crash_임시", data);
    }

    // 부스럭: 낮게 깔린 숨 잡음 + 드문드문 바스락 (반복용 3초)
    AudioClip CreateRustleClip()
    {
        var rng = new System.Random(37);
        float total = 3.0f;
        var data = new float[(int)(SampleRate * total)];
        float lp = 0f;
        for (int i = 0; i < data.Length; i++)
        {
            float t = i / (float)SampleRate;
            float noise = (float)(rng.NextDouble() * 2.0 - 1.0);
            lp += 0.03f * (noise - lp);
            float swell = 0.5f + 0.5f * Mathf.Sin(2f * Mathf.PI * t / total);   // 3초 한 바퀴 (반복 이음매 자연스럽게)
            data[i] = lp * swell * 0.9f;
        }
        for (int g = 0; g < 16; g++)   // 바스락 알갱이
        {
            int start = rng.Next(0, data.Length - SampleRate / 10);
            float len = 0.02f + (float)rng.NextDouble() * 0.05f;
            float amp = 0.12f + (float)rng.NextDouble() * 0.22f;
            float prev = 0f;
            for (int i = 0; i < (int)(len * SampleRate); i++)
            {
                float t = i / (float)SampleRate;
                float noise = (float)(rng.NextDouble() * 2.0 - 1.0);
                float high = noise - prev;    // 날카로운 바스락 (고역)
                prev = noise;
                data[start + i] += high * amp * Mathf.Exp(-t * 60f);
            }
        }
        for (int i = 0; i < data.Length; i++) data[i] = Mathf.Clamp(data[i], -1f, 1f);
        return MakeClip("Yeonho_Rustle_임시", data);
    }

    AudioClip MakeClip(string clipName, float[] data)
    {
        for (int i = 0; i < data.Length; i++) data[i] = Mathf.Clamp(data[i], -1f, 1f);   // 찢어지는 소리 방지
        var clip = AudioClip.Create(clipName, data.Length, 1, SampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }
}

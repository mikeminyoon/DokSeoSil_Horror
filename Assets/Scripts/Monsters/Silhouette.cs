using UnityEngine;

// 실루엣 — 모델을 새까맣게 (빛·디테일 없이 모양만). 구현노트 §15.25
//   창문 너머 연호는 실루엣만 보이게 (기획서 "창문 = 육안(실루엣) 확인", 유저 재확인 2026-11-27)
//   SetOn(true): 모델 아래 모든 렌더러의 재질을 검은 무광 재질로 / SetOn(false): 원래 재질로 되돌림
//   재질 칸(silhouetteMaterial)이 비면 코드가 URP Unlit 검정 재질을 만듦 (CCTV_ShadingMat이 URP Unlit을 써서 빌드에도 셰이더 들어감)
// 어느 귀신에든 붙여서 쓸 수 있음 (지금은 연호 모델에 연호가 자동으로 붙임). 재질만 바꾸고 켜고 끄기·위치는 안 건드림.
public class Silhouette : MonoBehaviour
{
    [Tooltip("비우면 검은 무광(URP Unlit) 재질 자동 생성")]
    public Material silhouetteMaterial;

    public bool IsOn { get; private set; }

    private Renderer[] renderers;
    private Material[][] originals;
    private static Material autoBlack;   // 자동 생성 검정 재질 (모든 실루엣이 같이 씀)

    public void SetOn(bool on)
    {
        if (on == IsOn) return;

        if (renderers == null) CacheRenderers();
        Material black = GetSilhouetteMaterial();
        if (on && black == null) return;   // 셰이더를 못 찾음 (경고는 GetSilhouetteMaterial에서)

        IsOn = on;
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] == null) continue;
            if (on)
            {
                var mats = new Material[originals[i].Length];
                for (int m = 0; m < mats.Length; m++) mats[m] = black;
                renderers[i].sharedMaterials = mats;
            }
            else
            {
                renderers[i].sharedMaterials = originals[i];
            }
        }
    }

    // 처음 쓸 때 한 번: 원래 재질 기억 (모델이 꺼져 있어도 찾음)
    void CacheRenderers()
    {
        renderers = GetComponentsInChildren<Renderer>(true);
        originals = new Material[renderers.Length][];
        for (int i = 0; i < renderers.Length; i++)
            originals[i] = renderers[i].sharedMaterials;
    }

    Material GetSilhouetteMaterial()
    {
        if (silhouetteMaterial != null) return silhouetteMaterial;
        if (autoBlack != null) return autoBlack;

        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Unlit/Color");
        if (shader == null)
        {
            Debug.LogWarning("실루엣: 검은 재질용 셰이더를 못 찾음 — Silhouette Material 칸에 검은 무광 재질을 넣어주세요");
            return null;
        }
        autoBlack = new Material(shader) { name = "Silhouette_검정(자동)" };
        if (autoBlack.HasProperty("_BaseColor")) autoBlack.SetColor("_BaseColor", Color.black);
        if (autoBlack.HasProperty("_Color")) autoBlack.SetColor("_Color", Color.black);
        return autoBlack;
    }
}

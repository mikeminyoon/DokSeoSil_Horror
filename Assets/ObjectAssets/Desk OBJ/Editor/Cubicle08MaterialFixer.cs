#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

// Rewires Cubicle08 materials for whichever render pipeline the project uses.
// Runs automatically once if the shipped URP materials can't render (pink),
// and is also available from Tools > Cubicle08 > Rebuild Materials.
[InitializeOnLoad]
public static class Cubicle08MaterialFixer
{
    const string Root = "Assets/Cubicle08_FireDamage";
    const string SessionKey = "Cubicle08MaterialFixer.Checked";

    static Cubicle08MaterialFixer()
    {
        EditorApplication.delayCall += AutoCheck;
    }

    static void AutoCheck()
    {
        if (SessionState.GetBool(SessionKey, false)) return;
        SessionState.SetBool(SessionKey, true);
        var probe = AssetDatabase.LoadAssetAtPath<Material>(Root + "/Materials/M_Cubicle08_A_Desk.mat");
        if (probe == null) return;
        var s = probe.shader;
        bool broken = s == null || !s.isSupported || s.name.Contains("InternalErrorShader");
        bool wrongPipeline = (GraphicsSettings.currentRenderPipeline == null) == s.name.StartsWith("Universal Render Pipeline");
        if (broken || wrongPipeline) Rebuild();
    }

    [MenuItem("Tools/Cubicle08/Rebuild Materials")]
    public static void Rebuild()
    {
        var rp = GraphicsSettings.currentRenderPipeline;
        string rpName = rp == null ? "" : rp.GetType().Name;
        bool urp = rpName.Contains("Universal");
        bool hdrp = rpName.Contains("HDRenderPipeline");
        Shader shader = urp ? Shader.Find("Universal Render Pipeline/Lit")
                      : hdrp ? Shader.Find("HDRP/Lit")
                      : Shader.Find("Standard");
        if (shader == null) { Debug.LogError("[Cubicle08] Lit shader not found for this pipeline."); return; }

        int count = 0;
        foreach (var guid in AssetDatabase.FindAssets("t:Material", new[] { Root + "/Materials" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            string key = Path.GetFileNameWithoutExtension(path).Substring(2); // Cubicle08_A_Desk
            Texture2D bc = Tex(key, "BaseColor"), n = Tex(key, "Normal"),
                      ms = Tex(key, "MetallicSmoothness"), ao = Tex(key, "AO");
            mat.shader = shader;
            if (urp)
            {
                mat.SetTexture("_BaseMap", bc); mat.SetTexture("_MainTex", bc);
                mat.SetTexture("_BumpMap", n); mat.SetTexture("_MetallicGlossMap", ms);
                mat.SetTexture("_OcclusionMap", ao);
                mat.SetFloat("_WorkflowMode", 1); mat.SetFloat("_Smoothness", 1);
                mat.SetFloat("_SmoothnessTextureChannel", 0); mat.SetFloat("_Metallic", 1);
                mat.SetColor("_BaseColor", Color.white);
                mat.EnableKeyword("_NORMALMAP"); mat.EnableKeyword("_METALLICSPECGLOSSMAP"); mat.EnableKeyword("_OCCLUSIONMAP");
            }
            else if (hdrp)
            {
                mat.SetTexture("_BaseColorMap", bc); mat.SetTexture("_NormalMap", n);
                mat.SetColor("_BaseColor", Color.white);
                mat.SetFloat("_Metallic", 0); mat.SetFloat("_Smoothness", 0.35f);
                mat.EnableKeyword("_NORMALMAP"); mat.EnableKeyword("_NORMALMAP_TANGENT_SPACE");
            }
            else
            {
                mat.SetTexture("_MainTex", bc); mat.SetTexture("_BumpMap", n);
                mat.SetTexture("_MetallicGlossMap", ms); mat.SetTexture("_OcclusionMap", ao);
                mat.SetFloat("_GlossMapScale", 1); mat.SetFloat("_SmoothnessTextureChannel", 0);
                mat.SetColor("_Color", Color.white);
                mat.EnableKeyword("_NORMALMAP"); mat.EnableKeyword("_METALLICGLOSSMAP");
            }
            EditorUtility.SetDirty(mat);
            count++;
        }

        foreach (var guid in AssetDatabase.FindAssets("t:Model", new[] { Root + "/Models" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var imp = AssetImporter.GetAtPath(path) as ModelImporter;
            if (imp == null) continue;
            string matName = "M_" + Path.GetFileNameWithoutExtension(path).Substring(3);
            var mat = AssetDatabase.LoadAssetAtPath<Material>(Root + "/Materials/" + matName + ".mat");
            if (mat == null) continue;
            imp.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), matName), mat);
            imp.SaveAndReimport();
        }
        AssetDatabase.SaveAssets();
        Debug.Log("[Cubicle08] Rebuilt " + count + " materials for " + (urp ? "URP" : hdrp ? "HDRP" : "Built-in") + ".");
    }

    static Texture2D Tex(string key, string map)
    {
        return AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "/Textures/T_" + key + "_" + map + ".png");
    }
}
#endif

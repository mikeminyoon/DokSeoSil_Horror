// 고해상도 패널 글자 층(RawImageHIgh)을 화면에 겹칠 때 쓰는 UI 셰이더.
//
// 왜 필요하냐: 패널 글자 카메라(PanelUI_Camera)가 PanelUIRT에 TextMeshPro 글자를 그리면,
// TMP는 "미리 곱한 알파(premultiplied)" 방식으로 그려서 RT 안 색이 이미 알파가 곱해진 상태다.
// 이걸 기본 UI 셰이더(SrcAlpha 섞기)로 또 겹치면 반투명 부분(빛 번짐·글자 가장자리)이
// 알파가 한 번 더 곱해져서 흐리고 어둡게 보인다.
// → 이 셰이더는 "One, OneMinusSrcAlpha"로 한 번만 섞어서 Scene 뷰에서 본 그대로 보이게 한다.
//
// 사용: 이 셰이더로 재질을 하나 만들어 RawImageHIgh의 Material 칸에 넣기.
Shader "UI/PremultipliedRaw"
{
    Properties
    {
        [PerRendererData] _MainTex ("Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        // 기본 UI 셰이더와 같은 칸들 (마스크 등과 호환용)
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
        }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend One OneMinusSrcAlpha      // ← 핵심: 이미 알파가 곱해진 색이라 한 번만 섞음
        ColorMask [_ColorMask]

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float4 color  : COLOR;
                float2 uv     : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos   : SV_POSITION;
                fixed4 color : COLOR;
                float2 uv    : TEXCOORD0;
            };

            sampler2D _MainTex;
            fixed4 _Color;

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color * _Color;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 c = tex2D(_MainTex, i.uv);   // 이미 알파가 곱해진 색
                return c * i.color.a;               // RawImage 전체 투명도만 적용 (색·알파 같이)
            }
            ENDCG
        }
    }
}

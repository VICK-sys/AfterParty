Shader "UnityParty/TitleHue"
{
    Properties
    {
        [PerRendererData] _MainTex ("Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _Hue ("Hue", Float) = 0
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Alpha Clip", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" "CanUseSpriteAtlas"="True" }
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
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"
            struct Input
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
            };
            struct Output
            {
                float4 vertex : SV_POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
                float4 world : TEXCOORD1;
            };
            sampler2D _MainTex;
            float4 _Color;
            float4 _TextureSampleAdd;
            float4 _ClipRect;
            float _Hue;
            Output vert(Input input)
            {
                Output output;
                output.world = input.vertex;
                output.vertex = UnityObjectToClipPos(input.vertex);
                output.uv = input.uv;
                output.color = input.color * _Color;
                return output;
            }
            float4 frag(Output input) : SV_Target
            {
                float4 color = (tex2D(_MainTex, input.uv) + _TextureSampleAdd) * input.color;
                float4 k = float4(0, -1.0 / 3.0, 2.0 / 3.0, -1);
                float4 p = lerp(float4(color.bg, k.wz), float4(color.gb, k.xy), step(color.b, color.g));
                float4 q = lerp(float4(p.xyw, color.r), float4(color.r, p.yzx), step(p.x, color.r));
                float d = q.x - min(q.w, q.y);
                float3 hsv = float3(abs(q.z + (q.w - q.y) / (6 * d + 1e-10)), d / (q.x + 1e-10), q.x);
                hsv.x += _Hue;
                float3 rgb = abs(frac(hsv.xxx + float3(1, 2.0 / 3.0, 1.0 / 3.0)) * 6 - 3);
                color.rgb = hsv.z * lerp(float3(1, 1, 1), saturate(rgb - 1), hsv.y);
                #ifdef UNITY_UI_CLIP_RECT
                color.a *= UnityGet2DClipping(input.world.xy, _ClipRect);
                #endif
                #ifdef UNITY_UI_ALPHACLIP
                clip(color.a - 0.001);
                #endif
                return color;
            }
            ENDCG
        }
    }
}

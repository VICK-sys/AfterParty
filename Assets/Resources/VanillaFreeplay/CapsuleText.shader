Shader "UnityParty/Freeplay Capsule Text"
{
    Properties
    {
        [PerRendererData] _MainTex ("Texture", 2D) = "white" {}
        _GlowColor ("Glow", Color) = (0, .8, 1, 1)
        _BlurColor ("Blur", Color) = (0, .8, 1, 1)
        _Selected ("Selected", Float) = 1
        _Destination ("Destination", Float) = 10
        _Additive ("Additive", Float) = 0
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Stencil { Ref [_Stencil] Comp [_StencilComp] Pass [_StencilOp] ReadMask [_StencilReadMask] WriteMask [_StencilWriteMask] }
        Cull Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha [_Destination], One OneMinusSrcAlpha
        ColorMask [_ColorMask]
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"
            struct appdata { float4 vertex : POSITION; float4 color : COLOR; float2 uv : TEXCOORD0; };
            struct v2f { float4 vertex : SV_POSITION; float4 color : COLOR; float2 uv : TEXCOORD0; float4 position : TEXCOORD1; };
            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            float4 _GlowColor, _BlurColor, _ClipRect;
            float _Selected, _Additive;
            float2 ClampUV(float2 uv)
            {
                float2 edge = 12.5 * _MainTex_TexelSize.xy;
                return clamp(uv, edge, 1 - edge);
            }
            float Blur(float2 uv, float center)
            {
                float result = center * .1964825501511404;
                const float3 offsets = float3(2.823529411764706, 6.588235294117646, 10.352941176470588);
                const float3 weights = float3(.1484534823364172, .04723519892522366, .005190681200574029);
                [unroll]
                for (int tap = 0; tap < 3; tap++)
                {
                    float2 offset = offsets[tap] * _MainTex_TexelSize.xy;
                    result += weights[tap] * (tex2D(_MainTex, ClampUV(uv + float2(offset.x, 0))).r
                        + tex2D(_MainTex, ClampUV(uv - float2(offset.x, 0))).r
                        + tex2D(_MainTex, ClampUV(uv + float2(0, offset.y))).r
                        + tex2D(_MainTex, ClampUV(uv - float2(0, offset.y))).r);
                }
                return result;
            }
            v2f vert(appdata v)
            {
                v2f o;
                o.position = v.vertex;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.color = v.color;
                o.uv = v.uv;
                return o;
            }
            fixed4 frag(v2f i) : SV_Target
            {
                float3 mask = tex2D(_MainTex, ClampUV(i.uv)).rgb;
                mask.g *= _GlowColor.a;
                float front = mask.r + mask.g;
                float back = _Selected > .5 ? Blur(i.uv, mask.r) : 0;
                float coverage = lerp(1 - front, 1, _Additive);
                float alpha = saturate(front + back * coverage);
                float3 rgb = (mask.r * i.color.rgb + _GlowColor.rgb * mask.g
                    + _BlurColor.rgb * back * coverage) / max(alpha, .00001);
                fixed4 color = fixed4(rgb, alpha * i.color.a);
                #ifdef UNITY_UI_CLIP_RECT
                color.a *= UnityGet2DClipping(i.position.xy, _ClipRect);
                color.a *= i.position.x < _ClipRect.z;
                #endif
                return color;
            }
            ENDCG
        }
    }
}

Shader "UnityParty/Menu Blend"
{
    Properties
    {
        [PerRendererData] _MainTex ("Texture", 2D) = "white" {}
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _SrcBlend ("Source Blend", Float) = 5
        _DstBlend ("Destination Blend", Float) = 10
        _Premultiply ("Premultiply", Float) = 0
        _ReplaceGreen ("Replace Green", Float) = 0
        _GreenTint ("Green Tint", Color) = (0,1,0,1)
        _Mosaic ("Mosaic", Vector) = (0,0,0,0)
        _ColorMask ("Color Mask", Float) = 15
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" "CanUseSpriteAtlas"="True" }
        Stencil { Ref [_Stencil] Comp [_StencilComp] Pass [_StencilOp] ReadMask [_StencilReadMask] WriteMask [_StencilWriteMask] }
        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend [_SrcBlend] [_DstBlend]
        ColorMask [_ColorMask]
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"
            struct appdata { float4 vertex : POSITION; float4 color : COLOR; float2 uv : TEXCOORD0; float2 outline : TEXCOORD1; };
            struct v2f { float4 vertex : SV_POSITION; float4 color : COLOR; float2 uv : TEXCOORD0; float4 position : TEXCOORD1; float outline : TEXCOORD2; };
            sampler2D _MainTex;
            float4 _ClipRect;
            float _Premultiply;
            float _ReplaceGreen;
            float4 _GreenTint;
            float4 _Mosaic;
            v2f vert(appdata v)
            {
                v2f o;
                o.position = v.vertex;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.color = v.color;
                o.uv = v.uv;
                o.outline = v.outline.x;
                return o;
            }
            fixed4 frag(v2f i) : SV_Target
            {
                float2 uv = i.uv;
                if (_Mosaic.x > 0 && _Mosaic.y > 0) uv = floor(uv * _Mosaic.xy) / _Mosaic.xy;
                fixed4 sampled = tex2D(_MainTex, uv);
                sampled.rgb = lerp(sampled.rgb, 1, i.outline);
                float green = saturate((sampled.g - max(sampled.r, sampled.b)) * 1000) * _ReplaceGreen;
                sampled.rgb = lerp(sampled.rgb, _GreenTint.rgb * sampled.g, green);
                fixed4 color = sampled * i.color;
                #ifdef UNITY_UI_CLIP_RECT
                color.a *= UnityGet2DClipping(i.position.xy, _ClipRect);
                #endif
                color.rgb *= lerp(1, color.a, _Premultiply);
                return color;
            }
            ENDCG
        }
    }
}

Shader "UnityParty/FunkinNote"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Saturation ("Saturation", Float) = 1
        _Screen ("Screen Blend", Float) = 0
        _HudOpacity ("HUD Opacity", Float) = 1
        _SrcBlend ("Source Blend", Float) = 5
        _DstBlend ("Destination Blend", Float) = 10
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "CanUseSpriteAtlas"="True" }
        Cull Off
        ZWrite Off
        Blend [_SrcBlend] [_DstBlend]
        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment Fragment
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            float _Saturation;
            float _Screen;
            float _HudOpacity;
            struct Attributes
            {
                float4 position : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };
            struct Varyings
            {
                float4 position : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };
            Varyings Vertex(Attributes input)
            {
                Varyings output;
                output.position = UnityObjectToClipPos(input.position);
                output.uv = input.uv;
                output.color = input.color;
                return output;
            }
            float4 Fragment(Varyings input) : SV_Target
            {
                float4 color = tex2D(_MainTex, input.uv);
                float value = max(color.r, max(color.g, color.b));
                color.rgb = lerp(value.xxx, color.rgb, _Saturation);
                color *= input.color;
                color.a *= _HudOpacity;
                if (_Screen > 0) color.rgb *= color.a;
                return color;
            }
            ENDHLSL
        }
    }
}

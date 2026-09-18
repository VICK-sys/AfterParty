Shader "Unity Party/Week 2 Composite"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Opacity ("Opacity", Float) = 1
        _PhillyColor ("Philly Color", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Cull Off
        ZWrite Off
        Blend One OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct Input { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct Output { float4 vertex : SV_POSITION; float2 uv : TEXCOORD0; };
            sampler2D _MainTex;
            float _Opacity;
            float _PhillyColor;
            float4 _Adjustment;
            Output vert(Input input)
            {
                Output output;
                output.vertex = UnityObjectToClipPos(input.vertex);
                output.uv = input.uv;
                #if UNITY_UV_STARTS_AT_TOP
                output.uv.y = 1 - output.uv.y;
                #endif
                return output;
            }
            float4 frag(Output input) : SV_Target
            {
                float4 color = tex2D(_MainTex, input.uv);
                if ((_PhillyColor > 0 || any(_Adjustment != 0)) && color.a > 0)
                {
                    float4 adjustment = _PhillyColor > 0 ? float4(-26, -16, -5, 0) : _Adjustment;
                    float3 source = color.rgb / color.a + adjustment.z / 255;
                    float c = cos(adjustment.x * 3.14159265359 / 180);
                    float s = sin(adjustment.x * 3.14159265359 / 180);
                    float3 hue = float3(
                        dot(source, float3(.299 + .701*c - .299*s, .587 - .587*c - .587*s, .114 - .114*c + .886*s)),
                        dot(source, float3(.299 - .299*c + .143*s, .587 + .413*c + .140*s, .114 - .114*c - .283*s)),
                        dot(source, float3(.299 - .299*c - .701*s, .587 - .587*c + .587*s, .114 + .886*c + .114*s)));
                    float contrast = 1 + adjustment.w / 100;
                    if (contrast > 1) contrast = ((.00852259 * exp(4.76454 * (contrast - 1))) * 1.01 - .0086078159) * 10 + 1;
                    hue = (hue - .25) * contrast + .25;
                    float saturation = 1 + adjustment.y * (adjustment.y > 0 ? 3 : 1) / 100;
                    color.rgb = (hue * saturation + dot(hue, float3(.2126, .7152, .0722)) * (1 - saturation)) * color.a;
                }
                return color * _Opacity;
            }
            ENDCG
        }
    }
}

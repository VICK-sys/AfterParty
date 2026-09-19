Shader "Unity Party/Week 2 Composite"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Opacity ("Opacity", Float) = 1
        _Lighting ("Lighting", Color) = (1,1,1,1)
        _PhillyColor ("Philly Color", Float) = 0
        _RimMask ("Rim Mask", 2D) = "black" {}
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
            sampler2D _RimMask;
            float4 _MainTex_TexelSize;
            float _Opacity;
            float _PhillyColor;
            float4 _Adjustment;
            float4 _Lighting;
            float4x4 _Affine;
            float _AffineEnabled;
            float4 _Rim, _RimAdjustment, _RimColor, _RimDirection;
            Output vert(Input input)
            {
                Output output;
                output.vertex = UnityObjectToClipPos(lerp(input.vertex, mul(_Affine, input.vertex), _AffineEnabled));
                output.uv = input.uv;
                #if UNITY_UV_STARTS_AT_TOP
                output.uv.y = 1 - output.uv.y;
                #endif
                return output;
            }
            float4 frag(Output input) : SV_Target
            {
                float4 color = tex2D(_MainTex, input.uv);
                float rim = 0;
                if (_Rim.w > 0 && color.a > 0)
                {
                    float3 luma = float3(.2126, .7152, .0722);
                    float center = dot(color.rgb, luma);
                    float2 px = max(abs(_MainTex_TexelSize.xy), fwidth(input.uv));
                    float downSign = -1;
                    #if UNITY_UV_STARTS_AT_TOP
                    downSign = 1;
                    #endif
                    float right = dot(tex2D(_MainTex, input.uv + float2(px.x, 0)).rgb, luma);
                    float down = dot(tex2D(_MainTex, input.uv + float2(0, downSign * px.y)).rgb, luma);
                    float diagonal = dot(tex2D(_MainTex, input.uv + float2(px.x, downSign * px.y)).rgb, luma);
                    float delta = max((abs(right-center)+abs(down-center)+abs(diagonal-center)*.7)/2.7*2, .00001);
                    float2 maskUV = input.uv;
                    #if UNITY_UV_STARTS_AT_TOP
                    maskUV.y = 1 - maskUV.y;
                    #endif
                    float threshold = tex2D(_RimMask, maskUV).b > 0 ? _Rim.z : _Rim.y;
                    float intensity = smoothstep(threshold-delta, threshold+delta, center);
                    float2 direction = _RimDirection.xy;
                    #if UNITY_UV_STARTS_AT_TOP
                    direction.y = -direction.y;
                    #endif
                    float2 checkedUV = input.uv + direction * _Rim.x * abs(_MainTex_TexelSize.xy);
                    float shadow = all(checkedUV > 0) && all(checkedUV < 1) ? tex2D(_MainTex, checkedUV).a : 0;
                    rim = (1 - shadow) * intensity;
                }
                if ((_Rim.w > 0 || _PhillyColor > 0 || any(_Adjustment != 0)) && color.a > 0)
                {
                    float4 adjustment = _Rim.w > 0 ? _RimAdjustment : _PhillyColor > 0 ? float4(-26, -16, -5, 0) : _Adjustment;
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
                color.rgb += _RimColor.rgb * rim * color.a;
                color.rgb *= _Lighting.rgb;
                return color * _Opacity;
            }
            ENDCG
        }
    }
}

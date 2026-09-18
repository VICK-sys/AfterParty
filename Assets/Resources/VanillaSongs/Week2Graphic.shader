Shader "Unity Party/Week 2 Graphic"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Opacity ("Opacity", Float) = 1
        _Rain ("Rain", Float) = 0
        _Clock ("Clock", Float) = 1
        _Tint ("Tint", Color) = (1,1,1,1)
        _BuildingFade ("Building Fade", Float) = 0
        _DstBlend ("Destination Blend", Float) = 10
        _RimMask ("Rim Mask", 2D) = "black" {}
        _Rim ("Rim Settings", Vector) = (0,0,0,0)
        _RimAdjustment ("Rim Color Adjustment", Vector) = (0,0,0,0)
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Cull Off
        ZWrite Off
        Blend SrcAlpha [_DstBlend], One OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct Input { float4 vertex : POSITION; float2 uv : TEXCOORD0; float4 color : COLOR; float4 addition : TEXCOORD1; };
            struct Output { float4 vertex : SV_POSITION; float2 uv : TEXCOORD0; float4 color : COLOR; float4 addition : TEXCOORD1; float2 world : TEXCOORD2; };
            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            float4 _FrameBounds;
            float _Opacity, _Rain, _Clock;
            float4 _Tint;
            float _BuildingFade;
            float3 _Wiggle;
            sampler2D _RimMask;
            float4 _Rim, _RimAdjustment;
            Output vert(Input input)
            {
                Output output;
                output.vertex = UnityObjectToClipPos(input.vertex);
                output.world = mul(unity_ObjectToWorld, input.vertex).xy * float2(100, -100);
                output.uv = input.uv;
                output.color = input.color;
                output.addition = input.addition;
                return output;
            }
            float randomValue(float2 value)
            {
                return frac(sin(dot(value - floor(value / 1000) * 1000, float2(12.9898, 78.233))) * 43758.5453);
            }
            float rainDistance(float2 p, float scale)
            {
                p *= 0.1;
                p.x += p.y * 0.1;
                p.y -= _Clock * 500 / scale;
                p.y *= 0.03;
                float ix = floor(p.x);
                p.y += (ix - floor(ix / 2) * 2) * 0.5 + (randomValue(ix.xx) - 0.5) * 0.3;
                float2 index = float2(ix, floor(p.y));
                p -= index;
                p.x += (randomValue(index.yx) * 2 - 1) * 0.35;
                float2 a = abs(p - 0.5);
                return randomValue(index) < lerp(1, 0.1, 0.4) ? 1 : max(a.x * 0.8, a.y * 0.5) - 0.1;
            }
            float3 adjustColor(float3 color, float4 adjustment)
            {
                color += adjustment.z / 255;
                float c = cos(adjustment.x * 3.14159265359 / 180);
                float s = sin(adjustment.x * 3.14159265359 / 180);
                float3 hue = float3(
                    dot(color, float3(.299+.701*c-.299*s,.587-.587*c-.587*s,.114-.114*c+.886*s)),
                    dot(color, float3(.299-.299*c+.143*s,.587+.413*c+.140*s,.114-.114*c-.283*s)),
                    dot(color, float3(.299-.299*c-.701*s,.587-.587*c+.587*s,.114+.886*c+.114*s)));
                float contrast = 1 + adjustment.w / 100;
                if (contrast > 1) contrast = ((.00852259 * exp(4.76454 * (contrast - 1))) * 1.01 - .0086078159) * 10 + 1;
                hue = (hue - .25) * contrast + .25;
                float saturation = 1 + adjustment.y * (adjustment.y > 0 ? 3 : 1) / 100;
                return hue * saturation + dot(hue, float3(.2126,.7152,.0722)) * (1 - saturation);
            }
            float4 frag(Output input) : SV_Target
            {
                float2 uv = input.uv;
                if (_Wiggle.z > 0)
                {
                    float2 p = float2(uv.x, 1 - uv.y);
                    p.x = floor(p.x / _MainTex_TexelSize.x) * _MainTex_TexelSize.x;
                    p.y += floor(sin(p.x * _Wiggle.y + (_Clock - 1) * _Wiggle.x) * _Wiggle.z / _MainTex_TexelSize.y) * _MainTex_TexelSize.y;
                    p.y = floor(p.y / _MainTex_TexelSize.y) * _MainTex_TexelSize.y;
                    p.x += floor(sin(p.y * _Wiggle.y * .5 + (_Clock - 1) * _Wiggle.x * .5) * _Wiggle.z * .5 / _MainTex_TexelSize.x) * _MainTex_TexelSize.x;
                    uv = float2(p.x, 1 - p.y);
                }
                float3 addition = 0;
                float sum = 0;
                if (_Rain > 0)
                {
                    float scales[4] = { 1, 1.8, 2.6, 4.8 };
                    float2 origin = saturate((input.uv - _FrameBounds.xy) / _FrameBounds.zw) * float2(1280, 720);
                    float2 world = origin;
                    for (int i = 0; i < 4; i++)
                    {
                        float scale = scales[i];
                        float r = rainDistance(world * scale / 7.2 + 500 * i, scale);
                        if (r < 0)
                        {
                            float value = (1 - exp(r * 5)) / scale * 2;
                            world += float2(10, -2) * value * 7.2;
                            addition += float3(0.1, 0.15, 0.2) * value;
                            sum += (1 - sum) * 0.75;
                        }
                    }
                    uv += (world - origin) / float2(1280, -720);
                }
                float4 sampled = tex2D(_MainTex, uv);
                if (_Rim.w > 0 && sampled.a > 0)
                {
                    float3 luma = float3(.2126,.7152,.0722);
                    float center = dot(sampled.rgb, luma);
                    float2 px = max(_MainTex_TexelSize.xy, fwidth(uv));
                    float right = dot(tex2D(_MainTex, uv + float2(px.x,0)).rgb, luma);
                    float down = dot(tex2D(_MainTex, uv - float2(0,px.y)).rgb, luma);
                    float diagonal = dot(tex2D(_MainTex, uv + float2(px.x,-px.y)).rgb, luma);
                    float delta = max((abs(right-center)+abs(down-center)+abs(diagonal-center)*.7)/2.7*2,.00001);
                    float threshold = tex2D(_RimMask, uv).b > 0 ? _Rim.z : _Rim.y;
                    float intensity = smoothstep(threshold-delta,threshold+delta,center);
                    float2 checkedUV = uv + float2(0,_Rim.x*_MainTex_TexelSize.y);
                    float shadow = checkedUV.x > _FrameBounds.x && checkedUV.x < _FrameBounds.x+_FrameBounds.z
                        && checkedUV.y < _FrameBounds.y && checkedUV.y > _FrameBounds.y+_FrameBounds.w ? tex2D(_MainTex,checkedUV).a : 0;
                    sampled.rgb = adjustColor(sampled.rgb,_RimAdjustment) + float3(82,53,29)/255*(1-shadow)*intensity;
                }
                float4 color = sampled * input.color + input.addition;
                color.a = sampled.a == 0 ? 0 : saturate(color.a) * _Opacity;
                color.rgb = lerp(color.rgb + addition, float3(0.4, 0.5019608, 0.8), 0.1 * sum);
                color *= _Tint;
                if (_BuildingFade > 0 && color.a > 0)
                {
                    float4 faded = saturate(float4(color.rgb * color.a, color.a) - _BuildingFade);
                    color = float4(faded.a > 0 ? faded.rgb / faded.a : 0, faded.a);
                }
                return color;
            }
            ENDCG
        }
    }
}

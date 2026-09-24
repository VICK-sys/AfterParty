Shader "Friday Fight Funkin'/Weekend Rain"
{
    Properties { _MainTex ("Texture", 2D) = "white" {} }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            float4 _View;
            float4 _RainColor;
            float _RainTime, _Intensity;
            float randomValue(float2 p)
            {
                return frac(sin(dot(p - floor(p / 1000) * 1000, float2(12.9898, 78.233))) * 43758.5453);
            }
            float rainDistance(float2 p, float scale)
            {
                p *= .1;
                p.x += p.y * .1;
                p.y -= _RainTime * 500 / scale;
                p.y *= .03;
                float ix = floor(p.x);
                p.y += (ix - floor(ix / 2) * 2) * .5 + (randomValue(ix.xx) - .5) * .3;
                float2 index = floor(p);
                p -= index;
                p.x += (randomValue(index.yx) * 2 - 1) * .35;
                float2 a = abs(p - .5);
                return randomValue(index) < lerp(1, .1, _Intensity) ? 1 : max(a.x * .8, a.y * .5) - .1;
            }
            float4 frag(v2f_img input) : SV_Target
            {
                float2 position = _View.xy + (input.uv - .5) * _View.zw * float2(1, -1);
                float2 original = position;
                float3 addition = 0;
                float sum = 0;
                const float scales[4] = { 1, 1.8, 2.6, 4.8 };
                for (int i = 0; i < 4; i++)
                {
                    float scale = scales[i];
                    float distance = rainDistance(position * scale / 3.6 + 500 * i, scale);
                    if (distance < 0)
                    {
                        float value = (1 - exp(distance * 5)) / scale * 2;
                        position += float2(10, -2) * value * 3.6;
                        addition += float3(.1, .15, .2) * value;
                        sum += (1 - sum) * .75;
                    }
                }
                float2 uv = input.uv + (position - original) / _View.zw * float2(1, -1);
                float4 color = tex2D(_MainTex, uv);
                color.rgb = lerp(color.rgb + addition, _RainColor.rgb, .1 * sum);
                return color;
            }
            ENDCG
        }
    }
}

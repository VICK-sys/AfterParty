Shader "Unity Party/Erect Color"
{
    Properties
    {
        [PerRendererData] _MainTex ("Texture", 2D) = "white" {}
        _Brightness ("Brightness", Float) = 0
        _Hue ("Hue", Float) = 0
        _Contrast ("Contrast", Float) = 1
        _DstBlend ("Destination Blend", Float) = 10
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "CanUseSpriteAtlas"="True" }
        Cull Off
        Lighting Off
        ZWrite Off
        Blend SrcAlpha [_DstBlend]
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct Input { float4 vertex : POSITION; float2 uv : TEXCOORD0; fixed4 color : COLOR; };
            struct Output { float4 vertex : SV_POSITION; float2 uv : TEXCOORD0; fixed4 color : COLOR; };
            sampler2D _MainTex;
            float _Brightness;
            float _Hue;
            float _Contrast;
            Output vert(Input input)
            {
                Output output;
                output.vertex = UnityObjectToClipPos(input.vertex);
                output.uv = input.uv;
                output.color = input.color;
                return output;
            }
            fixed4 frag(Output input) : SV_Target
            {
                float4 color = tex2D(_MainTex, input.uv) * input.color;
                float c = cos(_Hue);
                float s = sin(_Hue);
                float3 source = color.rgb + _Brightness;
                color.rgb = float3(
                    dot(source, float3(.299 + .701*c - .299*s, .587 - .587*c - .587*s, .114 - .114*c + .886*s)),
                    dot(source, float3(.299 - .299*c + .143*s, .587 + .413*c + .140*s, .114 - .114*c - .283*s)),
                    dot(source, float3(.299 - .299*c - .701*s, .587 - .587*c + .587*s, .114 + .886*c + .114*s)));
                color.rgb = (color.rgb - .25) * _Contrast + .25;
                return color;
            }
            ENDCG
        }
    }
}

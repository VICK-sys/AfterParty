Shader "UnityParty/DiamondWipe"
{
    Properties
    {
        [PerRendererData] _MainTex ("Texture", 2D) = "white" {}
        _Progress ("Progress", Range(0, 1)) = 0
        _Revealing ("Revealing", Float) = 0
        _Resolution ("Resolution", Vector) = (1280,720,0,0)
    }
    SubShader
    {
        Tags { "Queue"="Overlay" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" }
        Cull Off
        ZWrite Off
        ZTest Always
        Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct Input
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };
            struct Output
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
            };
            float _Progress;
            float _Revealing;
            float4 _Resolution;
            Output vert(Input input)
            {
                Output output;
                output.vertex = UnityObjectToClipPos(input.vertex);
                output.uv = input.uv;
                return output;
            }
            float4 frag(Output input) : SV_Target
            {
                float2 fragCoord = input.uv * _Resolution.xy;
                float xFraction = frac(fragCoord.x / 10.0);
                float yFraction = frac(fragCoord.y / 10.0);
                float2 uv = fragCoord / _Resolution.xy;
                float xDistance = abs(xFraction - 0.5);
                float yDistance = abs(yFraction - 0.5);
                float covered = xDistance + yDistance + uv.x + uv.y <= _Progress * 3.0 ? 1.0 : 0.0;
                if (_Progress <= 0.0) covered = 0.0;
                if (_Progress >= 1.0) covered = 1.0;
                return float4(0, 0, 0, lerp(covered, 1.0 - covered, _Revealing));
            }
            ENDCG
        }
    }
}

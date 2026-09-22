Shader "Funkin/RetryFade"
{
    Properties
    {
        [PerRendererData] _MainTex ("Texture", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "Queue"="Overlay" "RenderType"="Transparent" }
        Cull Off
        ZWrite Off
        ZTest Always
        Blend One One
        BlendOp RevSub
        ColorMask RGB
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct Input
            {
                float4 vertex : POSITION;
                fixed4 color : COLOR;
            };
            struct Output
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
            };
            Output vert(Input input)
            {
                Output output;
                output.vertex = UnityObjectToClipPos(input.vertex);
                output.color = input.color;
                return output;
            }
            fixed4 frag(Output input) : SV_Target
            {
                return fixed4(input.color.aaa, 0);
            }
            ENDCG
        }
    }
}

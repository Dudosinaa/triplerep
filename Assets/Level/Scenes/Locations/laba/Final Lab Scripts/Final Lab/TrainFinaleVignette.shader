Shader "FinalLab/Ending Vignette"
{
    Properties { _Closure ("Closure", Range(0, 1)) = 0 }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Cull Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct Vertex { float4 position : SV_POSITION; float2 uv : TEXCOORD0; };
            float _Closure;
            Vertex vert(appdata_img input)
            {
                Vertex output;
                output.position = UnityObjectToClipPos(input.vertex);
                output.uv = input.texcoord;
                return output;
            }
            fixed4 frag(Vertex input) : SV_Target
            {
                float radius = length(2.0 * input.uv - 1.0);
                float edge = lerp(1.45, -0.23, _Closure);
                return fixed4(0, 0, 0, smoothstep(edge, edge + 0.22, radius));
            }
            ENDCG
        }
    }
}

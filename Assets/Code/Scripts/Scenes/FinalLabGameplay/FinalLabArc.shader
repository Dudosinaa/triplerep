Shader "FinalLab/K17 Arc"
{
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent+10" "RenderType" = "Transparent" }
        Pass
        {
            Tags { "LightMode" = "SRPDefaultUnlit" }
            Cull Off
            ZWrite Off
            Blend SrcAlpha One

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Interpolators
            {
                float4 position : SV_POSITION;
                half4 color : COLOR;
            };

            Interpolators Vert(float4 position : POSITION, half4 color : COLOR)
            {
                Interpolators result;
                result.position = TransformObjectToHClip(position.xyz);
                result.color = color;
                return result;
            }

            half4 Frag(Interpolators input) : SV_Target { return input.color; }
            ENDHLSL
        }
    }
}

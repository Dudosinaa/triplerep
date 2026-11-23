Shader "FinalLab/Rescue Outline"
{
    Properties
    {
        _Color ("Color", Color) = (1, 0.76, 0.1, 1)
        _Width ("Width", Float) = 0.009
    }
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Opaque" "Queue" = "Geometry+25" }
        Pass
        {
            Tags { "LightMode" = "SRPDefaultUnlit" }
            Cull Front
            ZWrite Off
            ZTest LEqual
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float _Width;
            CBUFFER_END

            float4 Vert(float4 position : POSITION, float3 normal : NORMAL) : SV_POSITION
            {
                float3 positionWS = TransformObjectToWorld(position.xyz);
                float3 outward = TransformObjectToWorldNormal(normal);
                return TransformWorldToHClip(positionWS + outward * _Width);
            }

            half4 Frag() : SV_Target { return _Color; }
            ENDHLSL
        }
    }
}

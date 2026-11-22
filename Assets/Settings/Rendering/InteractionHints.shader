Shader "Hidden/InteractionHints"
{
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        float4 MaskVertex(float4 positionOS : POSITION) : SV_POSITION
        {
            return TransformObjectToHClip(positionOS.xyz);
        }

        half4 MaskFragment() : SV_Target
        {
            return 1;
        }
        ENDHLSL

        Pass
        {
            Name "Outline Mask"
            Cull Off
            ZWrite Off
            ZTest LEqual
            ColorMask R

            HLSLPROGRAM
            #pragma vertex MaskVertex
            #pragma fragment MaskFragment
            ENDHLSL
        }

        Pass
        {
            Name "Composite"
            Cull Off
            ZWrite Off
            ZTest Always
            Blend One OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Composite
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            half4 _OutlineColor;
            int _OutlineWidth;

            half OutlineMask(float2 uv)
            {
                return SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_PointClamp, uv).r;
            }

            half4 Composite(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord;
                half center = OutlineMask(uv);
                half expanded = center;
                float2 texel = _BlitTexture_TexelSize.xy;

                [loop]
                for (int radius = 1; radius <= _OutlineWidth; ++radius)
                {
                    float2 offset = radius * texel;
                    expanded = max(expanded, OutlineMask(uv + float2(offset.x, 0)));
                    expanded = max(expanded, OutlineMask(uv - float2(offset.x, 0)));
                    expanded = max(expanded, OutlineMask(uv + float2(0, offset.y)));
                    expanded = max(expanded, OutlineMask(uv - float2(0, offset.y)));

                    offset *= 0.7071;
                    expanded = max(expanded, OutlineMask(uv + offset));
                    expanded = max(expanded, OutlineMask(uv - offset));
                    expanded = max(expanded, OutlineMask(uv + float2(offset.x, -offset.y)));
                    expanded = max(expanded, OutlineMask(uv + float2(-offset.x, offset.y)));
                }

                half outline = saturate(expanded - center) * _OutlineColor.a;
                return half4(outline * _OutlineColor.rgb, outline);
            }
            ENDHLSL
        }
    }
}

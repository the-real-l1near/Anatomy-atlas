Shader "Hidden/TranslucentUIFX/Blur"
{
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline" = "UniversalPipeline"}
        LOD 100

        Pass
        {
            Name "Blur"
            ZTest Always ZWrite Off Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            float2 _TranslucentBlurOffset;

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float2 uv = input.texcoord;
                float2 offset = _TranslucentBlurOffset;

                // 3x3 tent filter. Repeated at increasing radii this produces a smooth
                // bokeh-like blur without the diagonal star artifacts of a box kernel.
                half4 color = SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, uv) * 4.0h;

                color += SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, uv + float2( offset.x, 0.0)) * 2.0h;
                color += SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, uv + float2(-offset.x, 0.0)) * 2.0h;
                color += SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, uv + float2(0.0,  offset.y)) * 2.0h;
                color += SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, uv + float2(0.0, -offset.y)) * 2.0h;

                color += SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, uv + float2( offset.x,  offset.y));
                color += SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, uv + float2( offset.x, -offset.y));
                color += SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, uv + float2(-offset.x,  offset.y));
                color += SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, uv + float2(-offset.x, -offset.y));

                return color * 0.0625h;
            }
            ENDHLSL
        }
    }
}

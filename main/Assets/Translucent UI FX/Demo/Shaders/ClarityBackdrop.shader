Shader "TranslucentUIFX/Demo/Clarity Backdrop"
{
    Properties
    {
        _DemoTime ("Time", Float) = 0
        _Artwork ("Artwork", 2D) = "black" {}
        _HasArtwork ("Artwork available", Float) = 0
        _ArtworkScale ("Cover scale", Vector) = (1,1,0,0)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Background" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Cull Off ZWrite Off ZTest Always
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };
            TEXTURE2D(_Artwork); SAMPLER(sampler_Artwork);
            CBUFFER_START(UnityPerMaterial)
                float _DemoTime;
                float _HasArtwork;
                float4 _ArtworkScale;
            CBUFFER_END
            Varyings Vert(Attributes v)
            {
                Varyings o; o.positionCS = TransformObjectToHClip(v.positionOS.xyz); o.uv = v.uv; return o;
            }
            half4 Frag(Varyings i) : SV_Target
            {
                float2 p = i.uv;
                // A small, slow camera drift keeps edges moving behind stationary glass.
                // Overscan prevents exposed borders at every phase of the animation.
                float t = _DemoTime * 0.075;
                float2 drift = float2(sin(t), sin(t * 0.71)) * 0.012;
                float2 uv = (p - 0.5) * _ArtworkScale.xy * 0.96 + 0.5 + drift;
                float3 color = SAMPLE_TEXTURE2D(_Artwork, sampler_Artwork, uv).rgb;
                float3 fallback = lerp(float3(0.025, 0.10, 0.20), float3(0.004, 0.012, 0.035), p.y);
                color = lerp(fallback, color, _HasArtwork);
                // Keep the header and control dock readable without flattening the material stage.
                float header = smoothstep(0.67, 0.96, p.y);
                float dock = 1.0 - smoothstep(0.13, 0.38, p.y);
                color *= 0.88 - header * 0.40 - dock * 0.52;
                return half4(color, 1);
            }
            ENDHLSL
        }
    }
}

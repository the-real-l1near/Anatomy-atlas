// Copyright (C) 2019-2021 Alexander Bogarsukov. All rights reserved.
// See the LICENSE.md file in the project root for more information.

Shader "Hidden/UnityFx/OutlineColor.URP"
{
    HLSLINCLUDE

        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        TEXTURE2D(_MainTex);
        SAMPLER(sampler_MainTex);

        half _Cutoff;

        struct Attributes
        {
            float4 positionOS : POSITION;
            float2 uv : TEXCOORD0;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };

        struct Varyings
        {
            float4 positionHCS : SV_POSITION;
            float2 uv : TEXCOORD0;
            UNITY_VERTEX_INPUT_INSTANCE_ID
            UNITY_VERTEX_OUTPUT_STEREO
        };

        Varyings Vert(Attributes input)
        {
            Varyings output;

            UNITY_SETUP_INSTANCE_ID(input);
            UNITY_TRANSFER_INSTANCE_ID(input, output);
            UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

            output.positionHCS = TransformObjectToHClip(input.positionOS.xyz);
            output.uv = input.uv;

            return output;
        }

        half4 FragmentSimple(Varyings input) : SV_Target
        {
            return 1;
        }

        half4 FragmentAlphaTest(Varyings input) : SV_Target
        {
            half4 c = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
            clip(c.a - _Cutoff);
            return 1;
        }

    ENDHLSL

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }

        Cull Off
        ZWrite Off
        ZTest LEqual

        Pass
        {
            Name "Opaque"

            HLSLPROGRAM

            #pragma multi_compile_instancing
            #pragma vertex Vert
            #pragma fragment FragmentSimple

            ENDHLSL
        }

        Pass
        {
            Name "Transparent"

            HLSLPROGRAM

            #pragma multi_compile_instancing
            #pragma vertex Vert
            #pragma fragment FragmentAlphaTest

            ENDHLSL
        }
    }
}
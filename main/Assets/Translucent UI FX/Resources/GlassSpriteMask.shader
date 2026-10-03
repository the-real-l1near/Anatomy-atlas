Shader "Hidden/TranslucentUIFX/Sprite Mask"
{
    Properties { _MainTex ("Sprite", 2D) = "white" {} }
    SubShader
    {
        Pass
        {
            Cull Off ZWrite Off ZTest Always Blend Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            struct Vertex { float4 position : POSITION; float2 uv : TEXCOORD0; };
            struct Fragment { float4 position : SV_POSITION; float2 uv : TEXCOORD0; };
            Fragment Vert(Vertex v)
            {
                Fragment o;
                o.position = float4(v.position.xy * 2.0 - 1.0, 0, 1);
                #if UNITY_UV_STARTS_AT_TOP
                o.position.y = -o.position.y;
                #endif
                o.uv = v.uv;
                return o;
            }
            float4 Frag(Fragment i) : SV_Target { return tex2D(_MainTex, i.uv).aaaa; }
            ENDHLSL
        }
    }
}

Shader "UI/TranslucentUIFX"
{
    Properties
    {
        [HideInInspector] _CaptureUnavailable("Background unavailable", Float) = 0
        [HideInInspector] _GlassSourceTex ("Camera source", 2D) = "white" {}
        [HideInInspector] _GlassBlurredTex ("Camera blur", 2D) = "white" {}
        [HideInInspector] _SpriteDistanceTex ("Sprite distance", 2D) = "white" {}
        [HideInInspector] _FusionSpriteFields ("Fusion sprite fields", 2DArray) = "" {}
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        [HideInInspector] _TextureSampleAdd ("Texture Sample Add", Vector) = (0,0,0,0)

        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255

        _ColorMask ("Color Mask", Float) = 15

        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
        
        // TranslucentFX properties
        _GlassIntensity("Glass Intensity", Range(0, 1)) = 1.0
        _BlurStrength("Blur Strength", Range(0, 1)) = 1.0
        _LuminosityBoost("Luminosity Boost", Range(0, 1)) = 0.0
        _TintColor("FX Tint Color", Color) = (0.8, 0.9, 1.0, 0.3)
        _FrostAmount("Frost Brightness", Range(0, 1)) = 0.0
        _NoiseAmount("Film Grain", Range(0, 1)) = 0.05
        _RefractionAmount("Refraction Distortion", Range(-0.2, 0.2)) = 0.02
        _RefractiveIndex("Refractive Index", Range(1, 2.5)) = 1.5
        _SphericalDistortion("Spherical Distortion", Float) = 0.0
        _ChromaticAberration("Chromatic Aberration", Range(0, 0.1)) = 0.0
        [HideInInspector] _AlphaGradientStability("Alpha Gradient Stability", Float) = 1.0
        _SpecularGlare("Specular Glare", Range(0, 1)) = 0.5
        _SpecularSharpness("Specular Sharpness", Range(8, 256)) = 96
        _RimDepth("Rim Depth", Range(0, 1)) = 0.35
        _LightDirection("Light Direction", Vector) = (-0.5, 0.55, 0, 0)
        
        _Brightness("Brightness", Range(0, 5)) = 1.0
        _Saturation("Saturation", Range(0, 5)) = 1.0
        _Contrast("Contrast", Range(0, 5)) = 1.0
        
        _AutoReadability("Auto Readability", Range(0, 1)) = 0.0
        
        _EdgeColor("Edge Highlight Color", Color) = (1,1,1,1)
        _EdgeWidth("Edge Highlight Width", Range(0, 1)) = 0.05
        _EdgePower("Edge Highlight Power", Range(0.1, 10)) = 2.0
        _EdgeShape("Edge Shape", Float) = 0.0
        _EdgeRounding("Edge Rounding Radius", Range(0, 0.5)) = 0.1
        _CornerContinuity("Corner Continuity", Range(0, 1)) = 0.75
        _ProceduralShape("Procedural Shape", Float) = 0.0
        _RectSize("Rect Size", Vector) = (100,100,0,0)
        [HideInInspector] _FusionEnabled("Fusion Enabled", Float) = 0.0
        [HideInInspector] _FusionCount("Fusion Count", Float) = 0.0
        [HideInInspector] _FusionSoftness("Fusion Softness", Float) = 28.0
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
            "RenderPipeline" = "UniversalPipeline"
        }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            Name "Default"
        HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 4.5
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS   : POSITION;
                float4 color        : COLOR;
                float2 texcoord     : TEXCOORD0;
                float4 shapeCoord   : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
                float4 positionNDC  : TEXCOORD1;
                float4 color        : COLOR;
                float2 texcoord     : TEXCOORD0;
                float2 shapeCoord   : TEXCOORD2;
                float shadowFlag : TEXCOORD4;
                float4 worldPosition : TEXCOORD3;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            CBUFFER_START(UnityPerMaterial)
            float4 _SpriteDistanceTex_TexelSize;
            float _LensDepth;
            float _CaptureBound;
            float _CaptureUnavailable;
            float4 _CaptureOptions;
            float4 _ShadowSettings;
            float4 _ShadowColor;
            float4 _SpriteDrawingRect;
            float4 _SurfaceOptics;
            float4 _SurfaceLighting;
            float4 _PointLightPosition;
            float4 _LipBands;
            float4 _LipLightColor;
            float4 _LipShadowColor;
            float4 _Color;
            float _GlassIntensity;
            float _BlurStrength;
            float _LuminosityBoost;
            float4 _TintColor;
            float _FrostAmount;
            float _NoiseAmount;
            float _RefractionAmount;
            float _RefractiveIndex;
            float _SphericalDistortion;
            float _ChromaticAberration;
            float _AlphaGradientStability;
            float _SpecularGlare;
            float _SpecularSharpness;
            float _RimDepth;
            float4 _LightDirection;
            
            float _Brightness;
            float _Saturation;
            float _Contrast;
            
            float _AutoReadability;
            
            float4 _EdgeColor;
            float _EdgeWidth;
            float _EdgePower;
            float _EdgeShape;
            float _EdgeRounding;
            float _CornerContinuity;
            float4 _CornerRadii;
            float4 _CornerProfiles;
            float _CustomCorners;
            float _ProceduralShape;
            float _SpriteShape;
            float _SpriteAlphaThreshold;
            float4 _SpriteUvRow0;
            float4 _SpriteUvRow1;
            float4 _RectSize;
            float4 _FusionFieldSize;
            float _FusionEnabled;
            float _FusionCount;
            float _FusionSoftness;
            float _FusionSupport;

            
            float4 _ClipRect;
            float4 _TextureSampleAdd;
            CBUFFER_END
            
            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            float4 _MainTex_ST;
            struct FusionElement { float4 rect; float4 data; float4 transform; float4 corners; float4 metrics; float4 tint; float4 optics; float4 surface; float4 style; float4 profiles; float4 graphic; float4 bands; };
            struct SurfaceStyle { float4 tint; float4 optics; float4 surface; float4 style; float4 graphic; float4 bands; float2 center; };
            SurfaceStyle BlendStyle(SurfaceStyle a, SurfaceStyle b, float weight)
            {
                SurfaceStyle o;
                o.graphic = lerp(a.graphic, b.graphic, weight); o.bands = lerp(a.bands, b.bands, weight);
                o.tint = lerp(a.tint, b.tint, weight); o.optics = lerp(a.optics, b.optics, weight);
                o.surface = lerp(a.surface, b.surface, weight); o.style = lerp(a.style, b.style, weight); o.center = lerp(a.center, b.center, weight);
                return o;
            }
            StructuredBuffer<FusionElement> _FusionElements;
            TEXTURE2D(_GlassSourceTex); SAMPLER(sampler_GlassSourceTex);
            TEXTURE2D(_GlassBlurredTex); SAMPLER(sampler_GlassBlurredTex);
            TEXTURE2D_ARRAY(_FusionSpriteFields);
            SAMPLER(sampler_FusionSpriteFields);
            TEXTURE2D(_SpriteDistanceTex);
            SAMPLER(sampler_SpriteDistanceTex);
            
            TEXTURE2D(_TranslucentUI_BlurredTex);
            SAMPLER(sampler_TranslucentUI_BlurredTex);

            TEXTURE2D(_TranslucentUI_SourceTex);
            SAMPLER(sampler_TranslucentUI_SourceTex);
            float _TranslucentUI_MaxBlurStrength;
            float _TranslucentUI_SceneViewPreview;

            half4 SourceSample(float2 uv)
            {
                return _CaptureBound > 0.5 ? SAMPLE_TEXTURE2D(_GlassSourceTex, sampler_GlassSourceTex, uv) : SAMPLE_TEXTURE2D(_TranslucentUI_SourceTex, sampler_TranslucentUI_SourceTex, uv);
            }
            half4 BlurSample(float2 uv)
            {
                return _CaptureBound > 0.5 ? SAMPLE_TEXTURE2D(_GlassBlurredTex, sampler_GlassBlurredTex, uv) : SAMPLE_TEXTURE2D(_TranslucentUI_BlurredTex, sampler_TranslucentUI_BlurredTex, uv);
            }

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.positionNDC = ComputeScreenPos(output.positionCS);
                output.worldPosition = input.positionOS;
                
                output.texcoord = input.texcoord;
                output.shapeCoord = input.shapeCoord.xy;
                output.shadowFlag = input.shapeCoord.z;
                output.color = input.color * _Color;

                return output;
            }

            float2 PolygonVertex(int index, int count, float2 halfSize)
            {
                if (count == 3)
                    return index == 0 ? float2(0, halfSize.y) : index == 1 ? -halfSize : float2(halfSize.x, -halfSize.y);
                float angle = index * 1.0471975512;
                return float2(cos(angle) * halfSize.x, sin(angle) * halfSize.y / 0.8660254);
            }

            float PolygonDistance(float2 p, float2 halfSize, int count)
            {
                float nearest = 1e10;
                bool inside = true;
                [unroll] for (int i = 0; i < 6; i++)
                {
                    // Fixed bounds keep Metal's unrolled paths fully initialized.
                    if (i < count)
                    {
                        float2 a = PolygonVertex(i, count, halfSize);
                        int next = i + 1 < count ? i + 1 : 0;
                        float2 b = PolygonVertex(next, count, halfSize);
                        float2 edge = b - a;
                        float2 v = p - a;
                        float2 closest = v - edge * saturate(dot(v, edge) / max(dot(edge, edge), 0.000001));
                        nearest = min(nearest, dot(closest, closest));
                        inside = inside && edge.x * v.y - edge.y * v.x >= 0;
                    }
                }
                return sqrt(nearest) * (inside ? -1.0 : 1.0);
            }

            float PrimitiveDistance(float2 p, float2 size, float shape, float smoothing, float4 corners)
            {
                float2 halfSize = max(size, float2(0.001, 0.001)) * 0.5;
                float shortHalf = min(halfSize.x, halfSize.y);
                float distance = 0.0;
                if (shape > 5.5)
                    distance = PolygonDistance(p, halfSize, shape > 6.5 ? 6 : 3);
                else if (shape > 2.5 && shape < 3.5)
                    distance = (dot(abs(p) / halfSize, float2(1, 1)) - 1.0) * shortHalf * 0.70710678;
                else if (shape > 0.5 && shape < 1.5)
                    distance = length(p) - shortHalf;
                else
                {
                    float corner = p.y >= 0.0 ? (p.x < 0.0 ? corners.x : corners.y) : (p.x < 0.0 ? corners.w : corners.z);
                    float radius = shape < 0.5 ? 0.0 : shape > 3.5 && shape < 4.5 ? shortHalf : saturate(corner * 2.0) * shortHalf;
                    float2 q = abs(p) - halfSize + radius;
                    float2 outside = max(q, 0.0);
                    float exponent = shape > 4.5 ? lerp(2.0, 4.0, saturate(smoothing)) : 2.0;
                    float curved = pow(pow(outside.x, exponent) + pow(outside.y, exponent), 1.0 / exponent);
                    distance = curved + min(max(q.x, q.y), 0.0) - radius;
                }
                return distance;
            }

            float CustomCornersDistance(float2 p, float2 size, float4 radii, float4 profiles)
            {
                float2 halfSize = max(size * 0.5, 0.0001);
                float2 q = abs(p) - halfSize;
                float result = length(max(q, 0.0)) + min(max(q.x, q.y), 0.0);
                [unroll] for (int c = 0; c < 4; c++)
                {
                    float radius = radii[c];
                    float2 side = float2(c == 0 || c == 3 ? -1.0 : 1.0, c < 2 ? 1.0 : -1.0);
                    float2 local = p * side - halfSize + radius;
                    if (radius < 0.0001 || local.x <= 0.0 || local.y <= 0.0) continue;
                    float2 u = max(local / radius, 0.00001);
                    float power = clamp(profiles[c], 0.5, 16.0);
                    float sum = pow(u.x, power) + pow(u.y, power);
                    float curved = pow(sum, 1.0 / power);
                    float2 gradient = pow(u, power - 1.0) * pow(sum, 1.0 / power - 1.0);
                    result = max(result, (curved - 1.0) * radius / max(length(gradient), 0.001));
                }
                return result;
            }

            float SmoothMinimum(float a, float b, float blendRadius)
            {
                if (blendRadius <= 0.00001) return min(a, b);
                float h = max(blendRadius - abs(a - b), 0.0) / blendRadius;
                return min(a, b) - h * h * blendRadius * 0.25;
            }

            float SpriteMemberDistance(float2 local, float2 size, int slice, float level)
            {
                float2 canonical = local / max(size, 0.0001) + 0.5;
                float2 bounded = saturate(canonical);
                float2 uv = (bounded * _FusionFieldSize.x + 4.0) / _FusionFieldSize.y;
                float d = SAMPLE_TEXTURE2D_ARRAY_LOD(_FusionSpriteFields, sampler_FusionSpriteFields, uv, slice, level).r;
                float dx = SAMPLE_TEXTURE2D_ARRAY_LOD(_FusionSpriteFields, sampler_FusionSpriteFields, uv + float2(1.0 / _FusionFieldSize.y, 0), slice, level).r
                    - SAMPLE_TEXTURE2D_ARRAY_LOD(_FusionSpriteFields, sampler_FusionSpriteFields, uv - float2(1.0 / _FusionFieldSize.y, 0), slice, level).r;
                float dy = SAMPLE_TEXTURE2D_ARRAY_LOD(_FusionSpriteFields, sampler_FusionSpriteFields, uv + float2(0, 1.0 / _FusionFieldSize.y), slice, level).r
                    - SAMPLE_TEXTURE2D_ARRAY_LOD(_FusionSpriteFields, sampler_FusionSpriteFields, uv - float2(0, 1.0 / _FusionFieldSize.y), slice, level).r;
                float localDistance = d / max(length(float2(dx, dy) * (_FusionFieldSize.x * 0.5) / max(size, 0.0001)), 0.001);
                return localDistance + length((canonical - bounded) * size);
            }

            float CalculateShapeDistance(float2 shapeUV, out float localSurfaceRadius, float fieldLevel, out SurfaceStyle appearance)
            {
                float minimumSize = max(0.0001, min(_RectSize.x, _RectSize.y));
                float2 surfacePoint = (shapeUV - 0.5) * _RectSize.xy;
                localSurfaceRadius = 0.5;
                appearance.tint = _TintColor;
                appearance.optics = float4(_BlurStrength, _RefractionAmount, _ChromaticAberration, _LensDepth);
                appearance.surface = _SurfaceOptics;
                appearance.style = float4(1.0, _RefractiveIndex, _SpecularGlare, _RimDepth);
                appearance.center = 0.5; appearance.graphic = 1.0; appearance.bands = _LipBands;
                float shapeDistance = 0.0;
                if (_FusionEnabled > 0.5)
                {
                    float additive = 100000.0, cutout = 100000.0;
                    float additiveRadius = 0.5, cutoutRadius = 0.5;
                    SurfaceStyle addStyle = appearance, cutStyle = appearance;
                    [loop] for (int i = 0; i < (int)_FusionCount; i++)
                    {
                        FusionElement element = _FusionElements[i];
                        SurfaceStyle memberStyle;
                        memberStyle.tint = element.tint; memberStyle.optics = element.optics; memberStyle.surface = element.surface; memberStyle.style = element.style;
                        memberStyle.graphic = element.graphic; memberStyle.bands = element.bands;
                        memberStyle.center = element.rect.xy / max(_RectSize.xy, 0.0001) + 0.5;
                        float2 delta = surfacePoint - element.rect.xy;
                        float4 transform = element.transform;
                        float2 local = float2(dot(delta, transform.xy), dot(delta, transform.zw));
                        float scale = element.metrics.y;
                        float2 box = abs(local) - element.rect.zw * 0.5;
                        float lowerBound = (length(max(box, 0.0)) + min(max(box.x, box.y), 0.0)) * scale - element.data.w;
                        float closest = element.metrics.x > 0.5 ? cutout : additive;
                        // Keep a finite nearest distance even outside the surface. A fixed
                        // support cutoff creates discontinuous derivatives and ghost edges.
                        if (lowerBound > closest + _FusionSoftness) continue;
                        float distance = element.metrics.z > 0.5
                            ? SpriteMemberDistance(local, element.rect.zw, (int)element.metrics.z - 1, fieldLevel)
                            : element.metrics.w > 0.5 ? CustomCornersDistance(local, element.rect.zw, element.corners, element.profiles)
                            : PrimitiveDistance(local, element.rect.zw, element.data.x, element.data.z, element.corners);
                        distance = distance * scale - element.data.w;
                        float radius = min(element.rect.z, element.rect.w) * 0.5 * scale / minimumSize;
                        // Blend the lip radius with the same weights as the boundary.
                        // This avoids a lighting seam when differently sized members meet.
                        if (element.metrics.x > 0.5)
                        {
                            float weight = _FusionSoftness > 0.00001 ? saturate(0.5 + 0.5 * (cutout - distance) / _FusionSoftness) : step(distance, cutout);
                            cutoutRadius = lerp(cutoutRadius, radius, weight);
                            cutStyle = BlendStyle(cutStyle, memberStyle, weight);
                            cutout = SmoothMinimum(cutout, distance, _FusionSoftness);
                        }
                        else
                        {
                            float weight = _FusionSoftness > 0.00001 ? saturate(0.5 + 0.5 * (additive - distance) / _FusionSoftness) : step(distance, additive);
                            additiveRadius = lerp(additiveRadius, radius, weight);
                            addStyle = BlendStyle(addStyle, memberStyle, weight);
                            additive = SmoothMinimum(additive, distance, _FusionSoftness);
                        }
                    }
                    float cutWeight = _FusionSoftness > 0.00001 ? saturate(0.5 + 0.5 * (-additive - cutout) / _FusionSoftness) : step(cutout, -additive);
                    localSurfaceRadius = lerp(additiveRadius, cutoutRadius, cutWeight);
                    appearance = BlendStyle(addStyle, cutStyle, cutWeight);
                    shapeDistance = -SmoothMinimum(-additive, cutout, _FusionSoftness);
                }
                else
                    shapeDistance = _CustomCorners > 0.5 ? CustomCornersDistance(surfacePoint, _RectSize.xy, _CornerRadii, _CornerProfiles) : PrimitiveDistance(surfacePoint, _RectSize.xy, _EdgeShape, _CornerContinuity, _CornerRadii);
                return shapeDistance / minimumSize;
            }

            half4 frag(Varyings input) : SV_Target
            {
                // Screen-space background coordinates and sprite-independent local coordinates.
                float2 screenUV = input.positionNDC.xy / input.positionNDC.w;
                screenUV = UnityStereoTransformScreenSpaceTex(screenUV);
                float2 shapeUV = input.shapeCoord;
                float localSurfaceRadius;
                if (input.shadowFlag > 0.5)
                {
                    float2 shadowUV = shapeUV - _ShadowSettings.yz;
                    SurfaceStyle unused;
                    float distance = CalculateShapeDistance(shadowUV, localSurfaceRadius, 0.0, unused);
                    if (_SpriteShape > 0.5 && _FusionEnabled < 0.5)
                    {
                        float2 canonical = (shadowUV - _SpriteDrawingRect.xy) / max(_SpriteDrawingRect.zw, 0.0001);
                        float2 bounded = saturate(canonical);
                        float fieldSize = _SpriteDistanceTex_TexelSize.z - 8.0;
                        float2 fieldUV = (bounded * fieldSize + 4.0) * _SpriteDistanceTex_TexelSize.xy;
                        distance = SAMPLE_TEXTURE2D_LOD(_SpriteDistanceTex, sampler_SpriteDistanceTex, fieldUV, 0).r / fieldSize;
                        distance += length(canonical - bounded);
                    }
                    float pixels = distance / max(length(float2(ddx(distance), ddy(distance))), 0.000001);
                    float sigma = max(0.5, _ShadowSettings.x);
                    float opacity = exp(-0.5 * pow(max(0.0, pixels) / sigma, 2.0));
                    if (_ShadowSettings.x <= 0.0) opacity = 1.0 - smoothstep(-0.5, 0.5, pixels);
                    #ifdef UNITY_UI_CLIP_RECT
                    opacity *= step(_ClipRect.x, input.worldPosition.x) * step(input.worldPosition.x, _ClipRect.z) * step(_ClipRect.y, input.worldPosition.y) * step(input.worldPosition.y, _ClipRect.w);
                    #endif
                    float dither = (frac(dot(input.positionCS.xy, float2(0.7548777, 0.5698403))) - 0.5) / 255.0;
                    return half4(_ShadowColor.rgb, saturate(opacity * _ShadowColor.a + dither) * input.color.a * _GlassIntensity);
                }
                SurfaceStyle appearance;
                float signedShapeDistance = CalculateShapeDistance(shapeUV, localSurfaceRadius, 0.0, appearance);
                float normalDistance = signedShapeDistance;
                float2 spriteNormalGradient = 0.0;
                if (_SpriteShape > 0.5 && _FusionEnabled < 0.5)
                {
                    float3 spriteUV = float3(input.texcoord, 1.0);
                    float2 fieldUV = float2(dot(spriteUV, _SpriteUvRow0.xyz), dot(spriteUV, _SpriteUvRow1.xyz));
                    float distance = SAMPLE_TEXTURE2D(_SpriteDistanceTex, sampler_SpriteDistanceTex, fieldUV).r;
                    // Convert the cached distance to local UI units using its screen gradient.
                    // This keeps the optical lip consistent when the sprite is stretched.
                    float fieldPerPixel = max(length(float2(ddx(distance), ddy(distance))), 0.0001);
                    float localPerPixel = sqrt(length(ddx(shapeUV) * _RectSize.xy) * length(ddy(shapeUV) * _RectSize.xy));
                    signedShapeDistance = distance / fieldPerPixel * localPerPixel / max(0.0001, min(_RectSize.x, _RectSize.y));
                    float fieldLevel = max(0.0, log2(max(1.0, appearance.surface.w * appearance.surface.z * _SpriteDistanceTex_TexelSize.z * 0.5)));
                    float2 normalStep = _SpriteDistanceTex_TexelSize.xy * exp2(fieldLevel);
                    float dx = SAMPLE_TEXTURE2D_LOD(_SpriteDistanceTex, sampler_SpriteDistanceTex, fieldUV + float2(normalStep.x, 0), fieldLevel).r - SAMPLE_TEXTURE2D_LOD(_SpriteDistanceTex, sampler_SpriteDistanceTex, fieldUV - float2(normalStep.x, 0), fieldLevel).r;
                    float dy = SAMPLE_TEXTURE2D_LOD(_SpriteDistanceTex, sampler_SpriteDistanceTex, fieldUV + float2(0, normalStep.y), fieldLevel).r - SAMPLE_TEXTURE2D_LOD(_SpriteDistanceTex, sampler_SpriteDistanceTex, fieldUV - float2(0, normalStep.y), fieldLevel).r;
                    float2 gradientUV = float2(dx, dy) / max(normalStep, 0.000001);
                    spriteNormalGradient = float2(dot(gradientUV, ddx(fieldUV)), dot(gradientUV, ddy(fieldUV)));
                    normalDistance = SAMPLE_TEXTURE2D_LOD(_SpriteDistanceTex, sampler_SpriteDistanceTex, fieldUV, fieldLevel).r / fieldPerPixel * localPerPixel / max(0.0001, min(_RectSize.x, _RectSize.y));
                    localSurfaceRadius = 0.5;
                }
                else if (_FusionEnabled > 0.5)
                {
                    float unusedRadius;
                    float fieldLevel = max(0.0, log2(max(1.0, appearance.surface.w * appearance.surface.z * _FusionFieldSize.x * 0.5)));
                    SurfaceStyle unusedStyle;
                    normalDistance = CalculateShapeDistance(shapeUV, unusedRadius, fieldLevel, unusedStyle);
                }
                float shapeAntialias = max(fwidth(signedShapeDistance), 0.0005);
                float proceduralAlpha = 1.0 - smoothstep(-shapeAntialias, shapeAntialias, signedShapeDistance);

                float clipAlpha = 1.0;
                #ifdef UNITY_UI_CLIP_RECT
                float2 clipPoint = input.worldPosition.xy;
                bool clipFail = clipPoint.x < _ClipRect.x || clipPoint.x > _ClipRect.z || clipPoint.y < _ClipRect.y || clipPoint.y > _ClipRect.w;
                clipAlpha = clipFail ? 0.0 : 1.0;
                #endif

                // Sample MainTex (UI image mask/sprite)
                half4 uiColor = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.texcoord) + _TextureSampleAdd;
                float spriteMask = uiColor.a;
                uiColor *= input.color;
                float sceneViewPreview = step(0.5, _TranslucentUI_SceneViewPreview);
                float spriteAntialias = max(fwidth(spriteMask), 0.001);
                float spriteCoverage = smoothstep(_SpriteAlphaThreshold - spriteAntialias, _SpriteAlphaThreshold + spriteAntialias, spriteMask);
                float silhouetteAlpha = _FusionEnabled > 0.5 ? proceduralAlpha
                    : _SpriteShape > 0.5 ? spriteCoverage : lerp(1.0, proceduralAlpha, _ProceduralShape);
                float runtimeAlpha = uiColor.a * clipAlpha * silhouetteAlpha;

                // Missing background must remain ordinary translucent UI, never an opaque placeholder.
                if (_CaptureUnavailable > 0.5 && sceneViewPreview < 0.5)
                {
                    half4 fallback = half4(appearance.tint.rgb * input.color.rgb * appearance.graphic.rgb,
                        runtimeAlpha * appearance.graphic.a * saturate(appearance.tint.a * _GlassIntensity * appearance.style.x));
                    #ifdef UNITY_UI_ALPHACLIP
                    clip(fallback.a - 0.001);
                    #endif
                    return fallback;
                }

                // Soft sprite masks and CanvasGroup fades used to keep the full optical
                // displacement until the final alpha blend. That mixed the live scene with
                // a spatially offset copy and produced doubled, crawling edges on moving
                // backgrounds. Collapse refraction and dispersion with local coverage so
                // transparent pixels approach the undistorted background continuously.
                float opticalCoverage = lerp(1.0, saturate(uiColor.a * silhouetteAlpha), saturate(_AlphaGradientStability));
                
                // Build a convex surface normal from analytic distance-to-edge. It remains stable
                // for stretched panels, pills, circles and continuous-corner rectangles.
                float2 safeSize = max(_RectSize.xy, float2(0.0001, 0.0001));
                float2 aspect = safeSize / min(safeSize.x, safeSize.y);
                float2 centeredPoint = (shapeUV - 0.5) * aspect;
                float distanceToEdge = max(0.0, -signedShapeDistance);
                // The screen-space SDF gradient follows the actual boundary, including
                // rotated and fused shapes. A narrow optical lip leaves the center flat.
                float2 baseRadialDirection = dot(centeredPoint, centeredPoint) > 0.000001 ? normalize(centeredPoint) : float2(0.0, 0.0);
                float interior = appearance.bands.z * smoothstep(0.4, 1.0, distanceToEdge / max(0.0001, appearance.surface.z * localSurfaceRadius * 2.0));
                float2 screenGradient = lerp(float2(ddx(normalDistance), ddy(normalDistance)), float2(ddx(signedShapeDistance), ddy(signedShapeDistance)), interior);
                if (_SpriteShape > 0.5 && _FusionEnabled < 0.5) screenGradient = spriteNormalGradient;
                float2 radialDirection = dot(screenGradient, screenGradient) > 0.00000001 ? normalize(screenGradient) : baseRadialDirection;
                // UV derivatives account for render-target orientation on every graphics API.
                float2 boundaryDirection = radialDirection.x * ddx(screenUV) + radialDirection.y * ddy(screenUV);
                float2 screenMetric = boundaryDirection * _ScreenParams.xy;
                boundaryDirection = dot(screenMetric, screenMetric) > 0.000001 ? normalize(screenMetric) : float2(0.0, 0.0);
                float lipWidth = max(shapeAntialias, appearance.surface.z * max(0.05, localSurfaceRadius * 2.0));
                float lip = appearance.surface.z > 0.00001 ? 1.0 - smoothstep(0.0, lipWidth, distanceToEdge) : 0.0;
                float surfaceSlope = lip * lerp(0.45, 0.92, _SphericalDistortion);
                float3 normal3D = float3(boundaryDirection * surfaceSlope, sqrt(saturate(1.0 - surfaceSlope * surfaceSlope)));
                float NdotV = normal3D.z;

                float iorScale = saturate((appearance.style.y - 1.0) / 0.6);
                float refractionMagnitude = lip;
                // Express displacement in a height-normalized screen metric so a lens
                // bends equally in both axes on wide and tall viewports.
                float2 opticalAspect = float2(_ScreenParams.y / max(1.0, _ScreenParams.x), 1.0);
                if (sceneViewPreview < 0.5)
                    screenUV -= normal3D.xy * opticalAspect * refractionMagnitude * appearance.optics.y * appearance.optics.w * iorScale * opticalCoverage * clipAlpha;

                float2 centerUV = screenUV - (shapeUV.x - 0.5) * ddx(screenUV) / max(length(ddx(shapeUV)), 0.0001)
                    - (shapeUV.y - 0.5) * ddy(screenUV) / max(length(ddy(shapeUV)), 0.0001);
                // Derive the projected panel center from the local-to-screen Jacobian.
                float2 ux = ddx(shapeUV), uy = ddy(shapeUV);
                float determinant = ux.x * uy.y - ux.y * uy.x;
                if (abs(determinant) > 0.00000001)
                {
                    float2 pixelOffset = float2(dot(shapeUV - appearance.center, float2(uy.y, -uy.x)), dot(shapeUV - appearance.center, float2(-ux.y, ux.x))) / determinant;
                    centerUV = input.positionNDC.xy / input.positionNDC.w - pixelOffset.x * ddx(screenUV) - pixelOffset.y * ddy(screenUV);
                    screenUV = centerUV + (screenUV - centerUV) / max(1.0, appearance.surface.x);
                }

                // Sample both the original capture and the maximum blur. Their per-element blend
                // makes Blur Strength genuinely independent on every Translucent Image.
                half4 sourceColor;
                half4 blurredColor;
                if (sceneViewPreview > 0.5)
                {
                    // The Scene View grid, gizmos and Overlay canvas are not part of the URP
                    // camera capture. Use a translucent authoring body instead of stale Game
                    // View pixels so layout and fused silhouettes remain stable while editing.
                    half3 neutralGlass = half3(0.18, 0.24, 0.30);
                    float previewTintWeight = saturate(0.20 + appearance.tint.a * 0.65);
                    half3 previewBody = lerp(neutralGlass, appearance.tint.rgb, previewTintWeight);
                    previewBody += _FrostAmount * 0.12 + _LuminosityBoost * 0.08;
                    sourceColor = half4(previewBody, 1.0);
                    blurredColor = sourceColor;
                }
                else if (appearance.optics.z > 0.001)
                {
                    float2 caOffset = normal3D.xy * opticalAspect * refractionMagnitude * (appearance.optics.z * appearance.optics.w * 0.6) * iorScale * opticalCoverage * clipAlpha;

                    sourceColor.r = SourceSample(screenUV + caOffset).r;
                    sourceColor.g = SourceSample(screenUV).g;
                    sourceColor.b = SourceSample(screenUV - caOffset).b;
                    sourceColor.a = 1.0;

                    blurredColor.r = BlurSample(screenUV + caOffset).r;
                    blurredColor.g = BlurSample(screenUV).g;
                    blurredColor.b = BlurSample(screenUV - caOffset).b;
                    blurredColor.a = 1.0;
                }
                else
                {
                    sourceColor = SourceSample(screenUV);
                    blurredColor = BlurSample(screenUV);
                }

                float capturedBlur = _CaptureBound > 0.5 ? _CaptureOptions.x : _TranslucentUI_MaxBlurStrength;
                float normalizedBlur = capturedBlur > 0.0001
                    ? saturate(appearance.optics.x / capturedBlur)
                    : 0.0;
                half4 blurColor = lerp(sourceColor, blurredColor, normalizedBlur);
                
                // Base luminance for Saturation
                float baseLuminance = dot(blurColor.rgb, float3(0.299, 0.587, 0.114));

                // -- Color Correction Stack --
                blurColor.rgb = lerp(baseLuminance.xxx, blurColor.rgb, _Saturation);
                blurColor.rgb = (blurColor.rgb - 0.5) * _Contrast + 0.5;
                blurColor.rgb *= _Brightness;

                // Volumetric Inner Fresnel Shadow
                float fresnel = pow(1.0 - NdotV, 2.5);
                float shadowIntensity = fresnel * appearance.style.w * clipAlpha;
                blurColor.rgb = lerp(blurColor.rgb, blurColor.rgb * 0.22, shadowIntensity);
                
                // Re-calculate physical luminance after color stack for accurate frost/light scattering
                float luminance = dot(blurColor.rgb, float3(0.299, 0.587, 0.114));
                
                // 🧠 Auto Readability System
                float readabilityOffset = (0.5 - luminance) * _AutoReadability;
                blurColor.rgb = saturate(blurColor.rgb + readabilityOffset);
                luminance = dot(blurColor.rgb, float3(0.299, 0.587, 0.114));

                // 2. Stylized Realism: Mute and Desaturate raw colors
                blurColor.rgb = lerp(blurColor.rgb, luminance.xxx, _FrostAmount);
                
                // Additive Luminosity Boost
                blurColor.rgb += luminance.xxx * _LuminosityBoost;

                // Clear glass preserves the captured luminance; tint controls absorption.

                // 3. Base logic: combine muted blur texture with user's tint color
                blurColor.rgb *= appearance.surface.y;
                half4 finalColor = lerp(blurColor, appearance.tint, appearance.tint.a);
                
                // Optional slight physical brightness from frost particles
                finalColor.rgb += _FrostAmount * 0.1;

                // Directional edge glint: the near side catches light, while the far side
                // retains just enough fill to read as a thick glass volume.
                float edgeDistance = max(0.0001, distanceToEdge);
                float effectiveEdgeWidth = _EdgeWidth * max(0.05, localSurfaceRadius * 2.0);
                float innerGlow = pow(saturate(1.0 - (edgeDistance / max(0.001, effectiveEdgeWidth))), _EdgePower);
                float2 lightDirection2D = dot(_LightDirection.xy, _LightDirection.xy) > 0.000001 ? normalize(_LightDirection.xy) : normalize(float2(-0.5, 0.55));
                float attenuation = 1.0;
                if (_SurfaceLighting.x > 1.5)
                {
                    float2 lightDelta = (_PointLightPosition.xy - input.positionNDC.xy / input.positionNDC.w) * float2(_ScreenParams.x / _ScreenParams.y, 1.0);
                    attenuation = 1.0 - smoothstep(0.0, max(0.01, _SurfaceLighting.w), length(lightDelta));
                    lightDirection2D = normalize(lightDelta + 0.000001);
                }
                float alignment = dot(boundaryDirection, lightDirection2D);
                float reach = max(0.0001, _SurfaceLighting.z);
                float nearLight = smoothstep(1.0 - reach * 2.0, 1.0, alignment);
                float farLight = smoothstep(1.0 - reach * 2.0, 1.0, -alignment);
                float opposing = _SurfaceLighting.x > 0.5 && _SurfaceLighting.x < 1.5 ? _SurfaceLighting.y : 0.0;
                float edgePosition = distanceToEdge / max(lipWidth, 0.00001);
                float outerBand = appearance.bands.y > 0.0001 ? 1.0 - smoothstep(0.0, appearance.bands.y, edgePosition) : 0.0;
                float innerBand = appearance.bands.x > 0.0001 ? smoothstep(1.0 - appearance.bands.x, 1.0, edgePosition) * (1.0 - smoothstep(1.0, 1.12, edgePosition)) : 0.0;
                float glintEnergy = ((nearLight + farLight * opposing) * outerBand + (farLight + nearLight * opposing) * innerBand) * attenuation * step(0.0001, _SurfaceLighting.z) * step(0.0001, appearance.surface.z);
                finalColor.rgb += _LipLightColor.rgb * _LipLightColor.a * glintEnergy * uiColor.a * clipAlpha;
                float shade = (1.0 - saturate(nearLight + farLight * opposing)) * lip * _LipShadowColor.a;
                finalColor.rgb = lerp(finalColor.rgb, _LipShadowColor.rgb, shade);

                // 5. Procedural Specular Highlight (The 'Apple Liquid' Glare)
                float3 lightDir = normalize(float3(lightDirection2D, 1.0));
                float3 viewDir = float3(0.0, 0.0, 1.0);
                float3 halfVector = normalize(lightDir + viewDir);
                float NdotH = saturate(dot(normal3D, halfVector));
                
                float specular = pow(NdotH, _SpecularSharpness);
                float specularIntensity = specular * lip * clipAlpha * appearance.style.z * (1.0 - sceneViewPreview);
                
                // Keep the highlight on the optical lip, away from content.
                finalColor.rgb += float3(1.0, 1.0, 1.0) * specularIntensity;

                // 5. Volume Scattering (Center Glow)
                float radialFalloff = saturate(length(centeredPoint) * 1.414);
                float baseCenterScatter = saturate(1.0 - radialFalloff);
                float fusedCenterScatter = saturate(distanceToEdge / max(0.0001, localSurfaceRadius));
                float centerScatter = lerp(baseCenterScatter, fusedCenterScatter, _FusionEnabled);
                finalColor.rgb += centerScatter * _FrostAmount * 0.15;

                // 5. Film Grain (Color Noise to simulate frosted acrylic micro-texture)
                float noise = frac(sin(dot(input.positionCS.xy, float2(12.9898, 4.1414))) * 43758.5453) * 2.0 - 1.0;
                finalColor.rgb += noise * (_NoiseAmount + (_CaptureBound > 0.5 ? _CaptureOptions.y / 255.0 : 0.0)) * (1.0 - sceneViewPreview);

                // Alpha combines native sprite masking, RectMask2D clipping and the optional
                // analytic silhouette. Existing sprite workflows remain fully compatible.
                finalColor.rgb = lerp(sourceColor.rgb, finalColor.rgb, saturate(_GlassIntensity));
                float previewOpacity = saturate(0.10 + _GlassIntensity * 0.10 + appearance.tint.a * 0.45 + _FrostAmount * 0.20);
                finalColor.a = runtimeAlpha * appearance.graphic.a * saturate(_GlassIntensity * appearance.style.x) * lerp(1.0, previewOpacity, sceneViewPreview);
                finalColor.rgb = saturate(finalColor.rgb * input.color.rgb * appearance.graphic.rgb);

                #ifdef UNITY_UI_ALPHACLIP
                clip(finalColor.a - 0.001);
                #endif

                return finalColor;
            }
        ENDHLSL
        }
    }
    CustomEditor "TranslucentUIFX.Editor.LiquidGlassMaterialGUI"
}

Shader "Custom/ToonCartoon"
{
    Properties
    {
        _BaseColor      ("Base Color",         Color)       = (1,1,1,1)
        _BaseMap        ("Base Texture",       2D)          = "white" {}

        [Header(Toon Lighting)]
        _ShadowColor    ("Shadow Color",       Color)       = (0.2, 0.2, 0.35, 1)
        _ShadowThreshold("Shadow Threshold",   Range(0,1))  = 0.5
        _ShadowSmooth   ("Shadow Softness",    Range(0,0.3))= 0.04

        [Header(Specular)]
        _SpecColor      ("Specular Color",     Color)       = (1,1,1,1)
        _SpecGloss      ("Specular Threshold", Range(0,1))  = 0.6
        _SpecSmooth     ("Specular Softness",  Range(0,0.1))= 0.02
        _SpecStrength   ("Specular Strength",  Range(0,1))  = 0.6

        [Header(Rim Light)]
        _RimColor       ("Rim Color",          Color)       = (1,1,1,1)
        _RimThreshold   ("Rim Threshold",      Range(0,1))  = 0.7
        _RimSmooth      ("Rim Softness",       Range(0,0.1))= 0.03
        _RimStrength    ("Rim Strength",       Range(0,1))  = 0.4

        [Header(Outline)]
        _OutlineColor   ("Outline Color",      Color)       = (0,0,0,1)
        _OutlineWidth   ("Outline Width",      Range(0,0.05))= 0.003
    }

    SubShader
    {
        Tags
        {
            "RenderType"  = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "Queue"       = "Geometry"
        }

        // ─────────────────────────────────────────────────────────
        // PASS 1 – Outline (back-face expanded hull)
        // ─────────────────────────────────────────────────────────
        Pass
        {
            Name "Outline"
            Cull Front

            HLSLPROGRAM
            #pragma vertex   OutlineVert
            #pragma fragment OutlineFrag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _OutlineColor;
                float  _OutlineWidth;
                // declare all properties here too so the CBuffer matches
                float4 _BaseColor;
                float4 _BaseMap_ST;
                float4 _ShadowColor;
                float  _ShadowThreshold;
                float  _ShadowSmooth;
                float4 _SpecColor;
                float  _SpecGloss;
                float  _SpecSmooth;
                float  _SpecStrength;
                float4 _RimColor;
                float  _RimThreshold;
                float  _RimSmooth;
                float  _RimStrength;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings   { float4 positionHCS : SV_POSITION; };

            Varyings OutlineVert(Attributes IN)
            {
                Varyings OUT;
                float3 normalWS = TransformObjectToWorldNormal(IN.normalOS);
                float3 posWS    = TransformObjectToWorld(IN.positionOS.xyz);
                posWS          += normalize(normalWS) * _OutlineWidth;
                OUT.positionHCS = TransformWorldToHClip(posWS);
                return OUT;
            }

            half4 OutlineFrag(Varyings IN) : SV_Target
            {
                return _OutlineColor;
            }
            ENDHLSL
        }

        // ─────────────────────────────────────────────────────────
        // PASS 2 – Main toon-lit surface
        // ─────────────────────────────────────────────────────────
        Pass
        {
            Name "ToonForward"
            Tags { "LightMode" = "UniversalForward" }
            Cull Back

            HLSLPROGRAM
            #pragma vertex   ToonVert
            #pragma fragment ToonFrag

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _SHADOWS_SOFT

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _BaseMap_ST;
                float4 _ShadowColor;
                float  _ShadowThreshold;
                float  _ShadowSmooth;
                float4 _SpecColor;
                float  _SpecGloss;
                float  _SpecSmooth;
                float  _SpecStrength;
                float4 _RimColor;
                float  _RimThreshold;
                float  _RimSmooth;
                float  _RimStrength;
                float4 _OutlineColor;
                float  _OutlineWidth;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 positionWS  : TEXCOORD0;
                float3 normalWS    : TEXCOORD1;
                float2 uv          : TEXCOORD2;
                float4 shadowCoord : TEXCOORD3;
            };

            Varyings ToonVert(Attributes IN)
            {
                Varyings OUT;
                VertexPositionInputs posInputs = GetVertexPositionInputs(IN.positionOS.xyz);
                OUT.positionHCS = posInputs.positionCS;
                OUT.positionWS  = posInputs.positionWS;
                OUT.normalWS    = TransformObjectToWorldNormal(IN.normalOS);
                OUT.uv          = TRANSFORM_TEX(IN.uv, _BaseMap);
                OUT.shadowCoord = GetShadowCoord(posInputs);
                return OUT;
            }

            // ── Stepped/smooth helper ──────────────────────────
            // Converts a raw dot-product into a hard cartoon band.
            float ToonStep(float threshold, float smoothness, float value)
            {
                return smoothstep(threshold - smoothness, threshold + smoothness, value);
            }

            half4 ToonFrag(Varyings IN) : SV_Target
            {
                // Sample texture
                half4 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv) * _BaseColor;

                float3 N = normalize(IN.normalWS);
                float3 V = normalize(GetWorldSpaceViewDir(IN.positionWS));

                // Main directional light
                Light mainLight = GetMainLight(IN.shadowCoord);
                float3 L = normalize(mainLight.direction);
                float3 H = normalize(L + V);                  // half-vector

                // ── 1. Diffuse toon banding ──────────────────
                float NdotL     = dot(N, L) * 0.5 + 0.5;      // remap to [0,1]
                float shadowAtt = mainLight.shadowAttenuation;
                float toonDiff  = ToonStep(_ShadowThreshold, _ShadowSmooth, NdotL * shadowAtt);

                half3 diffuse   = lerp(_ShadowColor.rgb, mainLight.color, toonDiff);

                // ── 2. Specular toon highlight ───────────────
                float NdotH    = dot(N, H);
                float specMask = ToonStep(_SpecGloss, _SpecSmooth, NdotH);
                half3 specular = _SpecColor.rgb * specMask * _SpecStrength * toonDiff;

                // ── 3. Rim light ─────────────────────────────
                float rimDot  = 1.0 - dot(N, V);
                float rimMask = ToonStep(_RimThreshold, _RimSmooth, rimDot);
                half3 rim     = _RimColor.rgb * rimMask * _RimStrength;

                // ── Combine ──────────────────────────────────
                half3 finalColor = albedo.rgb * diffuse + specular + rim;

                return half4(finalColor, albedo.a);
            }
            ENDHLSL
        }

        // ─────────────────────────────────────────────────────────
        // PASS 3 – Shadow caster (so the mesh still casts shadows)
        // ─────────────────────────────────────────────────────────
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On
            ColorMask 0
            Cull Back

            HLSLPROGRAM
            #pragma vertex   ShadowVert
            #pragma fragment ShadowFrag

            // Only Core.hlsl — avoids the LerpWhiteTo issue in older URP versions
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor; float4 _BaseMap_ST;
                float4 _ShadowColor; float _ShadowThreshold; float _ShadowSmooth;
                float4 _SpecColor; float _SpecGloss; float _SpecSmooth; float _SpecStrength;
                float4 _RimColor; float _RimThreshold; float _RimSmooth; float _RimStrength;
                float4 _OutlineColor; float _OutlineWidth;
            CBUFFER_END

            // Unity sets these per-light automatically
            float3 _LightDirection;
            float3 _LightPosition;

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings   { float4 positionHCS : SV_POSITION; };

            // Manual shadow bias — works on all URP versions
            float4 GetShadowPositionHClip(float3 positionOS, float3 normalOS)
            {
                float3 posWS  = TransformObjectToWorld(positionOS);
                float3 normWS = TransformObjectToWorldNormal(normalOS);

                // Push vertex slightly along the light direction to prevent shadow acne
                float invNdotL = 1.0 - saturate(dot(normWS, _LightDirection));
                float scale    = invNdotL * 0.001;
                posWS         += normWS  * scale;
                posWS         += _LightDirection * 0.0005;

                return TransformWorldToHClip(posWS);
            }

            Varyings ShadowVert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = GetShadowPositionHClip(IN.positionOS.xyz, IN.normalOS);
                return OUT;
            }

            half4 ShadowFrag(Varyings IN) : SV_Target { return 0; }
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}

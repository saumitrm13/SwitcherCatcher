Shader "Custom/MagicalHologram"
{
    Properties
    {
        [Header(Base Maps)]
        _BaseMap            ("Base Map (Albedo)", 2D)           = "white" {}
        _BaseColor          ("Base Tint", Color)                = (1,1,1,1)
        _NormalMap          ("Normal Map", 2D)                  = "bump" {}
        _NormalStrength     ("Normal Strength", Range(0,3))     = 1.0

        [Header(Hologram)]
        _HologramIntensity  ("Hologram Intensity", Range(0,1))  = 0.0
        _HologramColor      ("Hologram Color", Color)           = (0.2, 0.8, 1.0, 1.0)
        _ScanlineCount      ("Scanline Density", Range(10,200)) = 60
        _ScanlineSpeed      ("Scanline Speed", Range(0,5))      = 1.5
        _ScanlineThickness  ("Scanline Thickness", Range(0.01,0.5)) = 0.15
        _FlickerSpeed       ("Flicker Speed", Range(0,20))      = 4.0
        _FlickerAmount      ("Flicker Amount", Range(0,1))      = 0.15

        [Header(Glitch)]
        _GlitchIntensity    ("Glitch Intensity", Range(0,1))    = 0.0
        _GlitchSpeed        ("Glitch Speed", Range(0,20))       = 5.0
        _GlitchBlockSize    ("Glitch Block Size", Range(0.01,0.5)) = 0.1

        [Header(Rim Aura)]
        _RimColor           ("Rim Glow Color", Color)           = (0.5, 0.2, 1.0, 1.0)
        _RimPower           ("Rim Fresnel Power", Range(0.5,8)) = 3.0
        _RimIntensity       ("Rim Intensity", Range(0,5))       = 1.5

        [Header(Magic Glow)]
        _EmissiveColor      ("Emissive / Magic Color", Color)   = (0.4, 0.0, 0.8, 1.0)
        _EmissiveIntensity  ("Emissive Intensity", Range(0,10)) = 0.0
        _PulseSpeed         ("Pulse Speed", Range(0,5))         = 1.0
        _PulseAmount        ("Pulse Amount", Range(0,1))        = 0.3

        [Header(Magic Sparkles)]
        _SparkleIntensity   ("Sparkle Intensity", Range(0,1))   = 0.0
        _SparkleColor       ("Sparkle Color", Color)            = (1.0, 0.9, 0.4, 1.0)
        _SparkleScale       ("Sparkle Scale", Range(5,100))     = 30.0
        _SparkleSpeed       ("Sparkle Speed", Range(0,10))      = 3.0

        [Header(Dissolve)]
        _DissolveAmount     ("Dissolve Amount", Range(0,1))     = 0.0
        _DissolveColor      ("Dissolve Edge Color", Color)      = (0.2, 0.6, 1.0, 1.0)
        _DissolveEdge       ("Dissolve Edge Width", Range(0.01,0.2)) = 0.05
        _DissolveTexture    ("Dissolve Noise (R)", 2D)          = "white" {}

        [Header(Overall)]
        _Opacity            ("Overall Opacity", Range(0,1))     = 1.0
    }

    SubShader
    {
        Tags
        {
            "RenderType"        = "Transparent"
            "RenderPipeline"    = "UniversalPipeline"
            "Queue"             = "Transparent"
            "IgnoreProjector"   = "True"
        }

        Pass
        {
            Name "MagicalHologram"
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            // ─── Textures & Samplers ─────────────────────────────────────────
            TEXTURE2D(_BaseMap);        SAMPLER(sampler_BaseMap);
            TEXTURE2D(_NormalMap);      SAMPLER(sampler_NormalMap);
            TEXTURE2D(_DissolveTexture);SAMPLER(sampler_DissolveTexture);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _NormalMap_ST;
                float4 _DissolveTexture_ST;
                float4 _BaseColor;

                // Hologram
                float4 _HologramColor;
                float  _HologramIntensity;
                float  _ScanlineCount;
                float  _ScanlineSpeed;
                float  _ScanlineThickness;
                float  _FlickerSpeed;
                float  _FlickerAmount;

                // Glitch
                float  _GlitchIntensity;
                float  _GlitchSpeed;
                float  _GlitchBlockSize;

                // Rim
                float4 _RimColor;
                float  _RimPower;
                float  _RimIntensity;

                // Normal
                float  _NormalStrength;

                // Emissive
                float4 _EmissiveColor;
                float  _EmissiveIntensity;
                float  _PulseSpeed;
                float  _PulseAmount;

                // Sparkle
                float4 _SparkleColor;
                float  _SparkleIntensity;
                float  _SparkleScale;
                float  _SparkleSpeed;

                // Dissolve
                float  _DissolveAmount;
                float4 _DissolveColor;
                float  _DissolveEdge;

                // Overall
                float  _Opacity;
            CBUFFER_END

            // ─── Vertex I/O ─────────────────────────────────────────────────
            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float4 tangentOS  : TANGENT;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv          : TEXCOORD0;
                float3 worldPos    : TEXCOORD1;
                float3 worldNormal : TEXCOORD2;
                float3 worldView   : TEXCOORD3;
                float3 worldTangent: TEXCOORD4;
                float3 worldBinorm : TEXCOORD5;
                float  fogFactor   : TEXCOORD6;
            };

            // ─── Utility functions ───────────────────────────────────────────
            float Hash(float2 p)
            {
                p = frac(p * float2(443.8975, 397.2973));
                p += dot(p.xy, p.yx + 19.19);
                return frac(p.x * p.y);
            }

            float Noise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(Hash(i + float2(0,0)), Hash(i + float2(1,0)), u.x),
                            lerp(Hash(i + float2(0,1)), Hash(i + float2(1,1)), u.x), u.y);
            }

            // Star / sparkle pattern
            float Star(float2 uv, float size)
            {
                uv = abs(uv);
                float d = max(uv.x + uv.y, max(uv.x, uv.y));
                return smoothstep(size, size * 0.6, d);
            }

            // ─── Vertex Shader ───────────────────────────────────────────────
            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                VertexPositionInputs vpi = GetVertexPositionInputs(IN.positionOS.xyz);
                VertexNormalInputs   vni = GetVertexNormalInputs(IN.normalOS, IN.tangentOS);

                OUT.positionHCS = vpi.positionCS;
                OUT.worldPos    = vpi.positionWS;
                OUT.uv          = TRANSFORM_TEX(IN.uv, _BaseMap);
                OUT.worldNormal = vni.normalWS;
                OUT.worldTangent= vni.tangentWS;
                OUT.worldBinorm = vni.bitangentWS;

                float3 camPos   = GetCameraPositionWS();
                OUT.worldView   = normalize(camPos - vpi.positionWS);
                OUT.fogFactor   = ComputeFogFactor(vpi.positionCS.z);
                return OUT;
            }

            // ─── Fragment Shader ─────────────────────────────────────────────
            half4 frag(Varyings IN) : SV_Target
            {
                float  t   = _Time.y;
                float2 uv  = IN.uv;

                // ── Glitch offset ────────────────────────────────────────────
                float glitchTime = floor(t * _GlitchSpeed);
                float glitchRow  = floor(uv.y / _GlitchBlockSize);
                float glitchHash = Hash(float2(glitchTime, glitchRow));
                float glitchOff  = (glitchHash - 0.5) * 2.0 * _GlitchIntensity * 0.08;
                float2 glitchUV  = uv + float2(glitchOff, 0);

                // ── Base albedo ──────────────────────────────────────────────
                float4 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, glitchUV) * _BaseColor;

                // ── Normal map ───────────────────────────────────────────────
                float4 normalSample = SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap,
                                        TRANSFORM_TEX(uv, _NormalMap));
                float3 tNormal = UnpackNormalScale(normalSample, _NormalStrength);
                float3 wNormal = normalize(
                    tNormal.x * IN.worldTangent +
                    tNormal.y * IN.worldBinorm  +
                    tNormal.z * IN.worldNormal);

                // ── Basic diffuse (main directional light) ───────────────────
                Light mainLight   = GetMainLight();
                float  NdotL      = saturate(dot(wNormal, mainLight.direction));
                float3 diffuse    = albedo.rgb * mainLight.color * (0.5 + 0.5 * NdotL);

                // ── Fresnel rim ──────────────────────────────────────────────
                float  NdotV     = saturate(dot(wNormal, IN.worldView));
                float  fresnel   = pow(1.0 - NdotV, _RimPower);
                float3 rimGlow   = _RimColor.rgb * fresnel * _RimIntensity;

                // ── Emissive pulse ───────────────────────────────────────────
                float  pulse     = 1.0 + _PulseAmount * sin(t * _PulseSpeed * 6.2832);
                float3 emissive  = _EmissiveColor.rgb * _EmissiveIntensity * pulse;

                // ── Sparkles ─────────────────────────────────────────────────
                float2 sparkUV   = uv * _SparkleScale + float2(0, t * _SparkleSpeed * 0.1);
                float2 sparkCell = frac(sparkUV) - 0.5;
                float  sparkTime = floor(t * _SparkleSpeed) + Hash(floor(sparkUV));
                float  sparkRand = Hash(float2(sparkTime, floor(sparkUV).x + floor(sparkUV).y * 31.7));
                float  sparkle   = Star(sparkCell, 0.08) * step(0.7, sparkRand) * _SparkleIntensity;
                float3 sparkColor= _SparkleColor.rgb * sparkle * 3.0;

                // ── Scanlines ────────────────────────────────────────────────
                float  scanY     = uv.y + t * _ScanlineSpeed * 0.1;
                float  scanLine  = abs(sin(scanY * _ScanlineCount * 3.14159));
                scanLine         = step(1.0 - _ScanlineThickness, scanLine);

                // Flicker
                float flicker    = 1.0 - _FlickerAmount *
                                   step(0.9, Hash(float2(floor(t * _FlickerSpeed), 0.0)));

                float3 holoColor = _HologramColor.rgb;
                float  holoMask  = fresnel + scanLine * 0.5 + 0.1;
                float3 hologram  = holoColor * holoMask * flicker;

                // ── Combine ──────────────────────────────────────────────────
                float3 finalColor= lerp(diffuse, hologram, _HologramIntensity)
                                   + rimGlow
                                   + emissive
                                   + sparkColor;

                // ── Dissolve ─────────────────────────────────────────────────
                float2 dissUV    = TRANSFORM_TEX(uv, _DissolveTexture);
                float  dissNoise = SAMPLE_TEXTURE2D(_DissolveTexture, sampler_DissolveTexture, dissUV).r;
                float  dissClip  = dissNoise - _DissolveAmount;
                clip(dissClip);

                float  dissEdge  = step(0, dissClip) * step(dissClip, _DissolveEdge);
                finalColor       = lerp(finalColor, _DissolveColor.rgb * 3.0, dissEdge);

                // ── Final alpha & fog ─────────────────────────────────────────
                float alpha = albedo.a * _Opacity;
                alpha       = lerp(alpha, alpha * flicker, _HologramIntensity * 0.4);

                half4 color  = half4(finalColor, alpha);
                color.rgb    = MixFog(color.rgb, IN.fogFactor);
                return color;
            }
            ENDHLSL
        }
    }

    Fallback "Universal Render Pipeline/Unlit"
}

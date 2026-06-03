Shader "Custom/DissolveInstanced"
{
    Properties
    {
        _MainTex ("Main Texture", 2D) = "white" {}
        _NoiseTex ("Dissolve Noise", 2D) = "white" {}

        _EdgeColor ("Dissolve Edge Color", Color) = (1,0.5,0,1)
        _EdgeWidth ("Edge Width", Range(0.001,0.2)) = 0.05

        [Toggle] _StartDissolve ("Start Dissolve", Float) = 0
        [Toggle] _StartReappear ("Start Reappear", Float) = 0

        _Speed ("Animation Speed", Float) = 1.0

        _Progress ("Progress", Range(0,1)) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderType"="TransparentCutout"
            "Queue"="AlphaTest"
        }

        LOD 200

        Pass
        {
            CGPROGRAM

            #pragma vertex vert
            #pragma fragment frag

            #pragma multi_compile_instancing

            #include "UnityCG.cginc"

            sampler2D _MainTex;
            sampler2D _NoiseTex;

            float4 _MainTex_ST;

            fixed4 _EdgeColor;
            float _EdgeWidth;
            float _Speed;

            float _StartDissolve;
            float _StartReappear;

            UNITY_INSTANCING_BUFFER_START(Props)

                UNITY_DEFINE_INSTANCED_PROP(float, _Progress)

            UNITY_INSTANCING_BUFFER_END(Props)

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;

                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;

                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            v2f vert(appdata v)
            {
                v2f o;

                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v,o);

                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv,_MainTex);

                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);

                float progress =
                    UNITY_ACCESS_INSTANCED_PROP(Props,_Progress);

                float noise =
                    tex2D(_NoiseTex, i.uv).r;

                fixed4 col =
                    tex2D(_MainTex, i.uv);

                float threshold = progress;

                //------------------------------------
                // Dissolve clip
                //------------------------------------
                clip(noise - threshold);

                //------------------------------------
                // Edge glow
                //------------------------------------
                float edge =
                    smoothstep(
                        threshold,
                        threshold + _EdgeWidth,
                        noise);

                float edgeMask = 1 - edge;

                col.rgb =
                    lerp(
                        _EdgeColor.rgb,
                        col.rgb,
                        edge);

                return col;
            }

            ENDCG
        }
    }

    FallBack "Diffuse"
}
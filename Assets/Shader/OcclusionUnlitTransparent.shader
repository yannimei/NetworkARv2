Shader "Custom/OcclusionUnlitTransparent"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _MainTex ("Albedo (RGB)", 2D) = "white" {}
        _Opacity ("Opacity", Range(0,1)) = 1.0
        _EnvironmentDepthBias ("Environment Depth Bias", Float) = 0.0
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" }
        LOD 200

        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            CGPROGRAM

            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.5

            #pragma multi_compile _ HARD_OCCLUSION SOFT_OCCLUSION

            #include "UnityCG.cginc"
            #include "Packages/com.meta.xr.sdk.core/Shaders/EnvironmentDepth/BiRP/EnvironmentOcclusionBiRP.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float2 uv_MainTex : TEXCOORD0;
                float4 vertex : SV_POSITION;

                META_DEPTH_VERTEX_OUTPUT(1)

                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _Color;
            float _Opacity;
            float _EnvironmentDepthBias;

            v2f vert (appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv_MainTex = TRANSFORM_TEX(v.uv, _MainTex);
                META_DEPTH_INITIALIZE_VERTEX_OUTPUT(o, v.vertex);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                fixed4 texColor = tex2D(_MainTex, i.uv_MainTex);
                fixed4 finalColor = texColor * _Color;

                // Apply opacity to alpha
                finalColor.a *= _Opacity;

                // Premultiply alpha for correct blending
                finalColor.rgb *= finalColor.a;

                META_DEPTH_OCCLUDE_OUTPUT_PREMULTIPLY(i, finalColor, _EnvironmentDepthBias);

                return finalColor;
            }
            ENDCG
        }
    }
}



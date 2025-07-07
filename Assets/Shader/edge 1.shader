Shader "Custom/HedgeOutline_Occluded"
{
    Properties
    {
        _OutlineColor ("Outline Color", Color) = (0,0,0,1)
        _OutlineWidth ("Outline Width", Float) = 0.003
        _EnvironmentDepthBias ("Environment Depth Bias", Float) = 0.001
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" }

        Cull Front
        ZWrite On
        Lighting Off

        Pass
        {
            CGPROGRAM

            // Add occlusion support
            #pragma multi_compile _ HARD_OCCLUSION SOFT_OCCLUSION

            #include "UnityCG.cginc"
            #include "Packages/com.meta.xr.sdk.core/Shaders/EnvironmentDepth/BiRP/EnvironmentOcclusionBiRP.cginc"

            #pragma vertex vert
            #pragma fragment frag

            float _OutlineWidth;
            float4 _OutlineColor;
            float _EnvironmentDepthBias;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 pos : SV_POSITION;

                META_DEPTH_VERTEX_OUTPUT(0) // outputs world position for occlusion
                UNITY_VERTEX_OUTPUT_STEREO
            };

            v2f vert (appdata v)
            {
                v2f o;

                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                float3 norm = normalize(v.normal);
                float4 offsetVertex = v.vertex;
                offsetVertex.xyz += norm * _OutlineWidth;

                o.pos = UnityObjectToClipPos(offsetVertex);

                META_DEPTH_INITIALIZE_VERTEX_OUTPUT(o, offsetVertex);

                return o;
            }

            half4 frag (v2f i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);

                half4 col = _OutlineColor;

                // Apply occlusion from Oculus environment depth
                META_DEPTH_OCCLUDE_OUTPUT_PREMULTIPLY(i, col, _EnvironmentDepthBias);

                return col;
            }
            ENDCG
        }
    }
}

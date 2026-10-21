// Phone AR occlusion for a tabletop model: writes the real world's depth (ARCore environment depth) into the depth
// buffer before the scene is drawn, so real things in front of the model hide it. Unlike AR Foundation's built-in
// background occlusion, the real depth is pushed back by a margin first: the estimate is noisy by a few centimetres,
// and the surface the model stands on would otherwise swallow its floor.
Shader "OSPlays/ARDepthOcclusion"
{
    Properties
    {
        _OcclusionBiasMin("Minimum margin (m)", Float) = 0.05
        _OcclusionBiasRatio("Margin per metre", Float) = 0.06
    }
    SubShader
    {
        Tags { "Queue" = "Geometry-50" "RenderType" = "Opaque" "IgnoreProjector" = "True" "ForceNoShadowCasting" = "True" }
        Pass
        {
            ZWrite On
            ZTest Always
            ColorMask 0
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _EnvironmentDepth;          // set globally by ARShaderOcclusion (metres)
            float4x4 _OcclusionDisplayTransform;  // the camera frame's display matrix
            float _OcclusionScale;                // world units per real metre (the AR rig's scale)
            float _OcclusionBiasMin;
            float _OcclusionBiasRatio;

            struct v2f
            {
                float4 position : SV_POSITION;
                float4 screen : TEXCOORD0;
            };

            v2f vert(float4 vertex : POSITION)
            {
                v2f o;
                o.position = UnityObjectToClipPos(vertex.xyz);
                o.screen = ComputeScreenPos(o.position);
                return o;
            }

            float frag(v2f i) : SV_Depth
            {
                float2 uv = i.screen.xy / i.screen.w;
                float2 tc = mul(float4(uv.x, uv.y, 1.0f, 0.0f), _OcclusionDisplayTransform).xy;
                float metres = tex2D(_EnvironmentDepth, tc).x;
                if (metres <= 0.0) discard; // no estimate here: nothing real in front
                metres += max(_OcclusionBiasMin, metres * _OcclusionBiasRatio);
                float eye = metres * _OcclusionScale;
                if (eye <= _ProjectionParams.y) discard;
                return (1.0 / eye - _ZBufferParams.w) / _ZBufferParams.z;
            }
            ENDHLSL
        }
    }
}

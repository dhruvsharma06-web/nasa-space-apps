// Thin additive halo shell around the Earth (scale ~1.025x): glows at the limb, fades on the night side.
// OWNER: Integration Lead.
Shader "ASTRA/Atmosphere"
{
    Properties
    {
        _Color ("Colour", Color) = (0.35, 0.65, 1.0, 1)
        _Power ("Falloff", Float) = 4
        _Strength ("Strength", Float) = 2.2
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent" }
        Pass
        {
            Name "Forward"
            Tags { "LightMode" = "UniversalForward" }
            Blend One One
            ZWrite Off
            Cull Back
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float4 _Color; float _Power, _Strength;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 normalWS : TEXCOORD0; float3 positionWS : TEXCOORD1; };
            Varyings vert (Attributes v)
            {
                Varyings o;
                VertexPositionInputs p = GetVertexPositionInputs(v.positionOS.xyz);
                o.positionCS = p.positionCS; o.positionWS = p.positionWS;
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                return o;
            }
            half4 frag (Varyings i) : SV_Target
            {
                float3 n = normalize(i.normalWS);
                float3 v = GetWorldSpaceNormalizeViewDir(i.positionWS);
                float ndv = saturate(dot(n, v));
                float halo = pow(1.0 - ndv, _Power) * smoothstep(0.0, 0.12, ndv);   // bright near the limb, fades at the outer edge
                float ndl = dot(n, GetMainLight().direction);
                float sunSide = saturate(ndl * 1.5 + 0.25);
                return half4(_Color.rgb * halo * _Strength * sunSide, 1);
            }
            ENDHLSL
        }
    }
}

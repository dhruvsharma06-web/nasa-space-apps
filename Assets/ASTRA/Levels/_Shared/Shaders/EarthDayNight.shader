// Earth seen from orbit: lit day side, city lights on the night side, drifting clouds, atmosphere rim.
// The sun is the scene's main directional light. OWNER: Integration Lead.
Shader "ASTRA/EarthDayNight"
{
    Properties
    {
        _DayTex ("Day (Blue Marble)", 2D) = "white" {}
        _NightTex ("Night lights (Black Marble)", 2D) = "black" {}
        _CloudTex ("Clouds", 2D) = "black" {}
        _NightIntensity ("Night lights intensity", Float) = 1.6
        _CloudSpeed ("Cloud drift (uv/s)", Float) = 0.0015
        _CloudStrength ("Cloud strength", Float) = 0.85
        _AtmoColor ("Atmosphere colour", Color) = (0.30, 0.60, 1.00, 1)
        _AtmoPower ("Atmosphere falloff", Float) = 3.5
        _AtmoStrength ("Atmosphere strength", Float) = 1.4
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }
        Pass
        {
            Name "Forward"
            Tags { "LightMode" = "UniversalForward" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_DayTex);   SAMPLER(sampler_DayTex);
            TEXTURE2D(_NightTex); SAMPLER(sampler_NightTex);
            TEXTURE2D(_CloudTex); SAMPLER(sampler_CloudTex);
            CBUFFER_START(UnityPerMaterial)
                float4 _DayTex_ST;
                float _NightIntensity, _CloudSpeed, _CloudStrength, _AtmoPower, _AtmoStrength;
                float4 _AtmoColor;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float3 normalWS : TEXCOORD1; float3 positionWS : TEXCOORD2; };

            Varyings vert (Attributes v)
            {
                Varyings o;
                VertexPositionInputs p = GetVertexPositionInputs(v.positionOS.xyz);
                o.positionCS = p.positionCS; o.positionWS = p.positionWS;
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.uv = v.uv;
                return o;
            }

            half4 frag (Varyings i) : SV_Target
            {
                float3 n = normalize(i.normalWS);
                Light sun = GetMainLight();
                float ndl = dot(n, sun.direction);
                float lit = saturate(ndl);
                float dayMask = smoothstep(-0.08, 0.22, ndl);

                float3 day = SAMPLE_TEXTURE2D(_DayTex, sampler_DayTex, i.uv).rgb;
                float3 night = SAMPLE_TEXTURE2D(_NightTex, sampler_NightTex, i.uv).rgb;
                float cloud = SAMPLE_TEXTURE2D(_CloudTex, sampler_CloudTex, i.uv + float2(_Time.y * _CloudSpeed, 0)).r * _CloudStrength;

                float3 col = day * sun.color * (lit * 1.15 + 0.015);
                col = lerp(col, sun.color * (lit * 1.05 + 0.01), cloud);                       // clouds catch the light
                col += night * night * _NightIntensity * (1.0 - dayMask) * (1.0 - cloud);      // city lights on the night side

                float3 v = GetWorldSpaceNormalizeViewDir(i.positionWS);
                float fres = pow(1.0 - saturate(dot(n, v)), _AtmoPower);
                col += _AtmoColor.rgb * fres * _AtmoStrength * saturate(ndl + 0.35);           // blue limb on the lit side
                return half4(col, 1);
            }
            ENDHLSL
        }
    }
}

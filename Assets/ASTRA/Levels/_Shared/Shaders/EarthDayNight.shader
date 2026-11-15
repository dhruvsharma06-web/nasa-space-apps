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
        _SpecStrength ("Ocean glint", Float) = 1.6
        _SpecPower ("Ocean glint sharpness", Float) = 60
        _TerminatorColor ("Sunset band", Color) = (1.0, 0.45, 0.18, 1)
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
                float _NightIntensity, _CloudSpeed, _CloudStrength, _AtmoPower, _AtmoStrength, _SpecStrength, _SpecPower;
                float4 _AtmoColor, _TerminatorColor;
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
                float2 cuv = i.uv + float2(_Time.y * _CloudSpeed, 0);
                float cloud = SAMPLE_TEXTURE2D(_CloudTex, sampler_CloudTex, cuv).r * _CloudStrength;
                float cloudShadow = SAMPLE_TEXTURE2D(_CloudTex, sampler_CloudTex, cuv + float2(0.0015, -0.001)).r * _CloudStrength;
                float3 v = GetWorldSpaceNormalizeViewDir(i.positionWS);

                // ocean mask from the Blue Marble colours (water is darker and bluer than land)
                float water = saturate((day.b - max(day.r, day.g) * 0.85) * 6.0) * saturate(1.2 - dot(day, float3(0.6, 0.6, 0.6)) * 2.0);
                float3 h = normalize(sun.direction + v);
                float glint = pow(saturate(dot(n, h)), _SpecPower) * _SpecStrength * water * lit * (1.0 - cloud);

                float3 col = day * (1.0 - cloudShadow * 0.35) * sun.color * (lit * 1.15 + 0.015);
                col += sun.color * glint;                                                       // sunlight glinting on the oceans
                col = lerp(col, sun.color * (lit * 1.05 + 0.01), cloud);                         // clouds catch the light
                float band = exp(-pow(ndl / 0.12, 2.0));                                         // day/night line
                col = lerp(col, col * _TerminatorColor.rgb * 1.6 + _TerminatorColor.rgb * 0.02, band * 0.55 * (0.4 + cloud));   // warm sunset band
                col += night * night * _NightIntensity * (1.0 - dayMask) * (1.0 - cloud * 0.8); // city lights on the night side

                float fres = pow(1.0 - saturate(dot(n, v)), _AtmoPower);
                float3 rim = lerp(_AtmoColor.rgb, _TerminatorColor.rgb, band * 0.7);
                col += rim * fres * _AtmoStrength * saturate(ndl + 0.35);                        // blue limb, warm near the terminator
                return half4(col, 1);
            }
            ENDHLSL
        }
    }
}

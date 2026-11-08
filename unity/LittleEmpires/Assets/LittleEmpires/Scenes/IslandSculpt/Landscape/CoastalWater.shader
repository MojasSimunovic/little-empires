Shader "Little Empires/Coastal Water"
{
    Properties
    {
        _Deep ("Deep water", Color) = (0.055, 0.24, 0.32, 1)
        _Shallow ("Shallow water", Color) = (0.23, 0.58, 0.53, 1)
        _ShoreMask ("Baked depth from shoreline", 2D) = "white" {}
        _IslandRect ("Terrain origin and size", Vector) = (-192, -192, 384, 384)
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment Fragment
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_ShoreMask);
            SAMPLER(sampler_ShoreMask);
            CBUFFER_START(UnityPerMaterial)
                half4 _Deep;
                half4 _Shallow;
                float4 _IslandRect;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; };

            Varyings Vertex(Attributes input)
            {
                Varyings output;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                return output;
            }

            half4 Fragment(Varyings input) : SV_Target
            {
                float2 p = input.positionWS.xz;
                float2 uv = (p - _IslandRect.xy) / _IslandRect.zw;
                float depth = 1;
                if (all(uv >= 0) && all(uv <= 1))
                    depth = SAMPLE_TEXTURE2D(_ShoreMask, sampler_ShoreMask, uv).r;

                float t = _Time.y;
                float swell = sin(dot(p, float2(.48, .92)) + t * .76);
                float ripple = sin(dot(p, float2(-.78, .52)) + t * .51 + swell * .48);
                float crest = pow(saturate(.5 + .5 * swell * ripple), 6);
                float breakup = saturate(.58 + .42 * sin(dot(p, float2(.33, -.19)) - t * .38));
                float shore = (1 - smoothstep(.025, .12, depth)) * smoothstep(.003, .022, depth);
                half3 color = lerp(_Shallow.rgb, _Deep.rgb, smoothstep(.035, .85, depth));
                color += half3(.065, .10, .11) * (crest * breakup * .38 + shore * breakup * .36);
                return half4(color, 1);
            }
            ENDHLSL
        }
    }
}

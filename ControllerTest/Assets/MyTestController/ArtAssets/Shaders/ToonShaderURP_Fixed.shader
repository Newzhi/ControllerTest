Shader "Custom/ToonShaderURP_Fixed"
{
    Properties
    {
        // 主贴图和颜色
        _MainTex ("Texture", 2D) = "white" {}
        _Color("Main Color", Color) = (1,1,1,1)
        
        // 高光
        [HDR] _SpecularColor ("Specular Color", Color) = (1, 1, 1, 1)
        _Gloss ("Gloss", Float) = 20.0
        _SpecularThreshold("Specular Threshold",Range(0.0,1.0)) = 0.5
        
        // 二值化阴影
        _ShadowThreshold("Shadow Threshold", Range(-1.0,1.0)) = 0.0
        _ShadowColor("Shadow Color",Color) = (0.5,0.5,0.5,1.0)
        
        // 描边效果
        _OutlineWidth("OutlineWidth",Range(0.0,3.0)) = 1.0
        _OutlineColor("OutlineColor",Color) = (1.0,1.0,1.0,1.0)
    }
    
    SubShader
    {
        Tags 
        { 
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Geometry"
        }

        // 描边Pass
        Pass
        {
            Name "Outline"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            
            Cull Front
            
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _OutlineWidth;
                half4 _OutlineColor;
                half4 _Color;
            CBUFFER_END

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                
                // 简化的法线外扩方案
                float3 worldPos = TransformObjectToWorld(IN.positionOS.xyz);
                float3 worldNormal = TransformObjectToWorldNormal(IN.normalOS);
                
                // 在法线方向扩展顶点
                worldPos += worldNormal * _OutlineWidth * 0.01;
                
                OUT.positionCS = TransformWorldToHClip(worldPos);
                OUT.uv = IN.uv;
                
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                half3 albedo = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv).rgb * _Color.rgb;
                half3 col = albedo * _OutlineColor.rgb;
                return half4(col, 1.0);
            }
            ENDHLSL
        }

        // 主渲染Pass
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half4 _SpecularColor;
                half _Gloss;
                half _SpecularThreshold;
                half _ShadowThreshold;
                half4 _ShadowColor;
            CBUFFER_END

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float3 positionWS : TEXCOORD2;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = IN.uv;
                OUT.normalWS = TransformObjectToWorldNormal(IN.normalOS);
                OUT.positionWS = TransformObjectToWorld(IN.positionOS.xyz);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                // 获取光照信息
                Light mainLight = GetMainLight();
                
                // 法线和光照方向
                half3 normalWS = normalize(IN.normalWS);
                half3 lightDirWS = normalize(mainLight.direction);
                
                // 视角方向
                half3 viewDirWS = normalize(GetWorldSpaceViewDir(IN.positionWS));
                
                // 半向量（用于高光计算）
                half3 halfDirWS = normalize(lightDirWS + viewDirWS);
                
                // 基础颜色
                half3 albedo = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv).rgb * _Color.rgb;
                
                // 环境光（拍平法线，统一给向上的方向）
                half3 ambient = SampleSH(half3(0.0, 1.0, 0.0));
                
                // 漫反射（二值化）
                half nl = dot(lightDirWS, normalWS);
                half3 diffuse = nl > _ShadowThreshold ? 1.0 : _ShadowColor.rgb;
                
                // 高光
                half nh = dot(normalWS, halfDirWS);
                half3 specular = pow(max(nh, 1e-5), _Gloss) > _SpecularThreshold ? 
                    _SpecularColor.rgb : 0.0;
                
                // 最终颜色
                half3 col = ambient * albedo + (diffuse + specular) * albedo * mainLight.color;
                return half4(col, 1.0);
            }
            ENDHLSL
        }
    }
    
    FallBack "Universal Render Pipeline/Lit"
} 
Shader "Custom/URPShaderTemplate"
{
    Properties
    {
        _MainTex ("主纹理", 2D) = "white" {}
        _Color ("主颜色", Color) = (1,1,1,1)
        _NormalMap ("法线贴图", 2D) = "bump" {}
        _Metallic ("金属度", Range(0,1)) = 0
        _Smoothness ("光滑度", Range(0,1)) = 0.5
        _EmissionColor ("发光颜色", Color) = (0,0,0,1)
    }

    SubShader
    {
        Tags 
        { 
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Geometry"
        }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            // 包含必要的库
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Input.hlsl"

            // 声明纹理和采样器
            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            TEXTURE2D(_NormalMap);
            SAMPLER(sampler_NormalMap);

            // 常量缓冲区
            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half _Metallic;
                half _Smoothness;
                half4 _EmissionColor;
            CBUFFER_END

            // 顶点着色器输入结构
            struct Attributes
            {
                float4 positionOS : POSITION;        // 模型空间位置
                float3 normalOS : NORMAL;            // 模型空间法线
                float4 tangentOS : TANGENT;          // 模型空间切线
                float2 uv : TEXCOORD0;               // UV坐标
            };

            // 顶点着色器输出结构
            struct Varyings
            {
                float4 positionCS : SV_POSITION;     // 裁剪空间位置
                float2 uv : TEXCOORD0;               // UV坐标
                float3 normalWS : TEXCOORD1;         // 世界空间法线
                float3 positionWS : TEXCOORD2;       // 世界空间位置
                float3 tangentWS : TEXCOORD3;        // 世界空间切线
                float3 bitangentWS : TEXCOORD4;      // 世界空间副切线
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                
                // 位置变换
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.positionWS = TransformObjectToWorld(IN.positionOS.xyz);
                
                // 法线变换
                OUT.normalWS = TransformObjectToWorldNormal(IN.normalOS);
                
                // 切线变换
                OUT.tangentWS = TransformObjectToWorldDir(IN.tangentOS.xyz);
                OUT.bitangentWS = cross(OUT.normalWS, OUT.tangentWS) * IN.tangentOS.w;
                
                // UV坐标
                OUT.uv = IN.uv;
                
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                // 获取主光源
                Light mainLight = GetMainLight();
                
                // 采样纹理
                half4 albedo = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv) * _Color;
                
                // 法线贴图
                half3 normalTS = UnpackNormal(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, IN.uv));
                
                // 构建TBN矩阵
                half3x3 tangentToWorld = half3x3(IN.tangentWS, IN.bitangentWS, IN.normalWS);
                half3 normalWS = normalize(TransformTangentToWorld(normalTS, tangentToWorld));
                
                // 光照方向
                half3 lightDirWS = normalize(mainLight.direction);
                
                // 视角方向（正确的URP函数）
                half3 viewDirWS = normalize(GetWorldSpaceViewDir(IN.positionWS));
                
                // 半向量（用于Blinn-Phong高光）
                half3 halfDirWS = normalize(lightDirWS + viewDirWS);
                
                // 环境光
                half3 ambient = SampleSH(normalWS);
                
                // 漫反射
                half NdotL = dot(normalWS, lightDirWS);
                half3 diffuse = mainLight.color * albedo.rgb * max(0, NdotL);
                
                // 高光（Blinn-Phong）
                half NdotH = dot(normalWS, halfDirWS);
                half3 specular = mainLight.color * pow(max(0, NdotH), _Smoothness * 100);
                
                // 最终颜色
                half3 finalColor = ambient * albedo.rgb + diffuse + specular + _EmissionColor.rgb;
                
                return half4(finalColor, albedo.a);
            }
            ENDHLSL
        }
    }
    
    FallBack "Universal Render Pipeline/Lit"
} 
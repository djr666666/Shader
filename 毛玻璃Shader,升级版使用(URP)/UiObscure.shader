Shader "Custom/ExtremeGaussianBlur"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _BlurSize ("Blur Size", Range(0, 50)) = 10.0
        _Color ("Color Tint", Color) = (1,1,1,1)
    }

    SubShader
    {
        Tags 
        { 
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
        }

        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off
        ZWrite Off

        Pass
        {
            Name "ExtremeGaussianBlur"
            
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

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
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float4 _MainTex_TexelSize;
            float4 _Color;
            float _BlurSize;

            v2f vert(appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.vertex = TransformObjectToHClip(v.vertex.xyz);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }

            // 自适应采样范围的高斯模糊
            float4 blurAdaptive(float2 uv, float2 texelSize, float radius)
            {
                float4 color = 0;
                float totalWeight = 0;
                
                // 根据半径动态调整采样范围
                // 半径越大，采样范围越大
                int sampleRange = ceil(radius * 2.0); // 采样范围大约是半径的1.5倍
                sampleRange = min(sampleRange, 60); // 限制最大采样范围，防止性能爆炸
                
                // 使用更大的采样范围
                for (int x = -sampleRange; x <= sampleRange; x++)
                {
                    for (int y = -sampleRange; y <= sampleRange; y++)
                    {
                        // 计算距离
                        float dist = sqrt(float(x*x + y*y));
                        
                        // 高斯权重公式，使用更平滑的sigma
                        float sigma = radius * 0.6;
                        float weight = exp(-(dist * dist) / (2.0 * sigma * sigma));
                        
                        // 如果距离太远，权重会非常小，可以忽略
                        if (weight < 0.01)
                            continue;
                        
                        // 采样偏移
                        float2 offset = float2(x * texelSize.x, y * texelSize.y);
                        color += tex2D(_MainTex, uv + offset) * weight;
                        totalWeight += weight;
                    }
                }
                
                // 归一化
                return color / max(totalWeight, 0.001);
            }

            float4 frag(v2f i) : SV_Target
            {
                float2 uv = i.uv;
                float2 texelSize = _MainTex_TexelSize.xy;
                
                // 使用自适应范围的高斯模糊
                float4 result = blurAdaptive(uv, texelSize, _BlurSize);
                
                // 应用颜色色调
                result *= _Color;
                
                return result;
            }
            ENDHLSL
        }
    }
    
    FallBack "UI/Default"
}
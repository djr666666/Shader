// Upgrade NOTE: replaced '_Object2World' with 'unity_ObjectToWorld'

Shader "Unlit/Toon"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Diffuse ("_Diffuse", COLOR) = (1,1,1,1)
       _ToonEffectPow("_ToonEffectPow",Range(0,1))=1
        _ToonDiffuseMap ("_ToonDiffuseMap", 2D) = "white" {}

[Space(40)]
        _Specular("_Specular",COLOR)=(1,1,1,1)
        _Gloss("_Gloss",Range(1,256))=20
_SpecularMin("_SpecularMin",Range(0,1))=0.2
_SpecularMax("_SpecularMax",Range(0,1))=0.8
_SpecularSmooth("_SpecularSmooth",Range(0,1))=0.5



[Space(40)]
_RimColor("_RimColor",COLOR)=(1,1,1,1)
_RimMin("_RimMin",Range(0,1))=0.2
_RimMax("_RimMax",Range(0,1))=0.8
_RimSmooth("_RimSmooth",Range(0,1))=0.5
_RimBloomExp("_RimBloomExp",Range(0,8))=0.1
_RimBloomMulti("_RimBloomMulti",Range(0,8))=1
[Space(40)]
        _OutLineColor ("_OutLineColor", COLOR) = (1,1,1,1)
       _OutlineWidth("_OutlineWidth",float)=1.1

    }
    SubShader
    {
        
        Tags { "RenderType"="Opaque"  }
        Pass
        {
            CGPROGRAM

            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"
            #include "Lighting.cginc"
            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                fixed3 normal:NORMAL;
                float4 tangent : TANGENT;

            };
            struct v2f
            {
				float4 COLOR:COLOR;
                float2 uv : TEXCOORD0;
                fixed3 worldNormal:NORMAL;
                float3 worldPos : TEXTCOORD2;
                float3 tangent : TANGENT;

                float4 vertex : SV_POSITION;

            };
            fixed4 _Diffuse;

            sampler2D _MainTex;
sampler2D _ToonDiffuseMap;
            float4 _MainTex_ST;
float4 _RimColor;
float _RimMin;
float _RimMax;
float _RimSmooth;
float _RimBloomExp;
float _RimBloomMulti;


float _ToonEffectPow;
fixed4 _Specular;
float _Gloss;
float _SpecularMin;
float _SpecularMax;
float _SpecularSmooth;

            v2f vert (appdata v)
            {
               v2f o = (v2f)0;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.worldNormal = UnityObjectToWorldNormal(v.normal);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
			    o.tangent=UnityWorldToObjectDir(v.tangent);
                return o;
            }


            fixed4 frag (v2f i) : SV_Target
            {

                fixed3 ambient = UNITY_LIGHTMODEL_AMBIENT.xyz;
                half4 mainTex = tex2D (_MainTex, i.uv);
                half3 worldNormal = normalize(i.worldNormal);
                half3 worldLightDir = normalize(UnityWorldSpaceLightDir(i.worldPos));



                half halfLambert = dot(worldNormal, worldLightDir) * 0.5 + 0.5;
				//float Lambert=saturate( dot(worldLightDir,worldNormal)); 
                  float _lan=halfLambert;
                _lan=smoothstep(0,1,_lan);

                float toon=  tex2D(_ToonDiffuseMap,float2(0,_lan)) .g;
                toon = lerp(_lan,toon, _ToonEffectPow); 
                _lan=toon;

           fixed3 diffuse=_Diffuse.rgb*mainTex.rgb*_LightColor0.rgb*_lan;



fixed3 reflectDir=normalize(reflect(-worldLightDir,worldNormal));
half3 viewDir = normalize(UnityWorldSpaceViewDir(i.worldPos));
half spec=pow(saturate(dot(reflectDir,viewDir)),_Gloss);


half toon_spec=smoothstep(_SpecularMin,_SpecularMax,spec);
toon_spec=smoothstep(0,_SpecularSmooth,toon_spec);
fixed3 specular=_LightColor0.rgb*_Specular.rgb*lerp(spec,toon_spec, _ToonEffectPow);


  half f =  1.0 -saturate(dot(viewDir, worldNormal));
    half rim=smoothstep(_RimMin,_RimMax,f);
    rim = smoothstep(0, _RimSmooth, rim);
half NdotL = max(0, dot(worldNormal, worldLightDir));
    half rimBloom = pow (f, _RimBloomExp) * _RimBloomMulti  * (NdotL);
    //col.a = rimBloom;


    half3 rimColor = rim * _RimColor.rgb *  _RimColor.a*_LightColor0.rgb*rimBloom;

fixed3 color=ambient+(diffuse+specular+rimColor);
//color.rgb=toon;
return fixed4(color,rimBloom);


            }
            ENDCG
        }
    Pass
    {
        Tags {"LightMode"="ForwardBase"}

            Cull Front

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            half _OutlineWidth;
            half4 _OutLineColor;

            struct a2v 
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float2 uv : TEXCOORD0;
                float4 vertColor : COLOR;
                float4 tangent : TANGENT;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 vertColor : COLOR;

            };


            v2f vert (a2v v) 
            {
v2f o;
UNITY_INITIALIZE_OUTPUT(v2f, o);
float4 pos = UnityObjectToClipPos(v.vertex);
float3 viewNormal = mul((float3x3)UNITY_MATRIX_IT_MV, v.normal.xyz);

//float3 viewNormal = mul((float3x3)UNITY_MATRIX_IT_MV, v.tangent.xyz);


float3 ndcNormal = normalize(TransformViewToProjection(viewNormal.xyz)) * pos.w;//将法线变换到NDC空间
float4 nearUpperRight = mul(unity_CameraInvProjection, float4(1, 1, UNITY_NEAR_CLIP_VALUE, _ProjectionParams.y));//将近裁剪面右上角位置的顶点变换到观察空间
float aspect = abs(nearUpperRight.y / nearUpperRight.x);//求得屏幕宽高比
ndcNormal.x *= aspect;
pos.xy += 0.01 * _OutlineWidth * ndcNormal.xy;
o.pos = pos;
       o.vertColor = v.vertColor.rgb;


                return o;
            }

            half4 frag(v2f i) : SV_TARGET 
            {

      return fixed4(_OutLineColor * i.vertColor, 1);//顶点色rgb通道控制描边颜色

                //return _OutLineColor;
            }
            ENDCG
        }
   
    }

}

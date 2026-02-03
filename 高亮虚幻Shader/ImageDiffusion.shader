Shader "Hidden/ImageDiffusion"
{
    Properties
    {
      _MainTex ("Texture", 2D) = "white" {}
        _Speed("_Speed",range(0,2))=0.5
        _Gap("gap",float)=2

    }
    SubShader
    {
        // No culling or depth
        Cull Off ZWrite Off ZTest Always
        tags{
            "Queue"="Transparent"
        }
        blend srcalpha oneminussrcalpha
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }
            float _Speed;
            float _Gap;
            sampler2D _MainTex;
            fixed4 get_col(float2 mid,float2 uv,float delay)
            {
                float2 dv = mid - uv;  
                float2 offset = dv  * fmod(_Time.y*_Speed+delay,1)/_Gap; 
                float2 uv2 = offset + uv;  
                fixed4 col= tex2D(_MainTex, uv2);  
                return col;
            }
            float get_dis(float2 uv)
            {
                float left=uv.x;
                float right=1-uv.x;
                float top=uv.y;
                float bottom=1-uv.y;
                return min( min(left,right),min(top,bottom));
            }
            fixed4 frag (v2f i) : SV_Target
            {
                float2 mid = float2(0.5,0.5);
                fixed4 col_0=get_col(mid,i.uv,0);
                fixed4 col_1=get_col(mid,i.uv,0.33);
                fixed4 col_2=get_col(mid,i.uv,0.667);
                fixed4 col=col_0+col_1+col_2;
                float dis=get_dis(i.uv);
                col.a *= dis + dis*3;
                return col;
            }
            ENDCG
        }
    }
}

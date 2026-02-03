Shader "Hidden/ProgressAnimation"
{
     Properties
    {
        [NoScaleOffset]_MainTex("Image Sequence", 2D) = "white" {}
        _HorizontalAmount("Horizontal Amount", Float) = 8    //对于样例中的图片，横纵需设为8，不要忘记
        _VerticalAmount("VerticalAmount", Float) = 8
       _Progress("_Progress",float)=1
    }
    SubShader
    {
        Tags { "Queue" = "Transparent" }     //当成半透明对象处理
        PASS
        {
            cull off
            ZWrite Off
            ztest off
            Blend SrcAlpha OneMinusSrcAlpha
 
            CGPROGRAM
            #pragma vertex vert             //声明顶点着色器的函数
            #pragma fragment frag           //声明片段着色器的函数
            #include "UnityCG.cginc"
 
            fixed4 _Color;
            float _HorizontalAmount;
            float _VerticalAmount;
fixed _Progress;
            sampler2D _MainTex;
 
            float4 _MainTex_ST;
            struct _2vert 
            {
                float4 vertex: POSITION;
                float4 texcoord: TEXCOORD0;
            };
            struct vert2frag 
            {
                float4 pos: SV_POSITION;
                float2 uv: TEXCOORD0;
            };
 
            vert2frag vert(_2vert v) 
            {
                vert2frag o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.texcoord, _MainTex);
                return o;
            }
            fixed4 frag(vert2frag i) : SV_Target
            {
                float time = floor(_Progress);
                float row = floor(time / _HorizontalAmount);
                float column = time - row * _HorizontalAmount;
                row %= _VerticalAmount;
 
                half2 uv = i.uv + half2(column, _HorizontalAmount - row);
                uv.x /= _HorizontalAmount;
                uv.y /= _VerticalAmount;
 
                fixed4 color = tex2D(_MainTex, uv);
                return color; 
            } 
            ENDCG
        }
    }
}

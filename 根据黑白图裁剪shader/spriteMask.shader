Shader "Hidden/spriteMask"
{

    //*****mask图片必须是黑白图 扣白色的  jpg不是 png png 带同名通道
    Properties
    {
        _MainTex("_MainTex", 2D) = "white" {}
        _Mask("_Mask",2D)= "black" {}

        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255

        _ColorMask ("Color Mask", Float) = 15

        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0

    }
    SubShader
    {
 Tags{"Queue"="Transparent" }
        // No culling or depth
        Stencil 
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }
        ColorMask [_ColorMask]
        Cull Off 
        ZWrite Off 
        ZTest Always


blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP
            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
float4 color:COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID

            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
float4 color:COLOR;
                float4 worldPosition : TEXCOORD1;

                UNITY_VERTEX_OUTPUT_STEREO

            };
            sampler2D _MainTex;
fixed4 _MainTex_ST;

sampler2D _Mask;
            float4 _ClipRect;

            v2f vert (appdata v)
            {
                v2f o;
             UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                TRANSFORM_TEX(v.uv, _MainTex);
                o.worldPosition = v.vertex;

o.color=v.color;
                return o;
            }



            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 col = tex2D(_MainTex, i.uv*_MainTex_ST.xy+_MainTex_ST.zw);
                fixed4 _Maskcol = tex2D(_Mask, i.uv);
col.a*=1-_Maskcol.r;
//return _BGcol;
                fixed4 color = col;
      



  #ifdef UNITY_UI_CLIP_RECT
                color.a *= UnityGet2DClipping(i.worldPosition.xy, _ClipRect);
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                clip (color.a - 0.001);
                #endif
               return color;
            }
            ENDCG
        }
    }
}

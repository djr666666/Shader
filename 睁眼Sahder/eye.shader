Shader "Hidden/eye"
{
    Properties
    {
_BaseColor("_BaseColor",COLOR)=(0,0,0,1)

       [HideInInspector] _MainTex ("Texture", 2D) = "white" {}
        _Progress ("Progress", Range(0, 2)) = 1
    _ArchHeight ("Arch Height", Range (0, 2)) =1
 _AcuityPow("_AcuityPow",float)=300
_offset("_offset",Range(0,0.5))=0.1
    }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always
Tags{"Queue"="Transparent"}
blend SrcAlpha OneMinusSrcAlpha
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

float _Progress;
float _ArchHeight;
 float _AcuityPow;
fixed4 _BaseColor;
float _offset;
            fixed4 frag (v2f i) : SV_Target
            {



fixed4 col=_BaseColor;



float2 uv=i.uv;
    float upBorder = .5 + _Progress * (.5 + _ArchHeight);
    float downBorder = .5 - _Progress * (.5 + _ArchHeight);

    upBorder -=  _ArchHeight * pow(uv.x - .5, 2)-_offset;
    downBorder += _ArchHeight * pow(uv.x - .5, 2)-_offset;

downBorder=pow(smoothstep(0,1,uv.y-downBorder),_AcuityPow);
upBorder=pow(smoothstep(0,1,upBorder-uv.y),_AcuityPow);


    float visibleV = (1 -step(upBorder, uv.y) ) * (step(downBorder, uv.y));
     visibleV = upBorder * downBorder;
col.a=1-visibleV;
return col;



            }
            ENDCG
        }
    }
}

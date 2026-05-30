Shader "Unlit/Window"
{
Properties
{
_MainTex ("Texture", 2D) = "white" {}  // 主纹理
_Size("Size",float) = 1  // 大小
_T("Time",float) = 1  // 时间
_Distortion("Distortion", range(-5, 5)) = 1  // 扭曲
_Blur("Blur",range(0, 1)) = 1  // 模糊程度
}
SubShader
{
Tags { "RenderType"="Opaque" "Queue"="Transparent"}  // 标签
LOD 100
    GrabPass{"_GrabTexture"}  // GrabPass命令将渲染窗口中的内容复制到纹理_GrabTexture中
    Pass
    {
        CGPROGRAM
        #pragma vertex vert
        #pragma fragment frag
        // make fog work
        #pragma multi_compile_fog
        #define S(a, b, t) smoothstep(a,b,t)  // 定义了计算平滑插值的宏函数
        #include "UnityCG.cginc"  // Unity内置着色器代码宏

        struct appdata
        {
            float4 vertex : POSITION;  // 顶点位置
            float2 uv : TEXCOORD0;  // 纹理坐标
        };

        struct v2f
        {
            float2 uv : TEXCOORD0;  // 纹理坐标
            float4 grabUv : TEXCOORD1;  // 抓取窗口纹理坐标
            UNITY_FOG_COORDS(1)  // Unity内置的雾效相关宏
            float4 vertex : SV_POSITION;  // 顶点位置
        };

        sampler2D _MainTex,_GrabTexture;  // 主纹理和抓取窗口纹理
        float4 _MainTex_ST;  // 主纹理的平铺和偏移参数
        float _Size, _T , _Distortion, _Blur;  // 大小、时间、扭曲和模糊程度参数

        v2f vert (appdata v)
        {
            v2f o;
            o.vertex = UnityObjectToClipPos(v.vertex);  // 将顶点从对象空间转换到剪裁空间
            o.uv = TRANSFORM_TEX(v.uv, _MainTex);  // 对纹理坐标进行平铺和偏移变换
            o.grabUv = UNITY_PROJ_COORD(ComputeGrabScreenPos(o.vertex));  // 计算抓取窗口的纹理坐标

            UNITY_TRANSFER_FOG(o,o.vertex);  // 将雾效信息传递给片段着色器
            return o;
        }

        float N21(float2 p)
        {
            p = frac(p*float2(123.34,345.45));  // 将纹理坐标乘以固定值并取小数部分
            p += dot(p,p + 34.345);  // 点乘纹理坐标并加上一定值
            return frac(p.x*p.y);  // 返回取小数部分后的乘积
        }

        float3 Layer(float2 UV,float t)
        {
            float2 aspect = float2(2,1);  // 窗口宽高比
            float2 uv = UV*_Size * aspect;  // 对纹理坐标进行平铺和偏移变换
            uv.y += t * .25;  // 根据时间进行纹理坐标的偏移
            float2 gv = frac(uv)-.5;  // 将纹理坐标取小数部分后再减去0.5的值

            float2 id = floor(uv);  // 取纹理坐标的整数部分

            float n = N21(id);  // 计算噪声值
            t += n*6.2831;  // 根据噪声值进行时间的调整

            float w = UV .y * 10;  // 根据纹理坐标的y值进行宽度调整
            float x = (n - .5)*.8;  // 根据噪声值进行x的调整
            x += (.4-abs(x)) * sin(3*w)*pow(sin(w),6)*.45;  // 根据宽度和噪声进行x的进一步调整

            float y = -sin(t+sin(t+sin(t)*.5))*.45;  // 根据时间和噪声进行y的调整
            y -= (gv.x-x)*(gv.x-x);  // 根据x与纹理坐标的偏移进行y的进一步调整

            float2 dropPos = (gv-float2(x,y)) / aspect;  // 计算扭曲的纹理坐标
            float drop= S(.05,.03,length(dropPos));  // 计算扭曲的强度

            float2 trailPos = (gv-float2(x,t * .25)) / aspect;  // 计算轨迹的纹理坐标
            trailPos.y = (frac(trailPos.y * 8)-.5)/8;  // 计算纹理坐标的小数部分并进行调整
            float trail = S(.03,.01,length(trailPos));  // 计算轨迹的强度
            float fogTrail = S(-.05,.05,dropPos.y);  // 根据扭曲的纹理坐标的y值进行雾效的调整
            fogTrail *= S(.5, y, gv.y);  // 根据y和纹理坐标的y值再次进行雾效的调整
            trail *=fogTrail;  // 综合计算轨迹的强度

            fogTrail *= S(.05, .04, abs(dropPos.x));  // 根据扭曲的纹理坐标的x值进行雾效的进一步调整

            float2 offs = drop*dropPos + trail * trailPos;  // 计算扭曲和轨迹的综合效果

            return float3(offs,fogTrail);  // 返回扭曲、轨迹和雾效混合后的结果
        }

        fixed4 frag (v2f i) : SV_Target
        {
            float t = fmod(_Time.y +_T,7200);  // 计算时间

            float4 col = 0;  // 初始化颜色

            float3 drops = Layer(i.uv,t);  // 计算扭曲、轨迹和雾效
            drops += Layer(i.uv*1.23 + 7.54,t);  // 对纹理坐标进行平铺和偏移变换并再次计算扭曲、轨迹和雾效
            drops += Layer(i.uv*1.35 + 1.54,t);  // 对纹理坐标进行平铺和偏移变换并再次计算扭曲、轨迹和雾效
            drops += Layer(i.uv*1.57 - 7.54,t);  // 对纹理坐标进行平铺和偏移变换并再次计算扭曲、轨迹和雾效

            float fade = 1-saturate(fwidth(i.uv)*60);  // 根据纹理坐标的宽度计算淡化系数

            float blur = _Blur * 7 * (1-drops.z * fade);  // 根据淡化系数和扭曲、轨迹和雾效的深度计算模糊程度

            //col = tex2Dlod(_MainTex, float4(i.uv + drops.xy * _Distortion,0,blur));

            float2 projUv = i.grabUv.xy / i.grabUv.w;  // 根据抓取窗口的纹理坐标计算投影纹理坐标
            projUv += drops.xy * _Distortion * fade;  // 根据扭曲、轨迹和雾效的深度进行投影纹理坐标的调整
            blur *= .01;  // 调整模糊的程度

            const float numSamples = 32;  // 采样次数
            float a = N21(i.uv)*6.2831*0;  // 根据纹理坐标计算角度
            for(float i = 0; i < numSamples; i++)
            {
                float2 offs = float2(sin(a),cos(a))*blur;  // 根据角度和模糊程度计算偏移量
                float d = frac(sin((i+1)*546.)*5424.);  // 根据角度计算采样步长
                d = sqrt(d);  // 开平方根
                offs *= d;  // 根据采样步长调整偏移量
                col += tex2D(_GrabTexture,projUv + offs);  // 对抓取窗口纹理进行采样
                a++;
            }
            col /= numSamples;  // 对颜色进行平均

            //col *= 0; col += fade;

            return col*.9;  // 返回最终颜色
        }
        ENDCG
    }
}
}
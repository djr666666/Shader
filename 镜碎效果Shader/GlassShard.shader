Shader "Custom/GlassShard"
{
    // 物理折射液态玻璃碎片：把碎片当成一块有厚度、边缘带斜面(穹顶)的实体玻璃。
    //   1) 顶点数/位置可自定义的多边形 SDF -> 圆角可调的碎片轮廓
    //   2) 由 SDF 距离构造“边缘斜面 + 平顶”的高度场，算出表面法线
    //   3) refract() 物理折射光线穿过玻璃打到底面平面，采样背景 -> 真实透镜畸变
    //   4) RGB 三通道用不同 IOR 采样 -> 色散(边缘彩边)
    //   5) 菲涅尔边缘反光 + 方向高光 -> 玻璃锐利反光
    // 背景 _MainTex 可用 _TexPan/_TexZoom 在碎片内部自由移动/缩放，且只显示在碎片范围内。
    Properties
    {
        [Header(Background)]
        _MainTex ("Background (RGB)", 2D) = "white" {}
        _Tint ("Glass Tint", Color) = (1,1,1,1)
        _TexPan ("Tex Pan (内容平移)", Vector) = (0,0,0,0)
        _TexZoom ("Tex Zoom (内容缩放)", Range(0.1, 5.0)) = 1.0

        [Header(Shape)]
        _Center ("Center (UV)", Vector) = (0.5, 0.5, 0, 0)
        _Size ("Size", Range(0.05, 1.0)) = 0.45
        _Round ("Corner Round (圆角程度)", Range(0.0, 0.3)) = 0.02
        _Rotation ("Rotation", Range(0, 6.2832)) = 0.0
        _Edge ("Edge Softness", Range(0.0005, 0.03)) = 0.0005

        [Header(Polygon Vertices)]
        [IntRange] _VertCount ("Vertex Count (顶点数)", Range(3, 8)) = 6
        _V0 ("V0", Vector) = ( 0.00,  0.95, 0, 0)
        _V1 ("V1", Vector) = ( 0.68,  0.22, 0, 0)
        _V2 ("V2", Vector) = ( 0.50, -0.62, 0, 0)
        _V3 ("V3", Vector) = (-0.28, -0.90, 0, 0)
        _V4 ("V4", Vector) = (-0.78, -0.10, 0, 0)
        _V5 ("V5", Vector) = (-0.45,  0.58, 0, 0)
        _V6 ("V6", Vector) = ( 0.20,  0.80, 0, 0)
        _V7 ("V7", Vector) = (-0.10, -0.50, 0, 0)

        [Header(Glass Body)]
        _Thickness ("Bevel Thickness (边缘斜面宽度)", Range(0.005, 0.4)) = 0.05
        _IOR ("Index of Refraction (折射率)", Range(1.0, 2.0)) = 1.2
        _BaseHeight ("Base Height (折射强度)", Range(0.0, 2.0)) = 0.6
        _Dispersion ("Chromatic Dispersion (色散)", Range(0.0, 0.3)) = 0.05

        [Header(Surface)]
        [Toggle] _FrostOn ("Frosted Glass (磨砂开关)", Float) = 0
        _Frost ("Frost Amount (整体磨砂强度)", Range(0.0, 40.0)) = 6.0
        _BlurSize ("Edge Blur (边缘磨砂)", Range(0.0, 40.0)) = 40
        _Fresnel ("Fresnel Rim (菲涅尔反光)", Range(0.0, 3.0)) = 3
        _SpecStrength ("Specular Highlight", Range(0.0, 3.0)) = 1.8
        _LightAngle ("Light Angle", Range(0, 6.2832)) = 2.3
        _ShadowStrength ("Drop Shadow", Range(0.0, 0.6)) = 0.25

        [Header(Scan Sweep)]
        [Toggle] _ScanOn ("Scan Sweep (扫光开关)", Float) = 1
        [HDR] _ScanColor ("Scan Color (HDR)", Color) = (1, 1, 1, 1)
        _ScanAngle ("Scan Angle (扫光角度)", Range(0, 6.2832)) = 5.67
        _ScanWidth ("Scan Width (线宽)", Range(0.001, 0.5)) = 0.04
        _ScanSpeed ("Scan Speed (周期速度)", Range(0.0, 3.0)) = 1.5
        _ScanInterval ("Scan Interval (间隔停顿)", Range(0.0, 5.0)) = 5.0
        _ScanIntensity ("Scan Intensity", Range(0.0, 5.0)) = 0.53
        _ScanSpecBoost ("Scan Spec Boost (近高光增强)", Range(0.0, 8.0)) = 8.0

        [Header(Cracks)]
        _CrackStrength ("Crack Strength (裂纹强度)", Range(0.0, 2.0)) = 0.0
        _CrackScale ("Crack Density (蛛网密度)", Range(2.0, 30.0)) = 2
        _CrackWidth ("Crack Width (线宽)", Range(0.001, 0.06)) = 0.0064
        _CrackCenter ("Impact Center (冲击点)", Vector) = (0, 0, 0, 0)
        _RadialCount ("Radial Cracks (放射裂纹数)", Range(0.0, 16.0)) = 0
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off
        ZWrite Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            #define PI 3.14159265

            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            float4 _MainTex_ST;
            float4 _Tint;
            float4 _TexPan;
            float _TexZoom;

            float4 _Center;
            float _Size;
            float _Round;
            float _Rotation;
            float _Edge;

            float _VertCount;
            float4 _V0, _V1, _V2, _V3, _V4, _V5, _V6, _V7;

            float _Thickness;
            float _IOR;
            float _BaseHeight;
            float _Dispersion;

            float _FrostOn;
            float _Frost;
            float _BlurSize;
            float _Fresnel;
            float _SpecStrength;
            float _LightAngle;
            float _ShadowStrength;
            float _CrackStrength;
            float _CrackScale;
            float _CrackWidth;
            float4 _CrackCenter;
            float _RadialCount;

            float _ScanOn;
            float4 _ScanColor;
            float _ScanAngle;
            float _ScanWidth;
            float _ScanSpeed;
            float _ScanInterval;
            float _ScanIntensity;
            float _ScanSpecBoost;

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }

            float2x2 Rot (float a)
            {
                float c = cos(a), s = sin(a);
                return float2x2(c, -s, s, c);
            }

            // 自定义不规则多边形 SDF（iq）。顶点数与位置由材质属性决定（3~8 个）。
            float sdPolygon (float2 p)
            {
                float2 v[8];
                v[0] = _V0.xy; v[1] = _V1.xy; v[2] = _V2.xy; v[3] = _V3.xy;
                v[4] = _V4.xy; v[5] = _V5.xy; v[6] = _V6.xy; v[7] = _V7.xy;

                int N = (int)clamp(_VertCount, 3.0, 8.0);

                float d = dot(p - v[0], p - v[0]);
                float s = 1.0;
                int j = N - 1;
                for (int i = 0; i < N; i++)
                {
                    float2 vi = v[i];
                    float2 vj = v[j];
                    float2 e = vj - vi;
                    float2 w = p - vi;
                    float2 b = w - e * clamp(dot(w, e) / dot(e, e), 0.0, 1.0);
                    d = min(d, dot(b, b));

                    bool cx = p.y >= vi.y;
                    bool cy = p.y <  vj.y;
                    bool cz = (e.x * w.y) > (e.y * w.x);
                    if ((cx && cy && cz) || (!cx && !cy && !cz)) s = -s;

                    j = i;
                }
                return s * sqrt(d);
            }

            // 碎片 SDF：在“等高(按高度归一化、isotropic)”的 F 空间求值，返回带符号距离(F 单位)。
            float ShapeSD (float2 F, float aspect)
            {
                float2 Fc = float2(_Center.x * aspect, _Center.y);
                float2 st = F - Fc;
                st = mul(Rot(_Rotation), st);
                float2 p = st / max(_Size, 1e-4);
                return sdPolygon(p) * max(_Size, 1e-4) - _Round;
            }

            // —— 程序化玻璃裂纹 ——
            float hash1 (float n) { return frac(sin(n * 127.1) * 43758.5453); }
            float2 hash2 (float2 p)
            {
                p = float2(dot(p, float2(127.1, 311.7)), dot(p, float2(269.5, 183.3)));
                return frac(sin(p) * 43758.5453);
            }

            // Voronoi 到“最近单元边界”的距离（iq）。边界即裂纹缝。
            float voronoiEdge (float2 x)
            {
                float2 n = floor(x);
                float2 f = frac(x);

                float2 mr = 0;
                float md = 8.0;
                // 第一遍：找最近特征点
                for (int j = -1; j <= 1; j++)
                for (int i = -1; i <= 1; i++)
                {
                    float2 g = float2(i, j);
                    float2 o = hash2(n + g);
                    float2 r = g + o - f;
                    float d = dot(r, r);
                    if (d < md) { md = d; mr = r; }
                }
                // 第二遍：到相邻单元中垂线(即边界)的距离
                md = 8.0;
                for (int j2 = -2; j2 <= 2; j2++)
                for (int i2 = -2; i2 <= 2; i2++)
                {
                    float2 g = float2(i2, j2);
                    float2 o = hash2(n + g);
                    float2 r = g + o - f;
                    float2 diff = mr - r;
                    if (dot(diff, diff) > 1e-5)
                        md = min(md, dot(0.5 * (mr + r), normalize(r - mr)));
                }
                return md;
            }

            // 裂纹场：放射状主裂纹 + 蛛网碎裂网，范围 0~1（1=裂纹中心）
            float CrackField (float2 p)
            {
                float2 c = _CrackCenter.xy;
                float2 d = p - c;
                float r = length(d);

                // 1) 蛛网碎裂网（Voronoi 边界）
                float ev  = voronoiEdge(p * _CrackScale + 13.7);
                float web = 1.0 - smoothstep(0.0, _CrackWidth * _CrackScale, ev);
                web *= lerp(0.12, 1.0, saturate(1.0 - r / 1.6));   // 越靠冲击点越密

                // 2) 放射状主裂纹（带摆动、向外渐细渐隐）
                float radial = 0.0;
                if (_RadialCount >= 1.0)
                {
                    float ang = atan2(d.y, d.x);
                    float sectorId = floor((ang + PI) / (2.0 * PI) * _RadialCount);
                    float wob = sin(r * 16.0 + hash1(sectorId) * 6.2831) * 0.05;
                    float a = (ang / (2.0 * PI) + 0.5) * _RadialCount + wob * _RadialCount;
                    float fa = abs(frac(a) - 0.5);                       // 角向距离(0=裂纹中心)
                    float lineDist = fa * (2.0 * PI / max(_RadialCount, 1.0)) * r; // 转空间垂直距离
                    radial = 1.0 - smoothstep(0.0, _CrackWidth, lineDist);
                    radial *= smoothstep(0.0, 0.05, r);                  // 冲击点中心留撞击坑
                    radial *= 1.0 - smoothstep(1.2, 1.9, r);            // 远处淡出
                }

                return saturate(max(web, radial));
            }

            // 玻璃顶面高度（穹顶剖面）：边缘斜面、内部平顶。
            float Height (float sd, float t)
            {
                if (sd >= 0.0) return 0.0;
                if (sd < -t)   return t;
                float x = t + sd;             // 0..t
                return sqrt(max(t * t - x * x, 0.0));
            }

            // 把 F 空间偏移转回 UV
            float2 FtoUV (float2 offF, float aspect) { return float2(offF.x / aspect, offF.y); }

            // 背景采样：先按 _TexZoom/_TexPan 把内容在碎片内平移/缩放，再叠加折射偏移。
            float2 ContentUV (float2 uv)
            {
                return (uv - 0.5) / max(_TexZoom, 1e-4) + 0.5 + _TexPan.xy;
            }

            // 小核磨砂采样（半径随边缘增强）
            float3 SampleBlur (float2 uv, float radius)
            {
                float2 r = radius * _MainTex_TexelSize.xy;
                float3 c = tex2D(_MainTex, uv).rgb;
                float total = 1.0;
                for (int k = 0; k < 6; k++)
                {
                    float a = (float)k / 6.0 * 2.0 * PI;
                    float2 o = float2(cos(a), sin(a)) * r;
                    c += tex2D(_MainTex, uv + o).rgb;
                    c += tex2D(_MainTex, uv + o * 0.5).rgb;
                    total += 2.0;
                }
                return c / total;
            }

            fixed4 frag (v2f input) : SV_Target
            {
                float2 uv = input.uv;
                float aspect = _MainTex_TexelSize.z / max(_MainTex_TexelSize.w, 1.0);

                float2 F = float2(uv.x * aspect, uv.y);
                float t = _Thickness;
                float sd = ShapeSD(F, aspect);

                float shape  = smoothstep(_Edge, -_Edge, sd);
                float shadow = smoothstep(t * 2.5, 0.0, sd) * (1.0 - shape);

                // 完全在碎片之外：只输出纯黑阴影（靠 alpha 压暗背景），不采样 MainTex，
                // 因此即便 DropShadow 调大也不会透出超出边界的纹理。
                if (shape <= 0.001)
                {
                    float aShadow = shadow * _ShadowStrength;
                    return fixed4(0.0, 0.0, 0.0, aShadow * _Tint.a);
                }

                // 内容采样基准 UV（独立于形状的平移/缩放）
                float2 baseUV = ContentUV(uv);

                // —— 由 SDF 高度场求表面法线 ——
                float eps = max(_Size, 0.01) * 0.004;
                float sdx = ShapeSD(F + float2(eps, 0), aspect) - ShapeSD(F - float2(eps, 0), aspect);
                float sdy = ShapeSD(F + float2(0, eps), aspect) - ShapeSD(F - float2(0, eps), aspect);
                float2 grad = float2(sdx, sdy);
                grad = (length(grad) > 1e-6) ? normalize(grad) : float2(0.0, 0.0);

                float n_cos = saturate((t + sd) / t);
                float n_sin = sqrt(saturate(1.0 - n_cos * n_cos));
                float3 normal = normalize(float3(grad * n_cos, n_sin));

                float h = Height(sd, t);
                float3 incident = float3(0.0, 0.0, -1.0);

                // —— 物理折射 + 色散 ——
                float3 rR = refract(incident, normal, 1.0 / (_IOR - _Dispersion));
                float3 rG = refract(incident, normal, 1.0 / _IOR);
                float3 rB = refract(incident, normal, 1.0 / (_IOR + _Dispersion));

                float lenR = (h + _BaseHeight) / max(-rR.z, 1e-3);
                float lenG = (h + _BaseHeight) / max(-rG.z, 1e-3);
                float lenB = (h + _BaseHeight) / max(-rB.z, 1e-3);

                float2 uvR = baseUV + FtoUV(rR.xy * lenR, aspect);
                float2 uvG = baseUV + FtoUV(rG.xy * lenG, aspect);
                float2 uvB = baseUV + FtoUV(rB.xy * lenB, aspect);

                float edgeAmt = 1.0 - n_sin;
                // 边缘磨砂(随斜面增强) + 整体磨砂(开关控制，全碎片均匀)
                float blurR = _BlurSize * edgeAmt + _Frost * step(0.5, _FrostOn);

                float3 refr;
                refr.r = SampleBlur(uvR, blurR).r;
                refr.g = SampleBlur(uvG, blurR).g;
                refr.b = SampleBlur(uvB, blurR).b;
                refr *= _Tint.rgb;

                // 背景图自身的 alpha：透明区域在碎片内也应透明（而非显示垃圾 RGB）
                float texA = tex2D(_MainTex, uvG).a;

                // —— 菲涅尔 + 方向高光 ——
                float fres = pow(saturate(edgeAmt), 2.0) * _Fresnel;
                float2 lightDir = float2(cos(_LightAngle), sin(_LightAngle));
                float spec = saturate(dot(grad, lightDir));
                spec = pow(spec, 6.0) * edgeAmt * _SpecStrength;

                float3 glass = refr + fres * 0.25 + spec;

                // —— 裂纹：暗芯(缝隙) + 捕光亮点，受光方向调制，像真实玻璃断裂 ——
                if (_CrackStrength > 0.0)
                {
                    float2 pForCrack = mul(Rot(_Rotation), (F - float2(_Center.x*aspect, _Center.y))) / max(_Size, 1e-4);
                    float crk = CrackField(pForCrack);

                    // 暗芯：裂缝处玻璃错位、透光减少 -> 压暗
                    glass *= 1.0 - 0.5 * crk * saturate(_CrackStrength);
                    // 亮边/反光：裂纹尖锐处捕捉光，强度随光方向略变
                    float glint = pow(crk, 2.0) * (0.5 + 0.5 * saturate(spec + fres));
                    glass += glint * _CrackStrength;
                }

                // —— 扫光：周期性从指定角度扫过一道 HDR 发光线条 ——
                if (_ScanOn > 0.5)
                {
                    // 相对碎片中心的等高坐标，沿扫光角度方向投影
                    float2 q = F - float2(_Center.x * aspect, _Center.y);
                    float2 dir = float2(cos(_ScanAngle), sin(_ScanAngle));
                    float proj = dot(q, dir);            // 沿扫光轴的坐标

                    // 周期：扫过的时间 + 停顿间隔。frac 出 0~1 相位，前半段扫过、后段停。
                    float period = 1.0 / max(_ScanSpeed, 1e-3) + _ScanInterval;
                    float phase = frac(_Time.y / max(period, 1e-3));
                    float sweepT = saturate(phase * period * _ScanSpeed);   // 0..1 扫过进度
                    float ext = _Size + _ScanWidth;
                    float sweepPos = lerp(-ext, ext, sweepT);

                    // 细线 + 柔光晕
                    float dist = abs(proj - sweepPos);
                    float scanLine = smoothstep(_ScanWidth, 0.0, dist);
                    float glow = smoothstep(_ScanWidth * 4.0, 0.0, dist) * 0.4;
                    // 扫描进行中(未处于停顿)才显示
                    float active = step(sweepT, 0.999) * step(0.001, sweepT);
                    // 越靠近高光(spec 越强)发光越亮
                    float specBoost = 1.0 + _ScanSpecBoost * saturate(spec);
                    glass += _ScanColor.rgb * _ScanIntensity * specBoost * (scanLine + glow) * active;
                }

                // —— 统一合成（预乘）：玻璃只在 shape 内可见，阴影只压暗、不重绘纹理 ——
                // 覆盖度 = 形状遮罩 × 背景图 alpha，使图片透明处在碎片内同样透明
                float cover = shape * texA;
                float aShadow = shadow * _ShadowStrength;
                float a = max(cover, aShadow);
                float3 c = glass * cover;              // 玻璃按覆盖度预乘；透明/阴影区 -> 纯黑
                c = (a > 1e-4) ? c / a : c;            // 还原为直通 alpha
                return fixed4(c, a * _Tint.a);
            }
            ENDCG
        }
    }
    Fallback Off
}

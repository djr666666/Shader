using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 给 GlassShard 材质实时切换背景图用的小工具。
///
/// 背景：在 UI(Graphic) 上，纹理由 CanvasRenderer 绑定到 _MainTex，单纯改材质的
/// _MainTex 不会触发画布重建，所以要“重新开关一次 Image”才刷新。这里在改图时
/// 主动 SetMaterialDirty / 设置 RawImage.texture，做到实时更新。
///
/// 用法：挂到贴了 GlassShard 材质的物体上（RawImage / Image / 普通 Quad 都支持），
/// 把要显示的图拖到 texture 字段即可，编辑器与运行时都会立即生效。
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
public class GlassShardSource : MonoBehaviour
{
    [Tooltip("要显示在玻璃碎片里的背景图（可带透明通道）")]
    public Texture texture;

    private void OnEnable()  { Apply(); }
    private void OnValidate(){ Apply(); }

    /// <summary>把当前 texture 应用到渲染对象并强制刷新。</summary>
    public void Apply()
    {
        if (texture == null) return;

        // 1) RawImage：UI 推荐方式，直接设 texture（会绑定到 _MainTex），支持任意图
        var raw = GetComponent<RawImage>();
        if (raw != null)
        {
            raw.texture = texture;
            raw.SetMaterialDirty();
            raw.SetVerticesDirty();
            return;
        }

        // 2) 其它 UI Graphic（如 Image）：写进材质并标脏，触发画布重建
        var graphic = GetComponent<Graphic>();
        if (graphic != null && graphic.material != null)
        {
            graphic.material.SetTexture("_MainTex", texture);
            graphic.SetMaterialDirty();
            return;
        }

        // 3) 普通 Renderer（Quad/Sprite 等）：直接写材质，无需画布刷新
        var rend = GetComponent<Renderer>();
        if (rend != null)
        {
            var m = Application.isPlaying ? rend.material : rend.sharedMaterial;
            if (m != null) m.SetTexture("_MainTex", texture);
        }
    }

    /// <summary>运行时切换背景图。</summary>
    public void SetTexture(Texture tex)
    {
        texture = tex;
        Apply();
    }
}

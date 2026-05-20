using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

[RequireComponent(typeof(Image))]
public class ImageShatterEffect : MonoBehaviour
{
    // -------------------------------------------------------
    // 碎裂模式
    // -------------------------------------------------------
    public enum ShatterMode
    {
        Grid,   // 方格碎裂
        Glass   // 玻璃碎裂（Voronoi）
    }

    [Header("碎裂模式")]
    [SerializeField] private ShatterMode shatterMode = ShatterMode.Grid;

    [Header("方格碎裂参数")]
    [SerializeField] private int gridX = 10;
    [SerializeField] private int gridY = 10;

    [Header("玻璃碎裂参数")]
    [SerializeField] private int glassFragmentCount = 25;
    [SerializeField] private int voronoiSeed = 42;
    [Tooltip("玻璃裂纹起始点（0,0=左下 1,1=右上，留空则随机）")]
    [SerializeField] private Vector2 glassImpactPoint = new Vector2(0.5f, 0.5f);
    [SerializeField] private bool randomImpactPoint = false;

    [Header("通用碎裂参数")]
    [SerializeField] private float shatterForce = 200f;
    [SerializeField] private float gravityScale = 1f;
    [SerializeField] private float fragmentLifetime = 3f;
    [SerializeField] private float reassembleDuration = 0.2f;
    [SerializeField] private float fragmentScale = 1f;

    [Header("碎片显示模式")]
    [SerializeField] private bool useSolidColor = false;
    [SerializeField] private Color solidColorOverride = Color.white;
    [SerializeField] private bool useAverageColor = false;

    [Header("抖动参数")]
    [SerializeField] private bool enableShake = true;
    [SerializeField] private float shakeDuration = 0.1f;
    [SerializeField] private float shakeIntensity = 10f;
    [SerializeField] private int shakeVibrato = 30;
    [SerializeField] private float shakeRandomness = 20f;
    [SerializeField] private bool fadeOutShake = true;

    [Header("碎片预显参数")]
    [SerializeField] private bool enableFragmentPreview = true;
    [SerializeField] private float previewOffset = 3f;
    [SerializeField] private float previewDuration = 0.15f;

    [Header("懒加载设置")]
    [SerializeField] private bool prewarmOnStart = false;

    // -------------------------------------------------------
    // 私有状态
    // -------------------------------------------------------
    private Image sourceImage;
    private RectTransform rectTransform;

    private List<IFragmentData> fragments = new List<IFragmentData>();
    private List<IFragmentData> activeFragments = new List<IFragmentData>();

    private bool isShattered = false;
    private bool isBroken = false;
    private bool isAnimating = false;
    private bool isFragmentsReady = false;

    // 协程句柄：用于精确停止指定协程，避免 StopAllCoroutines 误伤
    private Coroutine shatterRoutine;
    private Coroutine brokenRoutine;
    private Coroutine reassembleRoutine;
    private Coroutine fadeOutRoutine;
    private Coroutine prewarmRoutine;

    private Color originalImageColor;
    private Texture2D sourceTexture;
    private bool ownsSourceTexture = false; // 是否拥有 sourceTexture 的所有权（决定是否在销毁时释放）

    // -------------------------------------------------------
    // 切片配置快照：用于检测运行时参数/Sprite 是否变化，决定是否需要重切
    // -------------------------------------------------------
    private Sprite cachedSprite;
    private ShatterMode cachedMode;
    private int cachedGridX;
    private int cachedGridY;
    private int cachedGlassCount;
    private int cachedVoronoiSeed;
    private Vector2 cachedRectSize;

    // -------------------------------------------------------
    // 碎片数据接口（Grid 和 Glass 通用）
    // -------------------------------------------------------
    private interface IFragmentData
    {
        GameObject GameObject { get; }
        RectTransform RectTransform { get; }
        Vector2 OriginalPosition { get; set; }
        Vector2 Velocity { get; set; }
        float AngularVelocity { get; set; }
        float CurrentAngle { get; set; }
        Color FadeStartColor { get; set; }
        void SetColor(Color color);
        void SetAlpha(float alpha);
    }

    // Grid 碎片：使用 Image 组件
    private class GridFragmentData : IFragmentData
    {
        public GameObject gameObject;
        public RectTransform rectTransform;
        public Image image;
        public Sprite originalSprite;
        public Sprite solidColorSprite;
        public Color averageColor;

        public GameObject GameObject => gameObject;
        public RectTransform RectTransform => rectTransform;
        public Vector2 OriginalPosition { get; set; }
        public Vector2 Velocity { get; set; }
        public float AngularVelocity { get; set; }
        public float CurrentAngle { get; set; }
        public Color FadeStartColor { get; set; }

        public void SetColor(Color color) => image.color = color;
        public void SetAlpha(float alpha)
        {
            Color c = image.color;
            c.a = alpha;
            image.color = c;
        }
    }

    // Glass 碎片：使用自定义 Mesh + CanvasRenderer
    private class GlassFragmentData : IFragmentData
    {
        public GameObject gameObject;
        public RectTransform rectTransform;
        public CanvasRenderer canvasRenderer;
        public List<Vector2> polygon; // 多边形顶点（局部坐标）
        public Color averageColor;
        public Texture2D fragmentTexture;

        public GameObject GameObject => gameObject;
        public RectTransform RectTransform => rectTransform;
        public Vector2 OriginalPosition { get; set; }
        public Vector2 Velocity { get; set; }
        public float AngularVelocity { get; set; }
        public float CurrentAngle { get; set; }
        public Color FadeStartColor { get; set; }

        private Color currentColor = Color.white;

        public void SetColor(Color color)
        {
            currentColor = color;
            canvasRenderer.SetColor(color);
        }

        public void SetAlpha(float alpha)
        {
            currentColor.a = alpha;
            canvasRenderer.SetAlpha(alpha);
        }
    }

    // -------------------------------------------------------
    // 生命周期
    // -------------------------------------------------------
    void Awake()
    {
        sourceImage = GetComponent<Image>();
        rectTransform = GetComponent<RectTransform>();

        if (sourceImage == null)
        {
            Debug.LogError("ImageShatterEffect: 需要 Image 组件");
            enabled = false;
            return;
        }

        // sprite 可能在运行时再赋值，这里不禁用组件，仅在没有时给出警告
        if (sourceImage.sprite == null)
            Debug.LogWarning("ImageShatterEffect: 初始 sprite 为空，请在触发碎裂前为 Image 设置 sprite");

        originalImageColor = sourceImage.color;
    }

    void Start()
    {
        if (prewarmOnStart)
            prewarmRoutine = StartCoroutine(PrewarmNextFrame());
    }

    void Update()
    {
        // 只有已炸开（isShattered）且不处于动画中时才运行碎片物理
        if (isShattered && !isBroken && !isAnimating)
            UpdateFragmentPhysics();
    }

    void OnDestroy()
    {
        ClearFragments();
        if (sourceTexture != null)
        {
            if (ownsSourceTexture) Destroy(sourceTexture);
            sourceTexture = null;
            ownsSourceTexture = false;
        }
    }

    // -------------------------------------------------------
    // 预热
    // -------------------------------------------------------
    private IEnumerator PrewarmNextFrame()
    {
        yield return null;
        EnsureFragmentsReady();
        Debug.Log("ImageShatterEffect: 预热完成");
        prewarmRoutine = null;
    }

    // -------------------------------------------------------
    // 调试入口
    // -------------------------------------------------------
    [ContextMenu("触发碎裂")]
    public void DebugShatter() => SetShattered(true);

    [ContextMenu("触发重组")]
    public void DebugReassemble() => SetShattered(false);

    [ContextMenu("触发 Broken（裂痕不炸开）")]
    public void DebugBroke() => SetBroke(true);

    [ContextMenu("退出 Broken 状态")]
    public void DebugExitBroke() => SetBroke(false);

    [ContextMenu("切换颜色模式")]
    public void DebugToggleColor() => ToggleColorMode();

    [ContextMenu("手动预热碎片")]
    public void DebugPrewarm()
    {
        EnsureFragmentsReady();
        Debug.Log($"预热完成：{fragments.Count} 个碎片");
    }

    [ContextMenu("失效碎片缓存（下次触发重切）")]
    public void DebugInvalidate()
    {
        InvalidateFragments();
        Debug.Log("ImageShatterEffect: 碎片缓存已失效，下次触发碎裂时将重新切割");
    }

    // -------------------------------------------------------
    // 公共接口
    // -------------------------------------------------------
    /// <summary>
    /// 设置碎裂状态（出现裂痕但不炸开）。
    /// SetBroke(true)  → 图片呈现碎裂外观，碎片静止在原位。
    /// SetBroke(false) → 如果当前处于 Broken 状态，直接还原（不经过重组动画）；
    ///                   若已进入炸开阶段则无效。
    /// </summary>
    public void SetBroke(bool broken)
    {
        if (isAnimating) return;

        if (broken && !isBroken && !isShattered)
        {
            if (sourceImage.sprite == null)
            {
                Debug.LogWarning("ImageShatterEffect: 无法进入 Broken 状态，sprite 为空");
                return;
            }
            // 进入"碎裂但不炸开"状态
            isBroken = true;
            brokenRoutine = StartCoroutine(EnterBrokenState());
        }
        else if (!broken && isBroken && !isShattered)
        {
            // 退出 Broken 状态，立即恢复原图（不做炸开）
            isBroken = false;
            ExitBrokenState();
        }
    }

    public void SetShattered(bool shattered)
    {
        if (isAnimating) return;

        if (shattered && !isShattered)
        {
            if (sourceImage.sprite == null)
            {
                Debug.LogWarning("ImageShatterEffect: 无法触发碎裂，sprite 为空");
                return;
            }
            if (isBroken)
            {
                // 已处于 Broken 状态：直接从当前碎片位置执行炸开
                isBroken = false;
                isShattered = true;
                ExecuteShatter();
            }
            else
            {
                // 未经过 Broken：走完整的预显→抖动→炸开流程
                shatterRoutine = StartCoroutine(ShatterWithFragmentPreview());
            }
        }
        else if (!shattered && isShattered)
        {
            Reassemble();
        }
    }

    public void ToggleColorMode()
    {
        useSolidColor = !useSolidColor;
        if (isFragmentsReady)
            ApplyColorMode();
    }

    public void SetColorMode(bool solidColor)
    {
        useSolidColor = solidColor;
        if (isFragmentsReady)
            ApplyColorMode();
    }

    public void ForceUpdateLayout()
    {
        if (!isShattered && !isAnimating && isFragmentsReady)
            UpdateFragmentTargetPositions();
    }

    // -------------------------------------------------------
    // 协程管理
    // -------------------------------------------------------
    /// <summary>
    /// 停止所有动画相关协程并强制重置 isAnimating 状态。
    /// 这是替代 StopAllCoroutines 的更精细的方式，避免误杀其他协程
    /// 并确保状态机不会卡在 isAnimating=true 上。
    /// </summary>
    private void StopAnimationRoutines()
    {
        if (shatterRoutine != null) { StopCoroutine(shatterRoutine); shatterRoutine = null; }
        if (brokenRoutine != null) { StopCoroutine(brokenRoutine); brokenRoutine = null; }
        if (reassembleRoutine != null) { StopCoroutine(reassembleRoutine); reassembleRoutine = null; }
        if (fadeOutRoutine != null) { StopCoroutine(fadeOutRoutine); fadeOutRoutine = null; }
        // 关键：确保动画状态被强制清理，否则下次调用会被早返拦截
        isAnimating = false;
    }

    // -------------------------------------------------------
    // 懒加载核心 + 热重载
    // -------------------------------------------------------
    /// <summary>
    /// 确保碎片处于"最新可用"状态。
    /// 行为：
    /// 1. 如果从未切过 → 切片。
    /// 2. 如果切片配置（Sprite / 模式 / 切割参数 / RectTransform 尺寸）相比上次发生变化 → 重切。
    /// 3. 否则复用现有碎片（懒加载，无开销）。
    /// 这是热重载的核心入口：所有需要碎片的地方都通过它来获取最新切片结果。
    /// </summary>
    private void EnsureFragmentsReady()
    {
        if (isFragmentsReady && !HasConfigChanged())
            return;

        CreateFragments();
        CaptureConfigSnapshot();
        isFragmentsReady = true;
    }

    /// <summary>
    /// 比对当前配置与上次切片时的快照，判断是否需要重切。
    /// </summary>
    private bool HasConfigChanged()
    {
        if (cachedSprite != sourceImage.sprite) return true;
        if (cachedMode != shatterMode) return true;
        if (cachedRectSize != rectTransform.rect.size) return true;

        if (shatterMode == ShatterMode.Grid)
        {
            if (cachedGridX != gridX) return true;
            if (cachedGridY != gridY) return true;
        }
        else // Glass
        {
            if (cachedGlassCount != glassFragmentCount) return true;
            if (cachedVoronoiSeed != voronoiSeed) return true;
        }

        return false;
    }

    /// <summary>
    /// 记录当前配置为新快照，后续以此为基准判断是否变化。
    /// </summary>
    private void CaptureConfigSnapshot()
    {
        cachedSprite = sourceImage.sprite;
        cachedMode = shatterMode;
        cachedGridX = gridX;
        cachedGridY = gridY;
        cachedGlassCount = glassFragmentCount;
        cachedVoronoiSeed = voronoiSeed;
        cachedRectSize = rectTransform.rect.size;
    }

    /// <summary>
    /// 主动让当前碎片失效。下次触发碎裂/Broken/重组时将自动重切。
    /// 外部代码批量修改参数后可以调用此方法，比改完一项就比对一次更高效；
    /// 或者用于强制重切（即便快照判断不到变化时，例如 Sprite 内容被外部修改但引用未变）。
    /// </summary>
    public void InvalidateFragments()
    {
        isFragmentsReady = false;
    }

    // -------------------------------------------------------
    // 颜色模式
    // -------------------------------------------------------
    private void ApplyColorToFragment(IFragmentData frag, float alpha = 1f)
    {
        Color c;
        if (useSolidColor)
        {
            if (shatterMode == ShatterMode.Grid)
            {
                var grid = frag as GridFragmentData;
                grid.image.sprite = grid.solidColorSprite;
                c = useAverageColor ? grid.averageColor : solidColorOverride;
            }
            else
            {
                var glass = frag as GlassFragmentData;
                c = useAverageColor ? glass.averageColor : solidColorOverride;
            }
        }
        else
        {
            if (shatterMode == ShatterMode.Grid)
            {
                var grid = frag as GridFragmentData;
                grid.image.sprite = grid.originalSprite;
            }
            c = originalImageColor;
        }

        c.a = alpha;
        frag.SetColor(c);
    }

    private void ApplyColorMode()
    {
        foreach (var frag in fragments)
            ApplyColorToFragment(frag);
    }

    // -------------------------------------------------------
    // 位置计算
    // -------------------------------------------------------
    private void UpdateFragmentTargetPositions()
    {
        if (shatterMode == ShatterMode.Grid)
        {
            Vector2 size = rectTransform.rect.size;
            Vector2 fragSize = new Vector2(size.x / gridX, size.y / gridY);

            int idx = 0;
            for (int y = 0; y < gridY; y++)
            {
                for (int x = 0; x < gridX; x++)
                {
                    if (idx >= fragments.Count) break;
                    fragments[idx].OriginalPosition = GetGridFragmentPosition(x, y);
                    fragments[idx].RectTransform.sizeDelta = fragSize;
                    idx++;
                }
            }
        }
        // Glass 碎片的位置在创建时已确定，不需要更新
    }

    private Vector2 GetGridFragmentPosition(int x, int y)
    {
        Vector2 size = rectTransform.rect.size;
        Vector2 pivotOffset = new Vector2(
            (0.5f - rectTransform.pivot.x) * size.x,
            (0.5f - rectTransform.pivot.y) * size.y
        );
        Vector2 fs = new Vector2(size.x / gridX, size.y / gridY);
        return new Vector2(
            (x + 0.5f) * fs.x - size.x * 0.5f + pivotOffset.x,
            (y + 0.5f) * fs.y - size.y * 0.5f + pivotOffset.y
        );
    }

    // -------------------------------------------------------
    // Broken 状态（裂痕显示，碎片静止在原位）
    // -------------------------------------------------------

    /// <summary>
    /// 进入 Broken 状态：复用预显流程让碎片出现在原位，但不继续炸开。
    /// </summary>
    private IEnumerator EnterBrokenState()
    {
        isAnimating = true;

        EnsureFragmentsReady();
        UpdateFragmentTargetPositions();

        if (enableFragmentPreview)
        {
            yield return StartCoroutine(TransitionToFragments());
        }
        else
        {
            sourceImage.enabled = false;
            ShowFragmentsAtOriginalPositions();
        }

        // 抖动（可选，与正常碎裂保持一致的视觉反馈）
        if (enableShake)
            yield return StartCoroutine(ShakeFragments());

        // 碎片静止在原位，不执行 ExecuteShatter
        isAnimating = false;
        brokenRoutine = null;
    }

    /// <summary>
    /// 退出 Broken 状态：隐藏碎片，恢复原图显示。
    /// </summary>
    private void ExitBrokenState()
    {
        StopAnimationRoutines(); // 替代 StopAllCoroutines，并确保 isAnimating 归零
        CancelInvoke();

        foreach (var f in fragments)
            f.GameObject.SetActive(false);

        activeFragments.Clear();
        sourceImage.enabled = true;
        sourceImage.color = originalImageColor;
    }

    // -------------------------------------------------------
    // 碎裂流程（新：先预显碎片，再抖动爆炸）
    // -------------------------------------------------------
    private IEnumerator ShatterWithFragmentPreview()
    {
        isAnimating = true;

        EnsureFragmentsReady();
        UpdateFragmentTargetPositions();

        // 第一步：先过渡到碎片模式（预显碎片）
        if (enableFragmentPreview)
        {
            yield return StartCoroutine(TransitionToFragments());
        }
        else
        {
            // 如果不启用预显，直接隐藏原图并显示碎片
            sourceImage.enabled = false;
            ShowFragmentsAtOriginalPositions();
        }

        // 第二步：抖动（抖动的是碎片容器或偏移）
        if (enableShake)
            yield return StartCoroutine(ShakeFragments());

        // 第三步：爆炸
        isShattered = true;
        ExecuteShatter();

        isAnimating = false;
        shatterRoutine = null;
    }

    /// <summary>
    /// 过渡到碎片模式：原图逐渐消失，碎片逐渐出现并轻微偏移
    /// </summary>
    private IEnumerator TransitionToFragments()
    {
        // 显示所有碎片在原始位置
        ShowFragmentsAtOriginalPositions();

        // 为每个碎片生成随机的预览偏移方向
        Dictionary<IFragmentData, Vector2> previewOffsets = new Dictionary<IFragmentData, Vector2>();
        foreach (var frag in fragments)
        {
            Vector2 randomDir = Random.insideUnitCircle.normalized;
            previewOffsets[frag] = randomDir * previewOffset;
        }

        float elapsed = 0f;
        Color imageStartColor = sourceImage.color;

        while (elapsed < previewDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / previewDuration);
            // 使用缓动函数让动画更自然
            float easedT = EaseOutCubic(t);

            // 原图逐渐透明
            Color imgColor = imageStartColor;
            imgColor.a = Mathf.Lerp(1f, 0f, easedT);
            sourceImage.color = imgColor;

            // 碎片从略微偏移的位置移动到原始位置，同时alpha从0到1
            foreach (var frag in fragments)
            {
                if (previewOffsets.ContainsKey(frag))
                {
                    Vector2 offset = Vector2.Lerp(previewOffsets[frag], Vector2.zero, easedT);
                    frag.RectTransform.localPosition = frag.OriginalPosition + offset;
                }
                ApplyColorToFragment(frag, easedT);
            }

            yield return null;
        }

        // 确保最终状态
        sourceImage.enabled = false;
        sourceImage.color = imageStartColor; // 恢复原始颜色以便重组

        foreach (var frag in fragments)
        {
            frag.RectTransform.localPosition = frag.OriginalPosition;
            ApplyColorToFragment(frag, 1f);
        }
    }

    /// <summary>
    /// 显示所有碎片在原始位置（不爆炸）
    /// </summary>
    private void ShowFragmentsAtOriginalPositions()
    {
        foreach (var frag in fragments)
        {
            frag.GameObject.SetActive(true);
            frag.RectTransform.localPosition = frag.OriginalPosition;
            frag.RectTransform.localRotation = Quaternion.identity;
            frag.CurrentAngle = 0f;
            ApplyColorToFragment(frag, 1f);
        }
    }

    /// <summary>
    /// 抖动碎片（碎片作为一个整体偏移）
    /// </summary>
    private IEnumerator ShakeFragments()
    {
        // 记录每个碎片的当前本地位置（此时它们都在原位）
        Dictionary<IFragmentData, Vector2> basePositions = new Dictionary<IFragmentData, Vector2>();
        foreach (var frag in fragments)
        {
            basePositions[frag] = frag.RectTransform.localPosition;
        }

        float elapsed = 0f;

        while (elapsed < shakeDuration)
        {
            elapsed += Time.deltaTime;
            float intensity = fadeOutShake
                ? Mathf.Lerp(shakeIntensity, 0f, elapsed / shakeDuration)
                : shakeIntensity;

            float px = Mathf.PerlinNoise(Time.time * shakeVibrato, 0f) * 2f - 1f;
            float py = Mathf.PerlinNoise(0f, Time.time * shakeVibrato) * 2f - 1f;
            float rx = Random.Range(-1f, 1f) * shakeRandomness * 0.01f;
            float ry = Random.Range(-1f, 1f) * shakeRandomness * 0.01f;

            Vector2 shakeOffset = new Vector2(
                (px + rx) * intensity,
                (py + ry) * intensity
            );

            // 对所有碎片应用相同的抖动偏移
            foreach (var frag in fragments)
            {
                if (frag.GameObject.activeInHierarchy)
                {
                    frag.RectTransform.localPosition = basePositions[frag] + shakeOffset;
                }
            }

            yield return null;
        }

        // 抖动结束，恢复到原始位置
        foreach (var frag in fragments)
        {
            if (frag.GameObject.activeInHierarchy)
            {
                frag.RectTransform.localPosition = frag.OriginalPosition;
            }
        }
    }

    private void ExecuteShatter()
    {
        sourceImage.enabled = false;
        isShattered = true;
        isBroken = false;   // 炸开时 Broken 状态归位
        CancelInvoke();
        activeFragments.Clear();

        Vector2 impactPoint = randomImpactPoint
            ? new Vector2(Random.value, Random.value)
            : glassImpactPoint;

        foreach (var frag in fragments)
        {
            frag.GameObject.SetActive(true);
            frag.RectTransform.localPosition = frag.OriginalPosition;
            frag.RectTransform.localRotation = Quaternion.identity;
            frag.CurrentAngle = 0f;

            ApplyColorToFragment(frag);

            // 玻璃模式：速度从冲击点向外辐射
            if (shatterMode == ShatterMode.Glass)
            {
                Vector2 center = frag.OriginalPosition;
                Vector2 impactLocal = new Vector2(
                    (impactPoint.x - 0.5f) * rectTransform.rect.width,
                    (impactPoint.y - 0.5f) * rectTransform.rect.height
                );
                Vector2 dir = (center - impactLocal).normalized;
                float dist = Vector2.Distance(center, impactLocal);
                float forceMult = Mathf.Clamp01(1f - dist / (rectTransform.rect.width * 0.5f));

                frag.Velocity = dir * shatterForce * (0.5f + forceMult * 0.5f) + new Vector2(
                    Random.Range(-0.3f, 0.3f) * shatterForce,
                    Random.Range(-0.5f, 0.2f) * shatterForce
                );
            }
            else
            {
                frag.Velocity = new Vector2(
                    Random.Range(-1f, 1f) * shatterForce,
                    Random.Range(-2f, 0.5f) * shatterForce
                );
            }

            frag.AngularVelocity = Random.Range(-360f, 360f);
            activeFragments.Add(frag);
        }

        Invoke(nameof(StartFadeOut), fragmentLifetime);
    }

    // -------------------------------------------------------
    // 物理更新
    // -------------------------------------------------------
    private void UpdateFragmentPhysics()
    {
        float dt = Time.deltaTime;

        for (int i = activeFragments.Count - 1; i >= 0; i--)
        {
            var f = activeFragments[i];

            f.Velocity += Vector2.down * (9.8f * 50f * gravityScale * dt);
            f.RectTransform.localPosition += (Vector3)(f.Velocity * dt);
            f.CurrentAngle += f.AngularVelocity * dt;
            f.RectTransform.localRotation = Quaternion.Euler(0f, 0f, f.CurrentAngle);

            Vector3 pos = f.RectTransform.localPosition;
            if (Mathf.Abs(pos.x) > 2000f || Mathf.Abs(pos.y) > 2000f)
            {
                f.GameObject.SetActive(false);
                activeFragments.RemoveAt(i);
            }
        }
    }

    // -------------------------------------------------------
    // 淡出
    // -------------------------------------------------------
    private void StartFadeOut() => fadeOutRoutine = StartCoroutine(FadeOutFragments());

    private IEnumerator FadeOutFragments()
    {
        float duration = 1f, elapsed = 0f;

        foreach (var f in fragments)
            if (f.GameObject.activeInHierarchy)
                f.FadeStartColor = (f is GridFragmentData grid) ? grid.image.color : Color.white;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            foreach (var f in fragments)
            {
                if (!f.GameObject.activeInHierarchy) continue;
                f.SetAlpha(Mathf.Lerp(1f, 0f, t));
            }

            yield return null;
        }

        foreach (var f in fragments)
            f.GameObject.SetActive(false);

        activeFragments.Clear();
        fadeOutRoutine = null;
    }

    // -------------------------------------------------------
    // 重组流程
    // -------------------------------------------------------
    private void Reassemble()
    {
        isShattered = false;
        isBroken = false;
        CancelInvoke();
        StopAnimationRoutines(); // 替代 StopAllCoroutines

        // 重组时不允许重切：如果配置变了，会销毁正在飞散的旧碎片造成视觉跳变。
        // 这里只在没有任何碎片时才执行切片；否则就用现有的旧碎片完成重组，
        // 重组结束后再 Invalidate，下次触发碎裂时才重切。
        bool configChangedDuringFlight = isFragmentsReady && HasConfigChanged();
        if (!isFragmentsReady)
            EnsureFragmentsReady();

        reassembleRoutine = StartCoroutine(ReassembleAnimation(configChangedDuringFlight));
    }

    private IEnumerator ReassembleAnimation(bool invalidateAfterDone)
    {
        isAnimating = true;
        UpdateFragmentTargetPositions();

        sourceImage.enabled = true;
        sourceImage.color = new Color(originalImageColor.r, originalImageColor.g, originalImageColor.b, 0f);

        foreach (var f in fragments)
        {
            f.GameObject.SetActive(true);
            ApplyColorToFragment(f, 1f);
        }

        var startPos = new Dictionary<IFragmentData, Vector2>(fragments.Count);
        var startRot = new Dictionary<IFragmentData, Quaternion>(fragments.Count);
        foreach (var f in fragments)
        {
            startPos[f] = f.RectTransform.localPosition;
            startRot[f] = f.RectTransform.localRotation;
        }

        float elapsed = 0f;

        while (elapsed < reassembleDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / reassembleDuration);

            if (t <= 0.85f)
            {
                float moveT = Mathf.SmoothStep(0f, 1f, t / 0.85f);
                foreach (var f in fragments)
                {
                    f.RectTransform.localPosition = Vector2.Lerp(startPos[f], f.OriginalPosition, moveT);
                    f.RectTransform.localRotation = Quaternion.Slerp(startRot[f], Quaternion.identity, moveT);
                }
                sourceImage.color = new Color(originalImageColor.r, originalImageColor.g, originalImageColor.b, 0f);
            }
            else
            {
                float fadeT = Mathf.SmoothStep(0f, 1f, (t - 0.85f) / 0.15f);
                foreach (var f in fragments)
                {
                    f.RectTransform.localPosition = f.OriginalPosition;
                    f.RectTransform.localRotation = Quaternion.identity;
                    ApplyColorToFragment(f, Mathf.Lerp(1f, 0f, fadeT));
                }
                Color ic = sourceImage.color;
                ic.a = Mathf.Lerp(0f, originalImageColor.a, fadeT);
                sourceImage.color = ic;
            }

            yield return null;
        }

        sourceImage.color = originalImageColor;

        foreach (var f in fragments)
            f.GameObject.SetActive(false);

        activeFragments.Clear();
        isAnimating = false;
        reassembleRoutine = null;

        // 如果重组期间检测到配置变化，重组结束后再失效，下次触发碎裂会自动重切
        if (invalidateAfterDone)
            InvalidateFragments();
    }

    // -------------------------------------------------------
    // 碎片创建
    // -------------------------------------------------------
    private void CreateFragments()
    {
        ClearFragments();

        if (sourceTexture != null)
        {
            if (ownsSourceTexture) Destroy(sourceTexture);
            sourceTexture = null;
            ownsSourceTexture = false;
        }

        // 运行时安全检查：sprite 或其纹理可能被外部代码设为 null
        if (sourceImage.sprite == null || sourceImage.sprite.texture == null)
        {
            Debug.LogWarning("ImageShatterEffect: sourceImage.sprite 或其 texture 为 null，跳过切片");
            return;
        }

        sourceTexture = GetReadableTexture(sourceImage.sprite.texture, out ownsSourceTexture);
        if (sourceTexture == null)
        {
            Debug.LogError("ImageShatterEffect: 无法读取源纹理");
            return;
        }

        if (shatterMode == ShatterMode.Grid)
            CreateGridFragments();
        else
            CreateGlassFragments();

        Debug.Log($"创建 {fragments.Count} 个 {shatterMode} 碎片");
    }

    // -------------------------------------------------------
    // Grid 碎片创建（原逻辑）
    // -------------------------------------------------------
    private void CreateGridFragments()
    {
        Vector2 imageSize = rectTransform.rect.size;
        int fw = Mathf.Max(1, sourceTexture.width / gridX);
        int fh = Mathf.Max(1, sourceTexture.height / gridY);
        Vector2 fragSize = new Vector2(imageSize.x / gridX, imageSize.y / gridY);

        for (int y = 0; y < gridY; y++)
        {
            for (int x = 0; x < gridX; x++)
            {
                // 最后一行/列吃掉整数除法的余数像素，避免图片边缘内容被丢弃
                int curFw = (x == gridX - 1) ? (sourceTexture.width - x * fw) : fw;
                int curFh = (y == gridY - 1) ? (sourceTexture.height - y * fh) : fh;
                curFw = Mathf.Max(1, curFw);
                curFh = Mathf.Max(1, curFh);

                Color[] pixels = sourceTexture.GetPixels(x * fw, y * fh, curFw, curFh);
                Color avg = CalculateAverageColor(pixels);

                // mipChain=false, linear=false → 保持 sRGB 解释，与源纹理一致
                Texture2D fragTex = new Texture2D(curFw, curFh, TextureFormat.RGBA32, false, false);
                fragTex.SetPixels(pixels);
                fragTex.Apply();
                Sprite origSprite = Sprite.Create(fragTex, new Rect(0, 0, curFw, curFh), new Vector2(0.5f, 0.5f));

                Texture2D solidTex = CreateSolidColorTexture(curFw, curFh, avg);
                Sprite solidSprite = Sprite.Create(solidTex, new Rect(0, 0, curFw, curFh), new Vector2(0.5f, 0.5f));

                GameObject go = new GameObject($"GridFragment_{x}_{y}");
                go.transform.SetParent(transform);
                go.transform.localScale = Vector3.one * fragmentScale;
                go.SetActive(false);

                RectTransform rt = go.AddComponent<RectTransform>();
                rt.sizeDelta = fragSize;

                Vector2 localPos = GetGridFragmentPosition(x, y);
                rt.localPosition = localPos;

                Image img = go.AddComponent<Image>();
                img.raycastTarget = false;

                var data = new GridFragmentData
                {
                    gameObject = go,
                    rectTransform = rt,
                    image = img,
                    originalSprite = origSprite,
                    solidColorSprite = solidSprite,
                    averageColor = avg,
                    OriginalPosition = localPos
                };

                ApplyColorToFragment(data, 1f);
                fragments.Add(data);
            }
        }
    }

    // -------------------------------------------------------
    // Glass 碎片创建（Voronoi）
    // -------------------------------------------------------
    private void CreateGlassFragments()
    {
        Random.InitState(voronoiSeed);

        Vector2 size = rectTransform.rect.size;
        Rect bounds = new Rect(-size.x * 0.5f, -size.y * 0.5f, size.x, size.y);

        // 生成 Voronoi 种子点
        List<Vector2> sites = new List<Vector2>();
        for (int i = 0; i < glassFragmentCount; i++)
        {
            sites.Add(new Vector2(
                Random.Range(bounds.xMin, bounds.xMax),
                Random.Range(bounds.yMin, bounds.yMax)
            ));
        }

        // 为每个种子点生成 Voronoi 多边形
        var voronoiCells = ComputeVoronoiCells(sites, bounds);

        for (int i = 0; i < voronoiCells.Count; i++)
        {
            var cell = voronoiCells[i];
            if (cell.Count < 3) continue; // 跳过退化多边形

            Vector2 center = GetPolygonCenter(cell);
            Color avg = SampleAverageColorFromPolygon(cell, center);

            GameObject go = new GameObject($"GlassFragment_{i}");
            go.transform.SetParent(transform);
            go.transform.localScale = Vector3.one * fragmentScale;
            go.SetActive(false);

            RectTransform rt = go.AddComponent<RectTransform>();
            rt.sizeDelta = Vector2.zero; // Mesh 自己定义大小
            rt.localPosition = center;

            CanvasRenderer cr = go.AddComponent<CanvasRenderer>();

            // 创建碎片纹理和 Mesh
            Texture2D fragTex = CreatePolygonTexture(cell, center);
            Material mat = new Material(Shader.Find("UI/Default"));
            mat.mainTexture = fragTex;

            Mesh mesh = CreatePolygonMesh(cell, center);
            cr.SetMaterial(mat, fragTex);
            cr.SetMesh(mesh);
            cr.SetColor(useSolidColor ? (useAverageColor ? avg : solidColorOverride) : originalImageColor);

            var data = new GlassFragmentData
            {
                gameObject = go,
                rectTransform = rt,
                canvasRenderer = cr,
                polygon = cell,
                averageColor = avg,
                fragmentTexture = fragTex,
                OriginalPosition = center
            };

            fragments.Add(data);
        }
    }

    // -------------------------------------------------------
    // Voronoi 计算（简化版）
    // -------------------------------------------------------
    private List<List<Vector2>> ComputeVoronoiCells(List<Vector2> sites, Rect bounds)
    {
        // 简化实现：用最近点分割，非真实 Voronoi
        // 真实算法需要 Fortune's Algorithm 或 Delaunay 三角化
        // 这里用网格采样近似

        int gridRes = 50;
        float cellWidth = bounds.width / gridRes;
        float cellHeight = bounds.height / gridRes;

        List<List<Vector2>> cells = new List<List<Vector2>>();
        for (int i = 0; i < sites.Count; i++)
            cells.Add(new List<Vector2>());

        // 为每个站点构建边界框
        for (int i = 0; i < sites.Count; i++)
        {
            Vector2 site = sites[i];

            // 找到相邻的边界点（简化：用矩形区域）
            List<Vector2> cellVerts = new List<Vector2>();

            // 使用简化的方法：计算与其他站点的中垂线
            // 这里用更简单的方法：直接生成不规则四边形/多边形

            // 为了性能，使用预定义的辐射点
            int rays = 8;
            for (int r = 0; r < rays; r++)
            {
                float angle = (r / (float)rays) * Mathf.PI * 2f;
                Vector2 dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));

                float maxDist = Mathf.Min(bounds.width, bounds.height) * 0.5f;
                Vector2 rayEnd = site + dir * maxDist;

                // 找到与其他站点的最近交点
                float minDist = maxDist;
                for (int j = 0; j < sites.Count; j++)
                {
                    if (i == j) continue;
                    Vector2 other = sites[j];

                    // 中垂线
                    Vector2 mid = (site + other) * 0.5f;
                    Vector2 perpDir = new Vector2(-(other.y - site.y), other.x - site.x).normalized;

                    // 射线与中垂线交点（简化计算）
                    float t = IntersectRayLine(site, dir, mid, perpDir);
                    if (t > 0 && t < minDist)
                        minDist = t;
                }

                Vector2 vertex = site + dir * minDist;

                // 限制在边界内
                vertex.x = Mathf.Clamp(vertex.x, bounds.xMin, bounds.xMax);
                vertex.y = Mathf.Clamp(vertex.y, bounds.yMin, bounds.yMax);

                cellVerts.Add(vertex);
            }

            // 按角度排序顶点
            cellVerts = cellVerts.OrderBy(v => Mathf.Atan2(v.y - site.y, v.x - site.x)).ToList();

            cells[i] = cellVerts;
        }

        return cells;
    }

    private float IntersectRayLine(Vector2 rayOrigin, Vector2 rayDir, Vector2 linePoint, Vector2 lineDir)
    {
        float cross = rayDir.x * lineDir.y - rayDir.y * lineDir.x;
        if (Mathf.Abs(cross) < 0.0001f) return -1f;

        Vector2 diff = linePoint - rayOrigin;
        float t = (diff.x * lineDir.y - diff.y * lineDir.x) / cross;
        return t;
    }

    private Vector2 GetPolygonCenter(List<Vector2> polygon)
    {
        Vector2 sum = Vector2.zero;
        foreach (var p in polygon) sum += p;
        return sum / polygon.Count;
    }

    // -------------------------------------------------------
    // 多边形纹理采样
    // -------------------------------------------------------
    private Color SampleAverageColorFromPolygon(List<Vector2> polygon, Vector2 center)
    {
        // 简化：只采样中心点颜色
        Vector2 size = rectTransform.rect.size;
        Vector2 uv = new Vector2(
            (center.x + size.x * 0.5f) / size.x,
            (center.y + size.y * 0.5f) / size.y
        );

        int px = Mathf.Clamp((int)(uv.x * sourceTexture.width), 0, sourceTexture.width - 1);
        int py = Mathf.Clamp((int)(uv.y * sourceTexture.height), 0, sourceTexture.height - 1);

        return sourceTexture.GetPixel(px, py);
    }

    private Texture2D CreatePolygonTexture(List<Vector2> polygon, Vector2 center)
    {
        // 为多边形创建纹理：从源纹理采样对应区域
        Rect polyBounds = GetPolygonBounds(polygon);
        int tw = Mathf.Max(1, (int)(polyBounds.width * sourceTexture.width / rectTransform.rect.width));
        int th = Mathf.Max(1, (int)(polyBounds.height * sourceTexture.height / rectTransform.rect.height));

        Texture2D tex = new Texture2D(tw, th, TextureFormat.RGBA32, false, false);
        Vector2 size = rectTransform.rect.size;

        for (int y = 0; y < th; y++)
        {
            for (int x = 0; x < tw; x++)
            {
                Vector2 localPos = new Vector2(
                    polyBounds.xMin + (x / (float)tw) * polyBounds.width,
                    polyBounds.yMin + (y / (float)th) * polyBounds.height
                );

                Vector2 uv = new Vector2(
                    (localPos.x + size.x * 0.5f) / size.x,
                    (localPos.y + size.y * 0.5f) / size.y
                );

                int px = Mathf.Clamp((int)(uv.x * sourceTexture.width), 0, sourceTexture.width - 1);
                int py = Mathf.Clamp((int)(uv.y * sourceTexture.height), 0, sourceTexture.height - 1);

                tex.SetPixel(x, y, sourceTexture.GetPixel(px, py));
            }
        }

        tex.Apply();
        return tex;
    }

    private Rect GetPolygonBounds(List<Vector2> polygon)
    {
        float minX = float.MaxValue, minY = float.MaxValue;
        float maxX = float.MinValue, maxY = float.MinValue;

        foreach (var p in polygon)
        {
            if (p.x < minX) minX = p.x;
            if (p.x > maxX) maxX = p.x;
            if (p.y < minY) minY = p.y;
            if (p.y > maxY) maxY = p.y;
        }

        return new Rect(minX, minY, maxX - minX, maxY - minY);
    }

    private Mesh CreatePolygonMesh(List<Vector2> polygon, Vector2 center)
    {
        Mesh mesh = new Mesh();

        // 顶点：相对于 center 的局部坐标
        Vector3[] vertices = new Vector3[polygon.Count];
        Vector2[] uvs = new Vector2[polygon.Count];
        Rect bounds = GetPolygonBounds(polygon);

        for (int i = 0; i < polygon.Count; i++)
        {
            vertices[i] = new Vector3(polygon[i].x - center.x, polygon[i].y - center.y, 0f);
            uvs[i] = new Vector2(
                (polygon[i].x - bounds.xMin) / bounds.width,
                (polygon[i].y - bounds.yMin) / bounds.height
            );
        }

        // 三角形：扇形三角化
        int[] triangles = new int[(polygon.Count - 2) * 3];
        for (int i = 0; i < polygon.Count - 2; i++)
        {
            triangles[i * 3] = 0;
            triangles[i * 3 + 1] = i + 1;
            triangles[i * 3 + 2] = i + 2;
        }

        mesh.vertices = vertices;
        mesh.uv = uvs;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        return mesh;
    }

    // -------------------------------------------------------
    // 工具方法
    // -------------------------------------------------------
    private Texture2D CreateSolidColorTexture(int w, int h, Color color)
    {
        // linear=false 显式指定 sRGB，与其他碎片纹理保持一致
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false, false);
        Color32[] pixels = new Color32[w * h];
        Color32 c32 = color;
        for (int i = 0; i < pixels.Length; i++) pixels[i] = c32;
        tex.SetPixels32(pixels);
        tex.Apply(false);
        return tex;
    }

    private Color CalculateAverageColor(Color[] pixels)
    {
        if (pixels == null || pixels.Length == 0) return Color.white;

        float r = 0, g = 0, b = 0, a = 0;
        int count = 0;

        foreach (var p in pixels)
        {
            if (p.a > 0.01f) { r += p.r; g += p.g; b += p.b; a += p.a; count++; }
        }

        return count == 0
            ? Color.clear
            : new Color(r / count, g / count, b / count, Mathf.Min(1f, a / count));
    }

    /// <summary>
    /// 获取一份可读的纹理副本。
    /// 注意颜色空间处理：必须使用 sRGB 读写，否则在 Linear 颜色空间下碎片会明显变暗。
    /// 如果源纹理本身可读，直接借用（不复制）；返回时通过 ownsSourceTexture 标记所有权。
    /// </summary>
    private Texture2D GetReadableTexture(Texture2D source, out bool ownsTexture)
    {
        if (source.isReadable)
        {
            // 直接借用，避免无谓的内存分配和 GC
            ownsTexture = false;
            return source;
        }

        // 使用 sRGB 读写，确保 Blit 写入 RT 时执行 Linear→sRGB 编码
        // 这样读出的字节就是正确的 sRGB 数据，与原始 Sprite 纹理行为一致
        RenderTexture rt = RenderTexture.GetTemporary(
            source.width, source.height, 0,
            RenderTextureFormat.ARGB32,
            RenderTextureReadWrite.sRGB
        );
        Graphics.Blit(source, rt);
        RenderTexture prev = RenderTexture.active;
        RenderTexture.active = rt;

        // mipChain=false, linear=false（即 sRGB）—— 必须显式指定 linear=false，
        // 否则在 Linear 颜色空间项目中采样时会被当成线性数据，导致颜色变暗
        Texture2D readable = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false, false);
        readable.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
        readable.Apply();

        RenderTexture.active = prev;
        RenderTexture.ReleaseTemporary(rt);
        ownsTexture = true;
        return readable;
    }

    /// <summary>
    /// 缓动函数：Ease Out Cubic
    /// </summary>
    private float EaseOutCubic(float t)
    {
        return 1f - Mathf.Pow(1f - t, 3f);
    }

    // -------------------------------------------------------
    // 清理
    // -------------------------------------------------------
    private void ClearFragments()
    {
        foreach (var f in fragments)
        {
            if (f is GridFragmentData grid)
            {
                if (grid.originalSprite != null)
                {
                    Destroy(grid.originalSprite.texture);
                    Destroy(grid.originalSprite);
                }
                if (grid.solidColorSprite != null)
                {
                    Destroy(grid.solidColorSprite.texture);
                    Destroy(grid.solidColorSprite);
                }
            }
            else if (f is GlassFragmentData glass)
            {
                if (glass.fragmentTexture != null)
                    Destroy(glass.fragmentTexture);
            }

            if (f.GameObject != null)
                Destroy(f.GameObject);
        }

        fragments.Clear();
        activeFragments.Clear();
        isFragmentsReady = false;
    }
}
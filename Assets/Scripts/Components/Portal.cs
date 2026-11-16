using UnityEngine;

/// <summary>
/// 传送门 —— 支持跨场景切换和同场景瞬移两种模式
/// 新增延迟，解决加载瞬间触发OnTriggerEnter直接跳关
/// </summary>
public class Portal : MonoBehaviour
{
    [Header("Portal Type")]
    [Tooltip("true = 切换到下一关场景；false = 同场景瞬移到 Target 位置")]
    public bool isLevelPortal = true;

    [Header("Teleport Target")]
    [Tooltip("同场景瞬移时的目标位置")]
    public Transform teleportTarget;

    [Header("Delay")]
    [Tooltip("场景加载后多久才能触发传送，防止开局瞬间误触发")]
    public float enableDelay = 0.5f;
    private bool canTeleport;

    [Header("Glow 终点圆柱发光（教授第6条）")]
    [Tooltip("勾选 = 运行时点亮这个蓝色圆柱")]
    public bool enableGlow = true;
    [Tooltip("自发光颜色（偏青的蓝，配合材质本身的 (0.2,0.9,0.9) 底色）")]
    public Color glowColor = new Color(0.25f, 0.95f, 1f);
    [Tooltip("自发光强度，取 2~4 可明显超过 Bloom 阈值")]
    public float glowIntensity = 3f;
    [Tooltip("呼吸（脉动）速度，0 = 恒定亮度")]
    public float glowPulseSpeed = 1.6f;
    [Tooltip("呼吸幅度，0.35 表示亮度在 ±35% 之间起伏")]
    [Range(0f, 1f)] public float glowPulseAmount = 0.35f;
    [Tooltip("是否额外挂一盏青色点光源，照亮圆柱周围地面")]
    public bool addGlowLight = true;
    public float glowLightRange = 5f;
    public float glowLightIntensity = 1.5f;

    // 运行时缓存的材质与灯光
    private Material[] glowMaterials;
    private Light glowLight;
    private Color[] baseEmissions;

    void Start()
    {
        canTeleport = false;
        Invoke(nameof(EnablePortal), enableDelay);

        SetupGlow();
    }

    /// <summary>把圆柱的材质改成自发光，可选再补一盏点光</summary>
    void SetupGlow()
    {
        if (!enableGlow) return;

        Renderer[] renderers = GetComponentsInChildren<Renderer>();
        if (renderers == null || renderers.Length == 0) return;

        // 收集所有材质（renderer.materials 会返回实例，改动不会污染场景里的共享材质）
        System.Collections.Generic.List<Material> mats = new System.Collections.Generic.List<Material>();
        foreach (Renderer r in renderers)
        {
            foreach (Material m in r.materials)
            {
                if (m != null && m.HasProperty("_EmissionColor"))
                    mats.Add(m);
            }
        }
        if (mats.Count == 0) return;

        glowMaterials = mats.ToArray();
        baseEmissions = new Color[glowMaterials.Length];

        for (int i = 0; i < glowMaterials.Length; i++)
        {
            glowMaterials[i].EnableKeyword("_EMISSION");
            // 让材质也进入 lightmap/GI 的自发光通道（只影响静态烘焙，运行时无害）
            glowMaterials[i].globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            glowMaterials[i].SetColor("_EmissionColor", glowColor * glowIntensity);
            baseEmissions[i] = glowColor * glowIntensity;
        }

        if (addGlowLight)
        {
            GameObject lightGo = new GameObject("PortalGlowLight");
            lightGo.transform.SetParent(transform, false);
            lightGo.transform.localPosition = new Vector3(0f, 0.8f, 0f);
            glowLight = lightGo.AddComponent<Light>();
            glowLight.type = LightType.Point;
            glowLight.color = glowColor;
            glowLight.range = glowLightRange;
            glowLight.intensity = glowLightIntensity;
            glowLight.shadows = LightShadows.None;
        }

        Debug.Log($"[Portal] 终点圆柱已点亮：材质 {glowMaterials.Length} 个，点光 {addGlowLight}");
    }

    void Update()
    {
        if (!enableGlow || glowMaterials == null || glowMaterials.Length == 0) return;

        // 呼吸效果：亮度在 glowIntensity 附近做正弦起伏
        float pulse = glowPulseSpeed <= 0.01f
            ? 1f
            : 1f + glowPulseAmount * Mathf.Sin(Time.time * glowPulseSpeed);

        for (int i = 0; i < glowMaterials.Length; i++)
        {
            if (glowMaterials[i] == null) continue;
            glowMaterials[i].SetColor("_EmissionColor", baseEmissions[i] * pulse);
        }

        if (glowLight != null)
            glowLight.intensity = glowLightIntensity * pulse;
    }

    void EnablePortal()
    {
        canTeleport = true;
    }

    void OnTriggerEnter(Collider other)
    {
        if (!canTeleport) return;

        // 通过组件识别玩家（不依赖 Tag，FBX 模型标签易失效）
        if (other.GetComponent<Player>() == null)
            return;

        if (isLevelPortal)
        {
            Debug.Log("进入传送门 → 加载下一关");
            if (LevelManager.Instance != null)
                LevelManager.Instance.LoadNextLevel();
        }
        else if (teleportTarget != null)
        {
            Debug.Log($"同场景瞬移到：{teleportTarget.position}");
            other.transform.position = teleportTarget.position;
        }
    }
}
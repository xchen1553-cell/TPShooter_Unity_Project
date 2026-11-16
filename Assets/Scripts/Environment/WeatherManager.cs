using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>天气类型（B1：3–4 种状态，先落地冬季，雨天/雾天后续按同一结构扩展）</summary>
public enum WeatherType
{
    Clear = 0,   // 晴天（原场景基础状态）
    Winter = 1,  // 冬季：下雪 + 雪地 + 冷光 + 移动变慢
    Rain = 2,    // 雨天：冷灰光 + 雨幕 + 地面湿滑（抓地力下降，停不住）
    Fog = 3,     // 雾天：浓雾低能见度 + 地面薄雾 + 敌人视野被压缩
}

/// <summary>一套天气参数</summary>
[System.Serializable]
public struct WeatherProfile
{
    public string label;                 // HUD 显示名
    public Color sunColor;
    public float sunIntensity;
    public Color ambientColor;
    public float ambientIntensity;
    public Color fogColor;
    public float fogStart;
    public float fogEnd;
    public Color groundColor;
    public float groundSmoothness;       // 地面光滑度（雨天调高 = 湿反光）
    public bool snowGround;              // 是否换成雪地纹理
    public float snowRate;               // 雪粒子每秒发射量，0 = 不下雪
    public float rainRate;               // 雨粒子每秒发射量，0 = 不下雨
    public float mistRate;               // 地面薄雾每秒发射量，0 = 没雾团
    public float playerSpeedMultiplier;  // 玩法规则（B3）：玩家移动速度倍率
    public float playerGrip;             // 玩法规则（B3）：抓地力（越大越不滑）
    public float enemyVisionMultiplier;  // 玩法规则（B3）：敌人视野倍率（雾天压缩）
}

/// <summary>
/// 天气系统（导师 B 线：B1 天气切换 / B2 光照 + VFX / B3 天气影响玩法）
///
/// 设计：一个常驻管理器 + 一组数据化的 WeatherProfile。切换天气只是换 profile，
/// 由 Update 在 1.5 秒内平滑过渡（光照、雾、地面色、雪量、玩家速度一起过渡），
/// 新增天气只需在 GetProfile 里加一个分支，主循环不动。
///
/// 冬季的玩法规则：玩家移动速度 ×0.65（冰雪拖慢脚步）。
/// 按键：1 = 晴天，2 = 冬季。
/// </summary>
public class WeatherManager : MonoBehaviour
{
    public static WeatherManager Instance { get; private set; }

    [Header("默认与过渡")]
    public WeatherType defaultWeather = WeatherType.Winter;
    public float transitionDuration = 1.5f;

    [Header("按键切换（演示用）")]
    public bool enableKeys = true;

    [Header("自动轮换（会议展示用）")]
    [Tooltip("勾选 = 每隔 autoCycleInterval 秒自动换天气，评委不用手动按键也能看到全部效果")]
    public bool autoCycle = true;
    [Tooltip("自动轮换间隔（秒）")]
    public float autoCycleInterval = 30f;
    [Tooltip("自动轮换是否包含晴天（默认不含，保证每次都在演示天气效果）")]
    public bool autoCycleIncludeClear = false;

    static readonly WeatherType[] autoCycleOrder =
    {
        WeatherType.Winter, WeatherType.Rain, WeatherType.Fog
    };

    float nextCycleTime;
    int lastShownSeconds = -1;

    [Header("雪粒子")]
    public float snowMaxParticles = 1500;
    public Vector3 snowAreaSize = new Vector3(26f, 2f, 26f);

    // ---- 运行时状态 ----
    WeatherType currentWeather;
    WeatherProfile target;

    // 当前"已应用"的数值（每帧向 target 逼近）
    Color appliedSunColor;
    float appliedSunIntensity;
    Color appliedAmbientColor;
    float appliedAmbientIntensity;
    Color appliedFogColor;
    float appliedFogStart;
    float appliedFogEnd;
    Color appliedGroundColor;
    float appliedGroundSmoothness;
    float appliedSnowRate;
    float appliedRainRate;
    float appliedMistRate;
    float appliedSpeedMultiplier;
    float appliedPlayerGrip;
    float appliedEnemyVision;

    /// <summary>敌人视野倍率（雾天 < 1），Enemy.DecideState 读取它来压缩侦测距离</summary>
    public float EnemyVisionMultiplier { get; private set; } = 1f;

    // ---- 场景引用（每次加载重新捕获）----
    Light sunLight;
    Material groundMaterial;
    Texture originalGroundTexture;
    Player player;
    ParticleSystem snow;
    ParticleSystem rain;
    ParticleSystem mist;

    int sceneApplyFrame = -1;
    bool sceneDirty;

    // ---- 静态资源 ----
    static Texture2D snowGroundTexture;
    static Texture2D snowParticleTexture;
    static Material snowParticleMaterial;
    static Material rainParticleMaterial;
    static Texture2D rainParticleTexture;

    // ============================================================ 启动
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        EnsureInstance();
        SceneManager.sceneLoaded -= OnAnySceneLoaded;
        SceneManager.sceneLoaded += OnAnySceneLoaded;
        if (Instance != null) Instance.MarkSceneDirty();
    }

    static void OnAnySceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (Instance != null) Instance.MarkSceneDirty();
    }

    static void EnsureInstance()
    {
        if (Instance != null) return;

        GameObject go = new GameObject("WeatherManager");
        DontDestroyOnLoad(go);
        Instance = go.AddComponent<WeatherManager>();
    }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        currentWeather = defaultWeather;
        target = GetProfile(currentWeather);
        nextCycleTime = Time.time + autoCycleInterval;
        BuildSnowSystem();
        BuildRainSystem();
        BuildMistSystem();
        BuildHud();
    }

    void MarkSceneDirty()
    {
        sceneDirty = true;
        sceneApplyFrame = Time.frameCount + 1;   // 等一帧，让 ArenaEnvironment 先把基础场景搭好
        nextCycleTime = Time.time + autoCycleInterval;   // 每进一个新场景重新计满一轮
        lastShownSeconds = -1;
    }

    // ============================================================ 每帧
    void Update()
    {
        if (sceneDirty && Time.frameCount >= sceneApplyFrame)
        {
            sceneDirty = false;
            CaptureSceneRefs();
            ApplyNow(target);      // 新场景直接到位，不做过渡
        }

        HandleKeys();
        HandleAutoCycle();
        UpdateCountdownLabel();
        StepTransition(Time.deltaTime);
    }

    void HandleKeys()
    {
        if (!enableKeys) return;
        if (Input.GetKeyDown(KeyCode.Alpha1)) SetWeather(WeatherType.Clear);
        if (Input.GetKeyDown(KeyCode.Alpha2)) SetWeather(WeatherType.Winter);
        if (Input.GetKeyDown(KeyCode.Alpha3)) SetWeather(WeatherType.Rain);
        if (Input.GetKeyDown(KeyCode.Alpha4)) SetWeather(WeatherType.Fog);
    }

    /// <summary>自动轮换：按 autoCycleOrder 顺序循环，间隔 autoCycleInterval 秒</summary>
    void HandleAutoCycle()
    {
        if (!autoCycle) return;
        if (Time.time < nextCycleTime) return;

        int index = System.Array.IndexOf(autoCycleOrder, currentWeather);
        WeatherType next = autoCycleIncludeClear && currentWeather == WeatherType.Clear
            ? autoCycleOrder[0]
            : autoCycleOrder[(index + 1) % autoCycleOrder.Length];

        SetWeather(next);
    }

    /// <summary>HUD 上的倒计时（每秒刷一次，避免每帧拼字符串）</summary>
    void UpdateCountdownLabel()
    {
        int seconds = autoCycle ? Mathf.Max(0, Mathf.CeilToInt(nextCycleTime - Time.time)) : -1;
        if (seconds == lastShownSeconds) return;
        lastShownSeconds = seconds;
        UpdateHudLabel();
    }

    /// <summary>切换天气（平滑过渡）；手动切换会把自动轮换的计时重置</summary>
    public void SetWeather(WeatherType type)
    {
        nextCycleTime = Time.time + autoCycleInterval;
        lastShownSeconds = -1;

        if (type == currentWeather)
        {
            UpdateHudLabel();
            return;
        }

        currentWeather = type;
        target = GetProfile(type);
        UpdateHudLabel();

        // 地面纹理切换是"跳变"的，直接换；颜色仍由过渡动画处理
        SwapGroundTexture(target.snowGround);

        Debug.Log($"[Weather] 切换到 {target.label}，玩家速度倍率 {target.playerSpeedMultiplier}");
    }

    void StepTransition(float dt)
    {
        float speed = transitionDuration <= 0.01f ? 1f : Mathf.Clamp01(dt / transitionDuration);

        appliedSunColor = Color.Lerp(appliedSunColor, target.sunColor, speed);
        appliedSunIntensity = Mathf.Lerp(appliedSunIntensity, target.sunIntensity, speed);
        appliedAmbientColor = Color.Lerp(appliedAmbientColor, target.ambientColor, speed);
        appliedAmbientIntensity = Mathf.Lerp(appliedAmbientIntensity, target.ambientIntensity, speed);
        appliedFogColor = Color.Lerp(appliedFogColor, target.fogColor, speed);
        appliedFogStart = Mathf.Lerp(appliedFogStart, target.fogStart, speed);
        appliedFogEnd = Mathf.Lerp(appliedFogEnd, target.fogEnd, speed);
        appliedGroundColor = Color.Lerp(appliedGroundColor, target.groundColor, speed);
        appliedGroundSmoothness = Mathf.Lerp(appliedGroundSmoothness, target.groundSmoothness, speed);
        appliedSnowRate = Mathf.Lerp(appliedSnowRate, target.snowRate, speed);
        appliedRainRate = Mathf.Lerp(appliedRainRate, target.rainRate, speed);
        appliedMistRate = Mathf.Lerp(appliedMistRate, target.mistRate, speed);
        appliedSpeedMultiplier = Mathf.Lerp(appliedSpeedMultiplier, target.playerSpeedMultiplier, speed);
        appliedPlayerGrip = Mathf.Lerp(appliedPlayerGrip, target.playerGrip, speed);
        appliedEnemyVision = Mathf.Lerp(appliedEnemyVision, target.enemyVisionMultiplier, speed);
        EnemyVisionMultiplier = appliedEnemyVision;

        PushToScene(dt);
    }

    /// <summary>立刻把目标参数应用到场景（切场景时用）</summary>
    void ApplyNow(WeatherProfile p)
    {
        appliedSunColor = p.sunColor;
        appliedSunIntensity = p.sunIntensity;
        appliedAmbientColor = p.ambientColor;
        appliedAmbientIntensity = p.ambientIntensity;
        appliedFogColor = p.fogColor;
        appliedFogStart = p.fogStart;
        appliedFogEnd = p.fogEnd;
        appliedGroundColor = p.groundColor;
        appliedGroundSmoothness = p.groundSmoothness;
        appliedSnowRate = p.snowRate;
        appliedRainRate = p.rainRate;
        appliedMistRate = p.mistRate;
        appliedSpeedMultiplier = p.playerSpeedMultiplier;
        appliedPlayerGrip = p.playerGrip;
        appliedEnemyVision = p.enemyVisionMultiplier;
        EnemyVisionMultiplier = appliedEnemyVision;

        SwapGroundTexture(p.snowGround);
        PushToScene(0f);
        UpdateHudLabel();
    }

    /// <summary>把当前数值真正写进灯光 / 雾 / 地面 / 雪 / 玩家</summary>
    void PushToScene(float dt)
    {
        if (sunLight != null)
        {
            sunLight.color = appliedSunColor;
            sunLight.intensity = appliedSunIntensity;
        }

        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = appliedAmbientColor;
        RenderSettings.ambientIntensity = appliedAmbientIntensity;

        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogColor = appliedFogColor;
        RenderSettings.fogStartDistance = appliedFogStart;
        RenderSettings.fogEndDistance = appliedFogEnd;

        if (groundMaterial != null)
        {
            if (groundMaterial.HasProperty("_BaseColor")) groundMaterial.SetColor("_BaseColor", appliedGroundColor);
            if (groundMaterial.HasProperty("_Color")) groundMaterial.SetColor("_Color", appliedGroundColor);
            if (groundMaterial.HasProperty("_Smoothness")) groundMaterial.SetFloat("_Smoothness", appliedGroundSmoothness);
            if (groundMaterial.HasProperty("_Glossiness")) groundMaterial.SetFloat("_Glossiness", appliedGroundSmoothness);
        }

        if (snow != null)
        {
            ParticleSystem.EmissionModule emission = snow.emission;
            emission.rateOverTime = appliedSnowRate;
        }

        if (rain != null)
        {
            ParticleSystem.EmissionModule emission = rain.emission;
            emission.rateOverTime = appliedRainRate;
        }

        if (mist != null)
        {
            ParticleSystem.EmissionModule emission = mist.emission;
            emission.rateOverTime = appliedMistRate;
        }

        if (player == null) player = Object.FindAnyObjectByType<Player>();
        if (player != null)
        {
            player.speedMultiplier = Mathf.Max(0.05f, appliedSpeedMultiplier);
            player.grip = Mathf.Clamp(appliedPlayerGrip, 0.05f, 1f);
        }

        FollowCamera(dt);
    }

    // ============================================================ 场景引用
    void CaptureSceneRefs()
    {
        sunLight = null;
        Light[] lights = Object.FindObjectsByType<Light>(FindObjectsInactive.Exclude);
        foreach (Light l in lights)
        {
            if (l.type == LightType.Directional) { sunLight = l; break; }
        }

        GameObject ground = GameObject.Find("Ground");
        if (ground != null)
        {
            Renderer r = ground.GetComponent<Renderer>();
            if (r != null)
            {
                groundMaterial = r.material;   // 实例化副本，避免污染共享材质
                originalGroundTexture = ReadGroundTexture(groundMaterial);
            }
        }

        player = Object.FindAnyObjectByType<Player>();
    }

    Texture ReadGroundTexture(Material m)
    {
        if (m == null) return null;
        if (m.HasProperty("_BaseMap")) return m.GetTexture("_BaseMap");
        if (m.HasProperty("_MainTex")) return m.GetTexture("_MainTex");
        return null;
    }

    void SwapGroundTexture(bool snowy)
    {
        if (groundMaterial == null) return;

        Texture tex = snowy ? (Texture)GetSnowGroundTexture() : originalGroundTexture;
        if (tex == null) return;

        if (groundMaterial.HasProperty("_BaseMap")) groundMaterial.SetTexture("_BaseMap", tex);
        if (groundMaterial.HasProperty("_MainTex")) groundMaterial.SetTexture("_MainTex", tex);
    }

    // ============================================================ 雪粒子
    void BuildSnowSystem()
    {
        GameObject go = new GameObject("SnowVFX");
        go.transform.SetParent(transform, false);

        snow = go.AddComponent<ParticleSystem>();
        ParticleSystem.MainModule main = snow.main;
        main.loop = true;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = new ParticleSystem.MinMaxCurve(5f, 8f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.4f, 1.2f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.035f, 0.09f);
        main.startColor = new Color(0.95f, 0.97f, 1f, 0.95f);
        main.maxParticles = Mathf.Max(100, (int)snowMaxParticles);
        main.gravityModifier = 0.03f;

        ParticleSystem.EmissionModule emission = snow.emission;
        emission.rateOverTime = 0f;

        ParticleSystem.ShapeModule shape = snow.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = snowAreaSize;

        // 缓慢下坠 + 轻微横向风偏（斜着飘才像雪）
        ParticleSystem.VelocityOverLifetimeModule velocity = snow.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.World;
        velocity.x = new ParticleSystem.MinMaxCurve(-0.9f, 0.9f);
        velocity.y = new ParticleSystem.MinMaxCurve(-6.5f, -3.5f);
        velocity.z = new ParticleSystem.MinMaxCurve(-0.6f, 0.6f);

        ParticleSystem.ColorOverLifetimeModule colorOverLife = snow.colorOverLifetime;
        colorOverLife.enabled = false;

        ParticleSystemRenderer psr = go.GetComponent<ParticleSystemRenderer>();
        psr.renderMode = ParticleSystemRenderMode.Billboard;
        Material mat = GetSnowParticleMaterial();
        if (mat != null) psr.material = mat;

        snow.Play();
    }

    /// <summary>让雪区跟着摄像机（永远罩住玩家视野）</summary>
    void FollowCamera(float dt)
    {
        Camera cam = Camera.main;
        if (cam == null) return;

        Vector3 camPos = cam.transform.position;
        Vector3 forward = cam.transform.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.0001f) forward = Vector3.forward;
        forward.Normalize();

        // 雪：落在相机前方 + 上方，罩住视野
        if (snow != null)
        {
            Vector3 snowPos = camPos + forward * 12f + Vector3.up * 9f;
            snow.transform.position = Vector3.Lerp(snow.transform.position, snowPos, Mathf.Clamp01(dt * 6f));
        }

        // 雨：发射点在更高处，雨幕从头顶落下
        if (rain != null)
        {
            Vector3 rainPos = camPos + forward * 8f + Vector3.up * 14f;
            rain.transform.position = Vector3.Lerp(rain.transform.position, rainPos, Mathf.Clamp01(dt * 6f));
        }

        // 雾团：贴在玩家周围的地面高度慢速飘动
        if (mist != null)
        {
            Vector3 mistPos = camPos + forward * 6f + Vector3.up * 2.2f;
            mist.transform.position = Vector3.Lerp(mist.transform.position, mistPos, Mathf.Clamp01(dt * 3f));
        }
    }

    /// <summary>雨：细长雨丝，用 Stretch 渲染模式按速度拉长</summary>
    void BuildRainSystem()
    {
        GameObject go = new GameObject("RainVFX");
        go.transform.SetParent(transform, false);

        rain = go.AddComponent<ParticleSystem>();
        ParticleSystem.MainModule main = rain.main;
        main.loop = true;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = new ParticleSystem.MinMaxCurve(1.1f, 1.7f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.5f, 1f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.02f, 0.045f);
        main.startColor = new Color(0.78f, 0.86f, 1f, 0.55f);
        main.maxParticles = 3000;
        main.gravityModifier = 0.2f;

        ParticleSystem.EmissionModule emission = rain.emission;
        emission.rateOverTime = 0f;

        ParticleSystem.ShapeModule shape = rain.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(30f, 1f, 30f);

        ParticleSystem.VelocityOverLifetimeModule velocity = rain.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.World;
        velocity.x = new ParticleSystem.MinMaxCurve(-0.7f, 0.7f);
        velocity.y = new ParticleSystem.MinMaxCurve(-24f, -18f);   // 落得比雪快得多
        velocity.z = new ParticleSystem.MinMaxCurve(-0.7f, 0.7f);

        ParticleSystemRenderer psr = go.GetComponent<ParticleSystemRenderer>();
        psr.renderMode = ParticleSystemRenderMode.Stretch;          // 按速度拉成雨丝
        psr.velocityScale = 0.09f;
        psr.lengthScale = 2.2f;

        Material mat = GetRainParticleMaterial();
        if (mat != null) psr.material = mat;

        rain.Play();
    }

    /// <summary>地面薄雾：大尺寸柔边贴片慢速飘动，让雾看起来有体积</summary>
    void BuildMistSystem()
    {
        GameObject go = new GameObject("MistVFX");
        go.transform.SetParent(transform, false);

        mist = go.AddComponent<ParticleSystem>();
        ParticleSystem.MainModule main = mist.main;
        main.loop = true;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = new ParticleSystem.MinMaxCurve(8f, 14f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.05f, 0.15f);
        main.startSize = new ParticleSystem.MinMaxCurve(3.5f, 6.5f);
        main.startColor = new Color(0.85f, 0.87f, 0.90f, 0.10f);   // 很淡，只加"厚度"
        main.maxParticles = 70;
        main.gravityModifier = 0f;

        ParticleSystem.EmissionModule emission = mist.emission;
        emission.rateOverTime = 0f;

        ParticleSystem.ShapeModule shape = mist.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(22f, 3f, 22f);

        ParticleSystem.VelocityOverLifetimeModule velocity = mist.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.World;
        velocity.x = new ParticleSystem.MinMaxCurve(-0.18f, 0.18f);
        velocity.y = new ParticleSystem.MinMaxCurve(0f, 0.05f);
        velocity.z = new ParticleSystem.MinMaxCurve(-0.18f, 0.18f);

        ParticleSystemRenderer psr = go.GetComponent<ParticleSystemRenderer>();
        psr.renderMode = ParticleSystemRenderMode.Billboard;
        Material mat = GetSnowParticleMaterial();   // 复用同一张柔边圆点
        if (mat != null) psr.material = mat;

        mist.Play();
    }

    // ============================================================ HUD
    void BuildHud()
    {
        GameObject canvasGo = new GameObject("WeatherHUD");
        canvasGo.transform.SetParent(transform, false);

        Canvas canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 5;

        CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
        scaler.scaleFactor = 1f;

        GameObject textGo = new GameObject("WeatherLabel");
        textGo.transform.SetParent(canvasGo.transform, false);

        Text text = textGo.AddComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = 20;
        text.fontStyle = FontStyle.Bold;
        text.color = Color.white;
        text.alignment = TextAnchor.UpperRight;
        text.raycastTarget = false;

        Shadow shadow = textGo.AddComponent<Shadow>();
        shadow.effectDistance = new Vector2(1.5f, -1.5f);
        shadow.effectColor = new Color(0f, 0f, 0f, 0.65f);

        RectTransform rt = textGo.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(1f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(1f, 1f);
        rt.anchoredPosition = new Vector2(-14f, -14f);
        rt.sizeDelta = new Vector2(320f, 30f);

        weatherLabel = text;
        lastShownSeconds = -1;
        UpdateHudLabel();
    }

    Text weatherLabel;

    void UpdateHudLabel()
    {
        if (weatherLabel == null) return;

        string suffix = (autoCycle && lastShownSeconds >= 0)
            ? "   ·   NEXT " + lastShownSeconds + "s"
            : "";
        weatherLabel.text = "WEATHER: " + target.label + suffix;
    }

    // ============================================================ 天气数据表
    public static WeatherProfile GetProfile(WeatherType type)
    {
        WeatherProfile p = new WeatherProfile();
        p.label = "CLEAR";
        p.sunColor = new Color(1f, 0.95f, 0.86f);
        p.sunIntensity = 1.45f;
        p.ambientColor = new Color(0.42f, 0.45f, 0.50f);
        p.ambientIntensity = 1.15f;
        p.fogColor = new Color(0.56f, 0.62f, 0.70f);
        p.fogStart = 18f;
        p.fogEnd = 85f;
        p.groundColor = new Color(0.82f, 0.84f, 0.80f);
        p.groundSmoothness = 0.12f;
        p.snowGround = false;
        p.snowRate = 0f;
        p.rainRate = 0f;
        p.mistRate = 0f;
        p.playerSpeedMultiplier = 1f;
        p.playerGrip = 1f;
        p.enemyVisionMultiplier = 1f;

        if (type == WeatherType.Winter)
        {
            p.label = "WINTER";
            p.sunColor = new Color(0.78f, 0.86f, 1f);      // 冷白偏蓝
            p.sunIntensity = 0.85f;
            p.ambientColor = new Color(0.56f, 0.66f, 0.82f); // 雪地反光偏冷
            p.ambientIntensity = 1f;
            p.fogColor = new Color(0.82f, 0.87f, 0.94f);    // 雪雾：白里透蓝
            p.fogStart = 10f;
            p.fogEnd = 48f;                                  // 能见度明显下降
            p.groundColor = new Color(0.98f, 0.99f, 1f);
            p.groundSmoothness = 0.06f;                     // 雪面偏哑光
            p.snowGround = true;
            p.snowRate = 320f;
            p.playerSpeedMultiplier = 0.65f;                // B3 规则 1：冰雪拖慢脚步
            p.playerGrip = 1f;
        }

        if (type == WeatherType.Rain)
        {
            p.label = "RAIN";
            p.sunColor = new Color(0.72f, 0.78f, 0.88f);    // 阴沉冷灰
            p.sunIntensity = 0.55f;
            p.ambientColor = new Color(0.40f, 0.45f, 0.52f);
            p.ambientIntensity = 0.95f;
            p.fogColor = new Color(0.62f, 0.67f, 0.73f);
            p.fogStart = 12f;
            p.fogEnd = 60f;
            p.groundColor = new Color(0.42f, 0.45f, 0.48f);  // 地面被淋湿变暗
            p.groundSmoothness = 0.55f;                      // 湿度反光
            p.snowGround = false;
            p.rainRate = 900f;
            p.playerSpeedMultiplier = 1f;                    // 雨天不减速，改变的是手感
            p.playerGrip = 0.30f;                            // B3 规则 2：湿滑，停不住/转向打滑
        }

        if (type == WeatherType.Fog)
        {
            p.label = "FOG";
            p.sunColor = new Color(0.88f, 0.89f, 0.92f);     // 灰白漫射光
            p.sunIntensity = 0.70f;
            p.ambientColor = new Color(0.56f, 0.58f, 0.62f);
            p.ambientIntensity = 0.90f;
            p.fogColor = new Color(0.74f, 0.76f, 0.80f);
            p.fogStart = 4f;
            p.fogEnd = 22f;                                  // 能见度压到很近
            p.groundColor = new Color(0.50f, 0.52f, 0.52f);
            p.groundSmoothness = 0.16f;
            p.snowGround = false;
            p.mistRate = 8f;                                 // 地面薄雾（体积感）
            p.playerSpeedMultiplier = 1f;
            p.playerGrip = 1f;
            p.enemyVisionMultiplier = 0.45f;                 // B3 规则 3：敌人视野被压缩，可潜行贴近
        }

        return p;
    }

    // ============================================================ 运行时纹理/材质
    Texture GetSnowGroundTexture()
    {
        if (snowGroundTexture != null) return snowGroundTexture;

        const int size = 256;
        const int cell = 64;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, true);
        tex.wrapMode = TextureWrapMode.Repeat;
        tex.filterMode = FilterMode.Bilinear;

        Color baseSnow = new Color(0.93f, 0.95f, 0.98f);
        Color dip = new Color(0.80f, 0.85f, 0.93f);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float noise = Mathf.PerlinNoise(x * 0.08f, y * 0.08f);
                Color c = Color.Lerp(baseSnow, dip, noise * 0.35f);
                if ((x % cell == 0) || (y % cell == 0)) c = Color.Lerp(c, dip, 0.5f); // 淡淡的格线
                tex.SetPixel(x, y, c);
            }
        }
        tex.Apply(true, false);
        snowGroundTexture = tex;
        return tex;
    }

    Material GetSnowParticleMaterial()
    {
        if (snowParticleMaterial != null) return snowParticleMaterial;

        Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (shader == null) shader = Shader.Find("Sprites/Default");
        if (shader == null) return null;

        Material m = new Material(shader);
        m.name = "SnowParticle";

        Texture2D tex = GetSnowParticleTexture();
        if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", tex);
        if (m.HasProperty("_MainTex")) m.SetTexture("_MainTex", tex);
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", Color.white);
        m.color = Color.white;

        snowParticleMaterial = m;
        return m;
    }

    /// <summary>程序化生成一枚"柔边圆点"，避免依赖美术资源</summary>
    Texture2D GetSnowParticleTexture()
    {
        if (snowParticleTexture != null) return snowParticleTexture;

        const int size = 32;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;

        float half = size * 0.5f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = (x + 0.5f - half) / half;
                float dy = (y + 0.5f - half) / half;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                float a = Mathf.Clamp01(1f - d);
                a = a * a;   // 边缘更柔和
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        }
        tex.Apply(false, false);
        snowParticleTexture = tex;
        return tex;
    }

    Material GetRainParticleMaterial()
    {
        if (rainParticleMaterial != null) return rainParticleMaterial;

        Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (shader == null) shader = Shader.Find("Sprites/Default");
        if (shader == null) return null;

        Material m = new Material(shader);
        m.name = "RainParticle";

        Texture2D tex = GetRainParticleTexture();
        if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", tex);
        if (m.HasProperty("_MainTex")) m.SetTexture("_MainTex", tex);
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", Color.white);
        m.color = Color.white;

        rainParticleMaterial = m;
        return m;
    }

    /// <summary>雨丝贴图：上下渐变、左右收边的细长条</summary>
    Texture2D GetRainParticleTexture()
    {
        if (rainParticleTexture != null) return rainParticleTexture;

        const int w = 8;
        const int h = 64;
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;

        for (int y = 0; y < h; y++)
        {
            float ty = (y + 0.5f) / h;
            float vertical = Mathf.Sin(ty * Mathf.PI);     // 上下两端淡出
            for (int x = 0; x < w; x++)
            {
                float tx = (x + 0.5f) / w;
                float horizontal = 1f - Mathf.Abs(tx - 0.5f) * 2f;  // 中间实、两侧虚
                float a = Mathf.Clamp01(vertical * horizontal);
                a = a * a;
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        }
        tex.Apply(false, false);
        rainParticleTexture = tex;
        return tex;
    }
}

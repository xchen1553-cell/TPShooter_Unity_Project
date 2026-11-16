using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

/// <summary>
/// 场景美化（运行时构建，不改场景文件）
/// Level1 / Level2 加载后自动执行：
///   1. 光照：暖色太阳 + 软阴影 + 环境光
///   2. 雾：线性雾，既增加空间纵深，也顺手遮住 50×50 地面的生硬边缘
///   3. 地面：运行时生成带网格与噪点的纹理，替换原来的单调纯色
///   4. 场景装饰：外围石柱、角落木箱堆、散落岩石（自动避开中央战斗区）
///
/// 全部参数集中在文件顶部的常量里，改数字即可，不需要动场景。
/// 这层也是后面天气系统的地基 —— 光照、雾、环境光都已经被参数化了。
/// </summary>
public static class ArenaEnvironment
{
    // ==================== 总开关 ====================
    static readonly bool enableDress = true;
    static readonly bool enableShadow = true;
    static readonly bool enableFog = true;
    static readonly bool enableDecor = true;

    // ==================== 光照 ====================
    static readonly Color sunColor = new Color(1f, 0.95f, 0.86f);   // 略暖的太阳
    static readonly float sunIntensity = 1.45f;
    static readonly Vector3 sunEuler = new Vector3(48f, -35f, 0f);
    static readonly float ambientIntensity = 1.15f;

    // ==================== 雾 ====================
    static readonly Color fogColor = new Color(0.56f, 0.62f, 0.70f);
    static readonly float fogStart = 18f;
    static readonly float fogEnd = 85f;

    // ==================== 地面 ====================
    static readonly Color groundBaseColor = new Color(0.20f, 0.22f, 0.19f);  // 深苔绿灰
    static readonly Color groundLineColor = new Color(0.27f, 0.30f, 0.25f);
    static readonly float groundSmoothness = 0.12f;
    static readonly Vector2 groundTiling = new Vector2(10f, 10f);

    // ==================== 装饰 ====================
    static readonly int pillarCount = 18;
    static readonly int boulderCount = 12;
    static readonly Color stoneColor = new Color(0.38f, 0.39f, 0.42f);
    static readonly Color crateColor = new Color(0.36f, 0.27f, 0.18f);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        if (!enableDress) return;

        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;

        Dress(SceneManager.GetActiveScene());
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        Dress(scene);
    }

    static void Dress(Scene scene)
    {
        if (scene.name != "Level1" && scene.name != "Level2") return;

        GameObject ground = GameObject.Find("Ground");
        if (ground == null) return;
        if (GameObject.Find("EnvironmentDecor") != null) return;   // 幂等：已经装饰过就跳过

        SetupLightingAndFog();
        SetupGround(ground);
        if (enableDecor) BuildDecor(ground);

        Debug.Log("[ArenaEnvironment] 场景美化完成：" + scene.name);
    }

    // ---------------------------------------------------------------- 光照 / 雾
    static void SetupLightingAndFog()
    {
        Light[] lights = Object.FindObjectsByType<Light>(FindObjectsInactive.Exclude);
        foreach (Light l in lights)
        {
            if (l.type != LightType.Directional) continue;

            l.color = sunColor;
            l.intensity = sunIntensity;
            if (enableShadow)
            {
                l.shadows = LightShadows.Soft;
                l.shadowStrength = 0.72f;
            }
            l.transform.rotation = Quaternion.Euler(sunEuler);
            RenderSettings.sun = l;
        }

        RenderSettings.ambientMode = AmbientMode.Skybox;
        RenderSettings.ambientIntensity = ambientIntensity;

        if (enableFog)
        {
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = fogColor;
            RenderSettings.fogStartDistance = fogStart;
            RenderSettings.fogEndDistance = fogEnd;
        }

        // 相机若被设成纯色背景，改回天空盒，让天空参与画面
        Camera cam = Camera.main;
        if (cam != null && cam.clearFlags == CameraClearFlags.SolidColor)
        {
            cam.clearFlags = CameraClearFlags.Skybox;
            cam.farClipPlane = Mathf.Max(cam.farClipPlane, 120f);
        }
    }

    // ---------------------------------------------------------------- 地面
    static void SetupGround(GameObject ground)
    {
        Renderer r = ground.GetComponent<Renderer>();
        if (r == null) return;

        Material m = r.material;

        SetColor(m, "_BaseColor", groundBaseColor);
        SetColor(m, "_Color", groundBaseColor);
        SetFloat(m, "_Smoothness", groundSmoothness);
        SetFloat(m, "_Glossiness", groundSmoothness);
        SetFloat(m, "_Metallic", 0f);

        Texture2D tex = CreateGroundTexture();
        if (tex != null)
        {
            if (m.HasProperty("_BaseMap"))
            {
                m.SetTexture("_BaseMap", tex);
                m.SetTextureScale("_BaseMap", groundTiling);
            }
            if (m.HasProperty("_MainTex"))
            {
                m.SetTexture("_MainTex", tex);
                m.SetTextureScale("_MainTex", groundTiling);
            }
            // 有了网格纹理，底色不再需要压那么深
            SetColor(m, "_BaseColor", new Color(0.82f, 0.84f, 0.80f));
            SetColor(m, "_Color", new Color(0.82f, 0.84f, 0.80f));
        }
    }

    /// <summary>生成一张"网格 + 噪点"的地面纹理（256×256，可平铺）</summary>
    static Texture2D CreateGroundTexture()
    {
        const int size = 256;
        const int cell = 64;

        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, true);
        tex.wrapMode = TextureWrapMode.Repeat;
        tex.filterMode = FilterMode.Bilinear;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float noise = Mathf.PerlinNoise(x * 0.06f, y * 0.06f);
                Color c = Color.Lerp(groundBaseColor, groundLineColor, noise * 0.45f);

                bool gridLine = (x % cell == 0) || (y % cell == 0);
                if (gridLine) c = groundLineColor * 1.25f;

                tex.SetPixel(x, y, c);
            }
        }
        tex.Apply(true, false);
        return tex;
    }

    // ---------------------------------------------------------------- 装饰
    static void BuildDecor(GameObject ground)
    {
        Renderer groundRenderer = ground.GetComponent<Renderer>();
        if (groundRenderer == null) return;

        Bounds bounds = groundRenderer.bounds;
        Vector3 center = new Vector3(bounds.center.x, 0f, bounds.center.z);
        float radius = Mathf.Min(bounds.extents.x, bounds.extents.z) * 0.90f;

        GameObject root = new GameObject("EnvironmentDecor");

        Material stone = CreateMaterial(stoneColor, 0.18f);
        Material crate = CreateMaterial(crateColor, 0.10f);

        System.Random rnd = new System.Random(20261114);   // 固定种子，每次布局一致

        // 记录角色位置，装饰物要避开它们，避免敌人/玩家出生就被卡在石头里
        System.Collections.Generic.List<Vector3> occupied = new System.Collections.Generic.List<Vector3>();
        foreach (Enemy e in Object.FindObjectsByType<Enemy>(FindObjectsInactive.Exclude))
            occupied.Add(e.transform.position);
        Player player = Object.FindAnyObjectByType<Player>();
        if (player != null) occupied.Add(player.transform.position);

        // 1) 外围石柱
        if (stone != null)
        {
            for (int i = 0; i < pillarCount; i++)
            {
                float angle = (i * 360f / pillarCount) * Mathf.Deg2Rad;
                // 每根柱子的方位角轻微微调，避免看起来像机械排列
                float jitter = ((float)rnd.NextDouble() - 0.5f) * 4f;
                angle += jitter * Mathf.Deg2Rad;

                float dist = radius * (0.94f + (float)rnd.NextDouble() * 0.06f);
                float height = 4.2f + (float)rnd.NextDouble() * 3.4f;
                float width = 1.1f + (float)rnd.NextDouble() * 0.6f;

                Vector3 pos = center + new Vector3(Mathf.Cos(angle) * dist, height * 0.5f, Mathf.Sin(angle) * dist);
                if (!IsClear(pos, occupied, 2.5f)) continue;

                GameObject pillar = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                pillar.name = "Pillar_" + i;
                pillar.transform.SetParent(root.transform, true);
                pillar.transform.position = pos;
                pillar.transform.localScale = new Vector3(width, height * 0.5f, width);
                pillar.transform.rotation = Quaternion.Euler(0f, (float)rnd.NextDouble() * 360f, 0f);
                ApplyMaterial(pillar, stone);
            }
        }

        // 2) 四角木箱堆（放在对角线、靠外圈，尽量不挡中央战斗区）
        if (crate != null)
        {
            float diag = radius * 0.78f;
            Vector3[] corners = new Vector3[]
            {
                new Vector3( diag, 0f,  diag),
                new Vector3(-diag, 0f,  diag),
                new Vector3( diag, 0f, -diag),
                new Vector3(-diag, 0f, -diag),
            };

            foreach (Vector3 corner in corners)
            {
                Vector3 basePos = center + corner;
                if (!IsClear(basePos, occupied, 3f)) continue;

                int stack = 3 + rnd.Next(2);
                for (int i = 0; i < stack; i++)
                {
                    float s = 1.1f + (float)rnd.NextDouble() * 0.3f;
                    Vector3 pos = basePos + new Vector3(
                        ((float)rnd.NextDouble() - 0.5f) * 1.6f,
                        s * 0.5f + i * (s * 0.92f),
                        ((float)rnd.NextDouble() - 0.5f) * 1.6f);

                    GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    box.name = "Crate_" + i;
                    box.transform.SetParent(root.transform, true);
                    box.transform.position = pos;
                    box.transform.localScale = Vector3.one * s;
                    box.transform.rotation = Quaternion.Euler(
                        0f,
                        ((float)rnd.NextDouble() - 0.5f) * 40f,
                        0f);
                    ApplyMaterial(box, crate);
                }
            }
        }

        // 3) 散落岩石
        if (stone != null)
        {
            for (int i = 0; i < boulderCount; i++)
            {
                float angle = (float)rnd.NextDouble() * Mathf.PI * 2f;
                float dist = radius * (0.82f + (float)rnd.NextDouble() * 0.14f);
                float size = 0.9f + (float)rnd.NextDouble() * 1.8f;

                Vector3 pos = center + new Vector3(
                    Mathf.Cos(angle) * dist,
                    size * 0.35f,
                    Mathf.Sin(angle) * dist);
                if (!IsClear(pos, occupied, 2f)) continue;

                GameObject rock = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                rock.name = "Boulder_" + i;
                rock.transform.SetParent(root.transform, true);
                rock.transform.position = pos;
                rock.transform.localScale = new Vector3(size, size * 0.65f, size);
                rock.transform.rotation = Quaternion.Euler(
                    (float)rnd.NextDouble() * 30f,
                    (float)rnd.NextDouble() * 360f,
                    (float)rnd.NextDouble() * 30f);
                ApplyMaterial(rock, stone);
            }
        }
    }

    // ---------------------------------------------------------------- 工具
    /// <summary>检查某位置是否离角色足够远（避免装饰物把角色卡住）</summary>
    static bool IsClear(Vector3 position, System.Collections.Generic.List<Vector3> occupied, float clearance)
    {
        foreach (Vector3 o in occupied)
        {
            Vector3 d = o - position;
            d.y = 0f;
            if (d.sqrMagnitude < clearance * clearance) return false;
        }
        return true;
    }

    static Material CreateMaterial(Color color, float smoothness)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) return null;

        Material m = new Material(shader);
        m.name = "ArenaStone";
        SetColor(m, "_BaseColor", color);
        SetColor(m, "_Color", color);
        SetFloat(m, "_Smoothness", smoothness);
        SetFloat(m, "_Metallic", 0f);
        return m;
    }

    static void ApplyMaterial(GameObject go, Material m)
    {
        if (m == null) return;
        Renderer r = go.GetComponent<Renderer>();
        if (r != null) r.material = m;
    }

    static void SetColor(Material m, string prop, Color c)
    {
        if (m != null && m.HasProperty(prop)) m.SetColor(prop, c);
    }

    static void SetFloat(Material m, string prop, float v)
    {
        if (m != null && m.HasProperty(prop)) m.SetFloat(prop, v);
    }
}

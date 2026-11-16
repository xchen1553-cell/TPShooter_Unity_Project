using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// UI 管理器 —— 屏幕上显示分数、生命、血条与血量数字（HP: 100 → 80 → … → 0）
/// 通过 GameManager 单例读取数据，通过 Character 的受伤/死亡事件驱动刷新
/// 血量归零：先把数字与血条归零、弹出 GameOver 面板，再由 GameManager 延时切场景
///
/// 【自检说明】场景里的 HealthBar 是一张 Image，但 m_Sprite 为空 —— Unity 的 UI Image
/// 在没有 Sprite 时不渲染任何像素，血条会"消失"。Score/Lives 文本引用的是旧版内置
/// Arial 字体，在 Unity 6 里已不存在，同样不渲染。所以本脚本在 Start 时统一做一次
/// HUD 自检：缺的东西就地补齐，保证任何场景里 HUD 都能显示。
/// </summary>
public class UIManager : MonoBehaviour
{
    [Header("UI References")]
    public Text scoreText;
    public Text livesText;
    public Image healthBar;
    [Tooltip("血量数字文本（如 HP: 100）；留空会在 Canvas 下自动创建一个")]
    public Text healthText;
    public GameObject gameOverPanel;
    [Tooltip("血条底槽（可选）；留空会自动在血条后面生成一个")]
    public Image healthBarBackground;

    [Header("Player Reference")]
    [Tooltip("玩家引用；留空会在 Start 时自动查找场景中的 Player")]
    public Character player;

    [Header("Health Colors 血量分段配色")]
    [Tooltip("血量 > 50% 的颜色")]
    public Color healthNormalColor = Color.white;
    [Tooltip("血量 25%~50% 的颜色")]
    public Color healthWarnColor = new Color(1f, 0.8f, 0.2f);
    [Tooltip("血量 <= 25% 的颜色")]
    public Color healthLowColor = Color.red;

    [Header("HUD Layout HUD 布局")]
    public float barWidth = 220f;
    public float barHeight = 20f;
    public float barBottomOffset = 20f;

    // 运行时生成的纯白 Sprite（用于补上缺失的 m_Sprite）
    static Sprite _runtimeSprite;
    // 当前真正订阅了事件的玩家（用于安全解绑，避免访问已销毁对象）
    Character _boundPlayer;

    void Start()
    {
        ResolvePlayer();
        EnsureHud();       // 先保证 HUD 能被渲染，再谈数值
        BindPlayer();
        RefreshHealth();
    }

    void OnDisable()
    {
        UnbindPlayer();
    }

    void OnDestroy()
    {
        UnbindPlayer();
    }

    // ---------------------------------------------------------------- 玩家绑定

    /// <summary>兜底绑定玩家：Inspector 引用失效（或指向已销毁的预制体占位对象）时自动查找</summary>
    void ResolvePlayer()
    {
        if (IsAlive(player)) return;
        player = FindAnyObjectByType<Player>();
        if (player == null) Debug.LogWarning("[HUD] 场景中找不到 Player，血量 UI 无法更新");
    }

    /// <summary>对象是否存在且未被销毁（Unity 的 == null 对已销毁对象返回 true，但直接访问会抛异常）</summary>
    bool IsAlive(Object obj)
    {
        if (obj == null) return false;
        try { return obj is not Component c || c.gameObject != null; }
        catch (MissingReferenceException) { return false; }
    }

    void BindPlayer()
    {
        if (player == null) return;
        if (_boundPlayer == player) return;

        UnbindPlayer();
        try
        {
            player.OnDamaged += UpdateHealthBar;
            player.OnDeath += ShowGameOver;
            _boundPlayer = player;
        }
        catch (MissingReferenceException)
        {
            Debug.LogWarning("[HUD] 玩家引用已失效，重建绑定");
            player = FindAnyObjectByType<Player>();
            if (player != null)
            {
                player.OnDamaged += UpdateHealthBar;
                player.OnDeath += ShowGameOver;
                _boundPlayer = player;
            }
        }
    }

    void UnbindPlayer()
    {
        if (_boundPlayer == null) return;
        try
        {
            _boundPlayer.OnDamaged -= UpdateHealthBar;
            _boundPlayer.OnDeath -= ShowGameOver;
        }
        catch (MissingReferenceException) { /* 场景卸载时对象已销毁，忽略 */ }
        _boundPlayer = null;
    }

    // ---------------------------------------------------------------- HUD 自检

    /// <summary>补齐所有会让 HUD 渲染不出来的缺口：血条 Sprite、底槽、字体、缺失对象</summary>
    void EnsureHud()
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null) canvas = FindAnyObjectByType<Canvas>();

        if (canvas == null)
        {
            Debug.LogError("[HUD] 场景里没有 Canvas，无法显示 UI");
            return;
        }

        // 屏幕空间相机模式但没指定相机时，UI 会完全不渲染 —— 强制回到 Overlay
        if (canvas.renderMode == RenderMode.ScreenSpaceCamera && canvas.worldCamera == null)
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        EnsureHealthBar(canvas);
        EnsureHealthBarBackground(canvas);
        EnsureHealthText(canvas);

        // 分数/生命文本：Unity 6 里旧的内置 Arial 字体已不存在，字体为空则不渲染
        FixTextFont(scoreText);
        FixTextFont(livesText);

        Debug.Log($"[HUD] canvas={canvas.name}/{canvas.renderMode}, bar={(healthBar != null ? healthBar.name : "null")}" +
                  $", sprite={(healthBar != null && healthBar.sprite != null ? healthBar.sprite.name : "null")}" +
                  $", hpText={(healthText != null ? healthText.name : "null")}" +
                  $", scoreText={(scoreText != null ? scoreText.name : "null")}" +
                  $", player={(player != null ? player.name : "null")}");
    }

    /// <summary>血条：缺失就创建；有对象但 Sprite 为空就补一张纯白 Sprite（否则 Image 不渲染）</summary>
    void EnsureHealthBar(Canvas canvas)
    {
        if (healthBar == null)
        {
            GameObject go = new GameObject("HealthBar", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(canvas.transform, false);
            RectTransform rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(0.5f, 0f);
            rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(0f, barBottomOffset);
            rt.sizeDelta = new Vector2(barWidth, barHeight);
            Image img = go.GetComponent<Image>();
            img.raycastTarget = false;
            healthBar = img;
        }

        if (healthBar.sprite == null)
            healthBar.sprite = GetRuntimeSprite();

        healthBar.type = Image.Type.Filled;
        healthBar.fillMethod = Image.FillMethod.Horizontal;
        healthBar.fillOrigin = (int)Image.OriginHorizontal.Left;
        healthBar.fillClockwise = true;
        healthBar.raycastTarget = false;
        healthBar.color = healthNormalColor;
    }

    /// <summary>底槽：一条深色细框，让绿色血条在任何地面上都能看出剩余比例</summary>
    void EnsureHealthBarBackground(Canvas canvas)
    {
        if (healthBarBackground == null)
        {
            Transform existing = canvas.transform.Find("HealthBarBG");
            if (existing != null) healthBarBackground = existing.GetComponent<Image>();
        }

        if (healthBarBackground == null)
        {
            GameObject go = new GameObject("HealthBarBG", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(healthBar.transform.parent, false);
            RectTransform rt = (RectTransform)go.transform;
            RectTransform barRt = (RectTransform)healthBar.transform;
            rt.anchorMin = barRt.anchorMin;
            rt.anchorMax = barRt.anchorMax;
            rt.pivot = barRt.pivot;
            rt.anchoredPosition = barRt.anchoredPosition;
            rt.sizeDelta = barRt.sizeDelta + new Vector2(4f, 4f);
            Image img = go.GetComponent<Image>();
            img.raycastTarget = false;
            healthBarBackground = img;
        }

        healthBarBackground.sprite = GetRuntimeSprite();
        healthBarBackground.type = Image.Type.Simple;
        healthBarBackground.color = new Color(0f, 0f, 0f, 0.55f);
        healthBarBackground.raycastTarget = false;

        // 画在血条后面
        healthBarBackground.transform.SetSiblingIndex(Mathf.Max(0, healthBar.transform.GetSiblingIndex()));
    }

    /// <summary>血量数字：缺失就补建，字体为空就补字体</summary>
    void EnsureHealthText(Canvas canvas)
    {
        if (healthText == null)
        {
            GameObject go = new GameObject("HealthText", typeof(RectTransform));
            go.transform.SetParent(canvas.transform, false);
            RectTransform rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(0.5f, 0f);
            rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(0f, barBottomOffset + barHeight + 18f);
            rt.sizeDelta = new Vector2(240f, 30f);

            Text txt = go.AddComponent<Text>();
            txt.fontSize = 24;
            txt.fontStyle = FontStyle.Bold;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.color = Color.white;
            txt.horizontalOverflow = HorizontalWrapMode.Overflow;
            txt.verticalOverflow = VerticalWrapMode.Overflow;
            txt.raycastTarget = false;

            Outline outline = go.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.85f);
            outline.effectDistance = new Vector2(1.5f, -1.5f);

            healthText = txt;
        }

        healthText.font = GetBuiltinFont();
        healthText.fontSize = 24;
        healthText.fontStyle = FontStyle.Bold;
        healthText.alignment = TextAnchor.MiddleCenter;
        healthText.transform.SetAsLastSibling(); // 永远画在最上层
    }

    void FixTextFont(Text txt)
    {
        if (txt == null) return;
        if (txt.font == null) txt.font = GetBuiltinFont();
    }

    /// <summary>Unity 6 的内置运行时字体；旧工程的 Arial.ttf 已不存在</summary>
    Font GetBuiltinFont()
    {
        Font f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (f == null) f = Resources.GetBuiltinResource<Font>("Arial.ttf");
        return f;
    }

    /// <summary>1×1 纯白 Sprite：给缺 Sprite 的 Image 兜底</summary>
    Sprite GetRuntimeSprite()
    {
        if (_runtimeSprite != null) return _runtimeSprite;

        Texture2D tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        tex.SetPixel(0, 0, Color.white);
        tex.Apply();
        tex.wrapMode = TextureWrapMode.Clamp;
        _runtimeSprite = Sprite.Create(tex, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 100f);
        _runtimeSprite.name = "HUD_White";
        return _runtimeSprite;
    }

    // ---------------------------------------------------------------- 刷新

    void Update()
    {
        if (GameManager.Instance != null)
        {
            if (scoreText != null)
                scoreText.text = $"Score: {GameManager.Instance.score}";

            if (livesText != null)
            {
                if (GameManager.Instance.lives > 1)
                {
                    livesText.enabled = true;
                    livesText.text = $"Lives: {GameManager.Instance.lives}";
                }
                else
                {
                    livesText.enabled = false;
                }
            }
        }

        // 兜底每帧刷新血量：即使受伤事件时序异常，数字与血条也不会停留在旧值
        RefreshHealth();
    }

    void UpdateHealthBar(float damage)
    {
        RefreshHealth();
    }

    /// <summary>统一刷新血条填充比例、血量数字与分段配色</summary>
    void RefreshHealth()
    {
        if (!IsAlive(player))
        {
            ResolvePlayer();
            if (player == null) return;
        }

        float ratio = player.maxHealth > 0f
            ? Mathf.Clamp01(player.CurrentHealth / player.maxHealth)
            : 0f;

        Color c = ratio > 0.5f ? healthNormalColor
                : ratio > 0.25f ? healthWarnColor
                : healthLowColor;

        if (healthBar != null)
        {
            healthBar.fillAmount = ratio;
            healthBar.color = c;
        }

        if (healthText != null)
        {
            int hp = Mathf.RoundToInt(player.CurrentHealth);
            healthText.text = $"HP: {hp}";
            healthText.color = c;
        }
    }

    /// <summary>显示 GameOver 面板，并把血量数字归零</summary>
    void ShowGameOver()
    {
        if (gameOverPanel != null)
            gameOverPanel.SetActive(true);

        if (healthText != null)
        {
            healthText.text = "HP: 0";
            healthText.color = healthLowColor;
        }
        if (healthBar != null)
        {
            healthBar.fillAmount = 0f;
            healthBar.color = healthLowColor;
        }
    }
}

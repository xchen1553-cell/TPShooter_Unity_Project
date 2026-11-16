using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 结算界面 —— 在 GameOverScene / WinScene 加载完成时，于该场景内生成一块结算面板：
/// 显示最终成绩，并提供「再玩一次」与「退出」两个按钮。
///
/// 之所以用 RuntimeInitializeOnLoadMethod 而不是往场景里塞组件：
/// 两个结算场景是纯静态文本场景（只有 Canvas + Text），直接改场景 YAML 风险高；
/// 用加载回调在这两个场景里现场搭 UI，既不动资产，也能保证在任何机器上都有字体可用
/// （场景里的 Text 引用的是旧版内置 Arial，在 Unity 6 里已经不渲染了）。
/// </summary>
public static class ResultScreen
{
    const string GameOverSceneName = "GameOverScene";
    const string WinSceneName = "WinScene";
    const string StartSceneName = "Level1";

    static Sprite _whiteSprite;
    static Font _font;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;

        // 如果直接从结算场景开始 Play（调试时常见），主动补一次
        Scene active = SceneManager.GetActiveScene();
        if (active.name == GameOverSceneName || active.name == WinSceneName)
            BuildPanel(active.name == WinSceneName);
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        bool isGameOver = scene.name == GameOverSceneName;
        bool isWin = scene.name == WinSceneName;
        if (!isGameOver && !isWin) return;

        BuildPanel(isWin);
    }

    // ---------------------------------------------------------------- 面板搭建

    static void BuildPanel(bool win)
    {
        // 结算场景里要让鼠标可用：解除锁定、显示光标
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        Time.timeScale = 1f;

        // 先把场景自带的静态文字关掉（那两个场景里是中文「生命耗尽，游戏结束。」之类），
        // 只保留本函数新建的英文面板，避免中英混排
        foreach (Text existing in Object.FindObjectsByType<Text>())
            existing.enabled = false;

        EnsureEventSystem();

        GameObject canvasGo = new GameObject("ResultCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Canvas canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 200;

        CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
        scaler.scaleFactor = 1f;

        // 全屏压暗 + 吞掉鼠标点击，避免误触到场景里别的东西
        Image dim = CreateImage(canvas.transform, "Dim", new Color(0f, 0f, 0f, 0.75f));
        StretchFull(dim.rectTransform);
        dim.raycastTarget = true;

        // 中间面板
        Image panel = CreateImage(canvas.transform, "Panel", new Color(0.11f, 0.12f, 0.14f, 0.96f));
        SetRect(panel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 0f), new Vector2(580f, 360f));

        // 标题
        Text title = CreateText(panel.transform, "Title", win ? "YOU WIN!" : "GAME OVER", 42, FontStyle.Bold, Color.white);
        SetRect(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -70f), new Vector2(540f, 60f));

        // 副标题（死因 / 通关说明）
        Text subtitle = CreateText(panel.transform, "Subtitle",
            win ? "All levels completed." : "Your health reached zero.", 22, FontStyle.Normal, new Color(0.75f, 0.78f, 0.82f));
        SetRect(subtitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -122f), new Vector2(540f, 34f));

        // 最终成绩
        int score = GameManager.Instance != null ? GameManager.Instance.score : 0;
        Text scoreText = CreateText(panel.transform, "Score", $"Final Score: {score}", 30, FontStyle.Bold,
            win ? new Color(0.55f, 0.95f, 0.6f) : new Color(1f, 0.85f, 0.35f));
        SetRect(scoreText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 18f), new Vector2(540f, 48f));

        // 两个按钮
        CreateButton(panel.transform, "ReplayButton", "PLAY AGAIN", new Vector2(-125f, -105f),
            new Color(0.18f, 0.62f, 0.32f, 1f), RestartGame);
        CreateButton(panel.transform, "QuitButton", "QUIT", new Vector2(125f, -105f),
            new Color(0.30f, 0.33f, 0.38f, 1f), QuitGame);
    }

    // ---------------------------------------------------------------- 按钮行为

    static void RestartGame()
    {
        Time.timeScale = 1f;
        if (GameManager.Instance != null) GameManager.Instance.ResetGame();
        if (LevelManager.Instance != null) LevelManager.Instance.ResetProgress();
        SceneManager.LoadScene(StartSceneName);
    }

    static void QuitGame()
    {
        Time.timeScale = 1f;
#if UNITY_EDITOR
        // 编辑器里「退出」= 停止 Play 模式
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // ---------------------------------------------------------------- UI 小工具

    static void EnsureEventSystem()
    {
        if (Object.FindAnyObjectByType<EventSystem>() != null) return;

        GameObject go = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        go.transform.SetAsFirstSibling();
    }

    static Image CreateImage(Transform parent, string name, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        Image img = go.GetComponent<Image>();
        img.sprite = GetWhiteSprite();
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    static Text CreateText(Transform parent, string name, string content, int size, FontStyle style, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);

        Text txt = go.AddComponent<Text>();
        txt.font = GetFont();
        txt.text = content;
        txt.fontSize = size;
        txt.fontStyle = style;
        txt.color = color;
        txt.alignment = TextAnchor.MiddleCenter;
        txt.horizontalOverflow = HorizontalWrapMode.Overflow;
        txt.verticalOverflow = VerticalWrapMode.Overflow;
        txt.raycastTarget = false;
        return txt;
    }

    static void CreateButton(Transform parent, string name, string label, Vector2 anchoredPos, Color color, UnityEngine.Events.UnityAction action)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);

        RectTransform rt = (RectTransform)go.transform;
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = new Vector2(210f, 58f);

        Image img = go.GetComponent<Image>();
        img.sprite = GetWhiteSprite();
        img.color = color;
        img.raycastTarget = true;

        Button btn = go.GetComponent<Button>();
        btn.targetGraphic = img;
        btn.onClick.AddListener(action);

        // 按钮文字
        Text txt = CreateText(go.transform, "Label", label, 26, FontStyle.Bold, Color.white);
        StretchFull(txt.rectTransform);
    }

    static void StretchFull(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    static void SetRect(RectTransform rt, Vector2 anchor, Vector2 anchoredPos, Vector2 size)
    {
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = size;
    }

    static Sprite GetWhiteSprite()
    {
        if (_whiteSprite != null) return _whiteSprite;

        Texture2D tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        tex.SetPixel(0, 0, Color.white);
        tex.Apply();
        tex.wrapMode = TextureWrapMode.Clamp;
        _whiteSprite = Sprite.Create(tex, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 100f);
        _whiteSprite.name = "Result_White";
        return _whiteSprite;
    }

    static Font GetFont()
    {
        if (_font != null) return _font;
        _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (_font == null) _font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        return _font;
    }
}

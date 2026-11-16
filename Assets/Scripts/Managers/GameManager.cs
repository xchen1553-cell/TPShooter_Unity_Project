using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("Game State")]
    public int score = 0;
    public int lives = 3;
    [Tooltip("勾选 = 保留多条命（死亡后重启本关）；取消 = 血量归零直接 Game Over（教授要求的行为）")]
    public bool restartLevelOnDeath = false;

    [Header("Death Timing 死亡过渡")]
    [Tooltip("血量归零后停留多久再切 GameOver 场景，让玩家看清 HP 归零与面板")]
    public float gameOverDelay = 1.2f;
    [Tooltip("还有命时，死亡后停留多久再重启本关")]
    public float restartDelay = 1.0f;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        // 一命到底模式下 lives 恒为 1，UI 上不再显示多余的 Lives 计数
        if (!restartLevelOnDeath)
            lives = 1;
    }

    public void AddScore(int amount)
    {
        score += amount;
        Debug.Log($"Score: {score}");
    }

    /// <summary>
    /// 从结算界面点「再玩一次」时调用：清空分数、恢复初始生命数。
    /// 本对象是 DontDestroyOnLoad 的，跨场景保留，所以必须显式复位。
    /// </summary>
    public void ResetGame()
    {
        score = 0;
        lives = restartLevelOnDeath ? 3 : 1;
        Debug.Log("[GameManager] 成绩已清零，重新开始");
    }

    /// <summary>
    /// 玩家死亡回调（血量归零 / 坠落）
    /// restartLevelOnDeath = false（默认）：血量归零 → 直接 Game Over
    /// restartLevelOnDeath = true：还有命 → 重启本关；命耗尽 → Game Over
    /// </summary>
    public void OnPlayerDeath()
    {
        lives--;
        Debug.Log($"玩家死亡！剩余生命：{lives}");

        if (restartLevelOnDeath && lives > 0)
            StartCoroutine(RestartLevelAfterDelay());
        else
            StartCoroutine(GameOverAfterDelay());
    }

    IEnumerator GameOverAfterDelay()
    {
        // 面板已由 UIManager 在 OnDeath 事件里弹出，这里留出时间让玩家看到 HP: 0
        if (gameOverDelay > 0f)
            yield return new WaitForSecondsRealtime(gameOverDelay);
        GameOver();
    }

    IEnumerator RestartLevelAfterDelay()
    {
        if (restartDelay > 0f)
            yield return new WaitForSecondsRealtime(restartDelay);
        RestartLevel();
    }

    /// <summary>生命耗尽 → 加载游戏结束场景</summary>
    void GameOver()
    {
        Debug.Log("Game Over! 游戏结束");
        Time.timeScale = 1f;
        SceneManager.LoadScene("GameOverScene");
    }

    /// <summary>生命还有 → 重新加载当前关卡（重置玩家位置与血量）</summary>
    void RestartLevel()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    public void WinGame()
    {
        Debug.Log("You Win!");
        Time.timeScale = 1f;
        SceneManager.LoadScene("WinScene");
    }
}

using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("Game State")]
    public int score = 0;
    public int lives = 3;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public void AddScore(int amount)
    {
        score += amount;
        Debug.Log($"Score: {score}");
    }

    /// <summary>
    /// 玩家死亡回调（掉落/被击杀）
    /// 讲义标准：生命还有 → 重启当前关卡；生命耗尽 → 游戏结束
    /// </summary>
    public void OnPlayerDeath()
    {
        lives--;
        Debug.Log($"玩家死亡！剩余生命：{lives}");

        if (lives <= 0)
        {
            GameOver();
        }
        else
        {
            RestartLevel();
        }
    }

    /// <summary>
    /// 生命耗尽 → 加载游戏结束场景
    /// </summary>
    void GameOver()
    {
        Debug.Log("Game Over! 游戏结束");
        Time.timeScale = 1f;
        SceneManager.LoadScene("GameOverScene");
    }

    /// <summary>
    /// 生命还有 → 重新加载当前关卡（重置玩家位置与血量）
    /// </summary>
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
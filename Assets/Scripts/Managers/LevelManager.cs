using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 关卡管理器 —— 单例，处理关卡进度
/// </summary>
public class LevelManager : MonoBehaviour
{
    public static LevelManager Instance { get; private set; }

    [Header("Levels")]
    public string[] levels = { "Level1", "Level2", "Level3" };

    public int currentLevelIndex = 0;

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

    /// <summary>
    /// 加载下一关
    /// </summary>
    public void LoadNextLevel()
    {
        currentLevelIndex++;

        if (currentLevelIndex >= levels.Length)
        {
            Win();
            return;
        }

        Debug.Log($"加载关卡：{levels[currentLevelIndex]}");
        SceneManager.LoadScene(levels[currentLevelIndex]);
    }

    /// <summary>
    /// 重新加载当前关卡
    /// </summary>
    public void RestartCurrentLevel()
    {
        SceneManager.LoadScene(levels[currentLevelIndex]);
    }

    /// <summary>
    /// 通关
    /// </summary>
    void Win()
    {
        Debug.Log("You Win!");
        SceneManager.LoadScene("WinScene");
    }
}

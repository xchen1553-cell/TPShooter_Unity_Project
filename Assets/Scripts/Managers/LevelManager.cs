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
    /// 若下一关超出关卡表，或该场景没有加进 Build Settings（例如工程里根本没有 Level3），
    /// 直接判定通关，而不是去 LoadScene 一个不存在的场景（那会报错并卡在旧场景）。
    /// </summary>
    public void LoadNextLevel()
    {
        currentLevelIndex++;

        if (currentLevelIndex >= levels.Length || !SceneExists(levels[currentLevelIndex]))
        {
            currentLevelIndex = levels.Length;
            Win();
            return;
        }

        Debug.Log($"加载关卡：{levels[currentLevelIndex]}");
        SceneManager.LoadScene(levels[currentLevelIndex]);
    }

    /// <summary>该场景是否真的可用（编辑器与打包后都适用）</summary>
    bool SceneExists(string sceneName)
    {
        return !string.IsNullOrEmpty(sceneName) && Application.CanStreamedLevelBeLoaded(sceneName);
    }

    /// <summary>从结算界面点「再玩一次」时调用：关卡进度归零</summary>
    public void ResetProgress()
    {
        currentLevelIndex = 0;
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
